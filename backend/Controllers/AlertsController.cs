using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlertsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertDto>>> GetAlerts([FromQuery] bool includeLow = false, [FromQuery] int top = 20)
    {
        if (top <= 0) top = 20;
        if (top > 100) top = 100;

        var expenses = await _db.Expenses.OrderByDescending(e => e.Date).ToListAsync();
        if (expenses.Count == 0)
        {
            return Ok(Array.Empty<AlertDto>());
        }

        var mean = expenses.Average(e => e.Total);
        var variance = expenses.Sum(e => Math.Pow(e.Total - mean, 2)) / expenses.Count;
        var stdDev = Math.Sqrt(variance);
        if (stdDev == 0)
        {
            return Ok(Array.Empty<AlertDto>());
        }

        var rawAlerts = expenses
            .Select(e =>
            {
                var zScore = Math.Abs((e.Total - mean) / stdDev);
                var deviation = mean == 0 ? 0 : ((e.Total - mean) / mean) * 100;
                var severity = CalculateSeverity(zScore, deviation);
                return new AlertDto
                {
                    Title = (string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category) + " Expense Spike",
                    Category = string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category,
                    Amount = Math.Round(e.Total, 2),
                    Average = Math.Round(mean, 2),
                    Deviation = Math.Round(deviation, 2),
                    Severity = severity,
                    Explanation = GenerateExplanation(e.Category, e.Total, mean, deviation, severity),
                    Suggestion = GenerateSuggestion(e.Category, severity),
                    CreatedAt = e.Date,
                    ZScore = Math.Round(zScore, 2),
                    Occurrences = 1
                };
            })
            .ToList();

        var alerts = rawAlerts
            .GroupBy(a => NormalizeCategoryKey(a.Category))
            .Select(g =>
            {
                var representative = g
                    .OrderByDescending(a => SeverityRank(a.Severity))
                    .ThenByDescending(a => Math.Abs(a.Deviation))
                    .ThenByDescending(a => a.CreatedAt)
                    .First();

                var occurrences = g.Count();
                var latestDate = g.Max(a => a.CreatedAt);

                return new AlertDto
                {
                    Title = representative.Category + " Expense Spike",
                    Category = representative.Category,
                    Amount = representative.Amount,
                    Average = representative.Average,
                    Deviation = representative.Deviation,
                    Severity = representative.Severity,
                    Explanation = BuildAggregatedExplanation(representative.Explanation, occurrences),
                    Suggestion = representative.Suggestion,
                    CreatedAt = latestDate,
                    ZScore = representative.ZScore,
                    Occurrences = occurrences
                };
            })
            .Where(a => includeLow || string.Equals(a.Severity, "Low", StringComparison.OrdinalIgnoreCase) == false)
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenByDescending(a => Math.Abs(a.Deviation))
            .Take(top)
            .ToList();

        return Ok(alerts);
    }

    private static string CalculateSeverity(double zScore, double deviation)
    {
        if (zScore > 3 || deviation > 100) return "High";
        if (zScore > 2 || deviation > 50) return "Medium";
        return "Low";
    }

    private static int SeverityRank(string severity)
    {
        if (severity == "High") return 3;
        if (severity == "Medium") return 2;
        return 1;
    }

    private static string NormalizeCategoryKey(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "uncategorized" : category.Trim().ToLowerInvariant();
    }

    private static string BuildAggregatedExplanation(string baseExplanation, int occurrences)
    {
        if (occurrences <= 1) return baseExplanation;
        return baseExplanation + " Similar spikes appeared " + occurrences + " times.";
    }

    private static string GenerateExplanation(string? category, double amount, double average, double deviation, string severity)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category;
        var sign = deviation >= 0 ? "+" : "";
        return c + " spending is " + sign + deviation.ToString("0.##") + "% vs average (" +
               amount.ToString("0.00") + " vs " + average.ToString("0.00") + "), marked " + severity + " risk.";
    }

    private static string GenerateSuggestion(string? category, string severity)
    {
        var c = (category ?? string.Empty).ToLowerInvariant();

        if (c.Contains("marketing") || c.Contains("advert"))
            return "Review current campaigns, pause low-ROI channels, and reset campaign caps.";

        if (c.Contains("legal") || c.Contains("law"))
            return "Verify billing details with legal provider and confirm one-time vs recurring charges.";

        if (c.Contains("rent") || c.Contains("lease"))
            return "Check lease terms and investigate any non-standard rent adjustments.";

        if (severity == "High")
            return "Review related transactions immediately and validate supporting receipts.";

        if (severity == "Medium")
            return "Audit recent transactions in this category and set a tighter approval threshold.";

        return "Monitor this category next cycle and keep notes on unusual items.";
    }
}

public class AlertDto
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public double Average { get; set; }
    public double Deviation { get; set; }
    public string Severity { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public double ZScore { get; set; }
    public int Occurrences { get; set; }
}
