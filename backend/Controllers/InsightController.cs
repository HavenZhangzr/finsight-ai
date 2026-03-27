using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class InsightController : ControllerBase
{
    private readonly AppDbContext _db;

    public InsightController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("trends")]
    public async Task<ActionResult<IEnumerable<TrendPointDto>>> GetExpenseTrends(
        [FromQuery] string granularity = "month",
        [FromQuery] int periods = 6)
    {
        var mode = NormalizeGranularity(granularity);
        var boundedPeriods = GetBoundedPeriods(mode, periods);

        var now = DateTime.UtcNow.Date;
        var buckets = BuildBuckets(mode, boundedPeriods, now);
        var firstBucket = buckets.First();

        var expenses = await _db.Expenses
            .Where(e => e.Date >= firstBucket)
            .ToListAsync();

        var grouped = expenses
            .GroupBy(e => NormalizeToBucket(e.Date, mode))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        var result = buckets.Select(bucket => new TrendPointDto
        {
            Period = FormatBucketLabel(bucket, mode),
            BucketStart = bucket.ToString("yyyy-MM-dd"),
            Total = Math.Round(grouped.TryGetValue(bucket, out var value) ? value : 0d, 2)
        });

        return Ok(result);
    }

    [HttpGet("category-breakdown")]
    public async Task<ActionResult<IEnumerable<CategoryBreakdownDto>>> GetCategoryBreakdown([FromQuery] int top = 6)
    {
        if (top <= 0) top = 6;
        if (top > 20) top = 20;

        var grouped = await _db.Expenses
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category)
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Sum(x => x.Total),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        var overall = grouped.Sum(x => x.Total);
        if (overall <= 0)
        {
            return Ok(Array.Empty<CategoryBreakdownDto>());
        }

        var topItems = grouped.Take(top).ToList();
        var otherItems = grouped.Skip(top).ToList();

        var result = topItems.Select(x => new CategoryBreakdownDto
        {
            Category = x.Category,
            Total = Math.Round(x.Total, 2),
            Count = x.Count,
            Percentage = Math.Round((x.Total / overall) * 100, 2)
        }).ToList();

        if (otherItems.Count > 0)
        {
            var otherTotal = otherItems.Sum(x => x.Total);
            result.Add(new CategoryBreakdownDto
            {
                Category = "Other",
                Total = Math.Round(otherTotal, 2),
                Count = otherItems.Sum(x => x.Count),
                Percentage = Math.Round((otherTotal / overall) * 100, 2)
            });
        }

        return Ok(result.OrderByDescending(x => x.Total));
    }

    [HttpGet("anomaly-explanations")]
    public async Task<ActionResult<IEnumerable<AnomalyExplanationDto>>> GetAnomalyExplanations([FromQuery] int top = 6)
    {
        if (top <= 0) top = 6;
        if (top > 30) top = 30;

        var expenses = await _db.Expenses
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        if (expenses.Count == 0)
        {
            return Ok(Array.Empty<AnomalyExplanationDto>());
        }

        var mean = expenses.Average(e => e.Total);
        var variance = expenses.Sum(e => Math.Pow(e.Total - mean, 2)) / expenses.Count;
        var stdDev = Math.Sqrt(variance);

        if (stdDev == 0)
        {
            return Ok(Array.Empty<AnomalyExplanationDto>());
        }

        var result = expenses
            .Select(e =>
            {
                var zScore = Math.Abs((e.Total - mean) / stdDev);
                var severity = GetSeverity(zScore);
                var deviationPercent = mean == 0 ? 0 : ((e.Total - mean) / mean) * 100;
                var recommendation = GetRecommendation(severity, e.Category);

                return new AnomalyExplanationDto
                {
                    ExpenseId = e.Id,
                    Date = e.Date.ToString("yyyy-MM-dd"),
                    Payee = e.Payee,
                    Category = string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category,
                    Total = Math.Round(e.Total, 2),
                    AverageAmount = Math.Round(mean, 2),
                    DeviationPercent = Math.Round(deviationPercent, 2),
                    ZScore = Math.Round(zScore, 2),
                    Severity = severity,
                    Method = "ZScore",
                    RiskLevel = severity,
                    Explanation = string.Format(
                        "Spent {0:0.00} (Avg {1:0.00}), deviation {2:+0.##;-0.##;0}%.",
                        e.Total,
                        mean,
                        deviationPercent),
                    Recommendation = recommendation
                };
            })
            .Where(x => x.ZScore >= 1.8)
            .OrderByDescending(x => x.ZScore)
            .Take(top)
            .ToList();

        return Ok(result);
    }

    private static string NormalizeGranularity(string? raw)
    {
        var value = (raw ?? "month").Trim().ToLowerInvariant();
        if (value == "day" || value == "week" || value == "month")
        {
            return value;
        }
        return "month";
    }

    private static int GetBoundedPeriods(string granularity, int periods)
    {
        var defaultPeriods = granularity == "day" ? 14 : granularity == "week" ? 12 : 6;
        var maxPeriods = granularity == "day" ? 60 : granularity == "week" ? 52 : 24;

        if (periods <= 0) return defaultPeriods;
        if (periods > maxPeriods) return maxPeriods;
        return periods;
    }

    private static List<DateTime> BuildBuckets(string granularity, int periods, DateTime now)
    {
        if (granularity == "day")
        {
            var start = now.AddDays(-(periods - 1));
            return Enumerable.Range(0, periods).Select(i => start.AddDays(i)).ToList();
        }

        if (granularity == "week")
        {
            var currentWeek = StartOfWeek(now);
            var start = currentWeek.AddDays(-7 * (periods - 1));
            return Enumerable.Range(0, periods).Select(i => start.AddDays(i * 7)).ToList();
        }

        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var monthStart = currentMonth.AddMonths(-(periods - 1));
        return Enumerable.Range(0, periods).Select(i => monthStart.AddMonths(i)).ToList();
    }

    private static DateTime NormalizeToBucket(DateTime input, string granularity)
    {
        var date = input.Date;
        if (granularity == "day") return date;
        if (granularity == "week") return StartOfWeek(date);
        return new DateTime(date.Year, date.Month, 1);
    }

    private static string FormatBucketLabel(DateTime bucket, string granularity)
    {
        if (granularity == "day") return bucket.ToString("MM-dd");
        if (granularity == "week")
        {
            var week = ISOWeek.GetWeekOfYear(bucket);
            return "W" + week.ToString("D2") + " " + bucket.ToString("yyyy");
        }
        return bucket.ToString("yyyy-MM");
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset).Date;
    }

    private static string GetSeverity(double zScore)
    {
        if (zScore >= 3) return "High";
        if (zScore >= 2) return "Medium";
        return "Low";
    }

    private static string GetRecommendation(string severity, string category)
    {
        var categoryName = string.IsNullOrWhiteSpace(category) ? "this category" : category;
        if (severity == "High")
        {
            return "Review transactions in " + categoryName + " immediately and verify receipts.";
        }
        if (severity == "Medium")
        {
            return "Monitor recent spending in " + categoryName + " and confirm business need.";
        }
        return "Track this pattern for the next cycle and keep supporting notes.";
    }
}

public class TrendPointDto
{
    public string Period { get; set; } = "";
    public string BucketStart { get; set; } = "";
    public double Total { get; set; }
}

public class CategoryBreakdownDto
{
    public string Category { get; set; } = "";
    public double Total { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class AnomalyExplanationDto
{
    public int ExpenseId { get; set; }
    public string Date { get; set; } = "";
    public string Payee { get; set; } = "";
    public string Category { get; set; } = "";
    public double Total { get; set; }
    public double AverageAmount { get; set; }
    public double DeviationPercent { get; set; }
    public double ZScore { get; set; }
    public string Severity { get; set; } = "";
    public string Method { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Recommendation { get; set; } = "";
}
