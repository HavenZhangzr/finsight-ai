using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExpenseAnomalyDetectionService _anomalyService;

    public AlertsController(AppDbContext db, IExpenseAnomalyDetectionService anomalyService)
    {
        _db = db;
        _anomalyService = anomalyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertDto>>> GetAlerts(
        [FromQuery] bool includeLow = false,
        [FromQuery] int top = 20,
        [FromQuery] double threshold = 3.0)
    {
        if (top <= 0) top = 20;
        if (top > 100) top = 100;
        if (threshold <= 0) threshold = 3.0;

        var expenses = await _db.Expenses.OrderByDescending(e => e.Date).ToListAsync();
        if (expenses.Count == 0)
        {
            return Ok(Array.Empty<AlertDto>());
        }

        var allData = expenses.Select(MapExpenseToData).ToList();
        var anomalyCandidates = await _anomalyService.GetAmountOutliersAsync(allData, threshold);
        if (anomalyCandidates.Count == 0)
        {
            return Ok(Array.Empty<AlertDto>());
        }

        var candidateKeys = anomalyCandidates
            .Select(ToDataKey)
            .ToHashSet(StringComparer.Ordinal);

        var outlierExpenses = expenses
            .Where(e => candidateKeys.Contains(ToDataKey(MapExpenseToData(e))))
            .ToList();

        if (outlierExpenses.Count == 0)
        {
            return Ok(Array.Empty<AlertDto>());
        }

        var mean = expenses.Average(e => e.Total);

        // Hybrid candidates: ML/service outliers + rule-based medium/high deviation candidates.
        var ruleCandidateKeys = expenses
            .Where(e => mean != 0 && ((e.Total - mean) / mean) * 100 > 50)
            .Select(e => ToDataKey(MapExpenseToData(e)))
            .ToHashSet(StringComparer.Ordinal);

        var mergedOutlierExpenses = expenses
            .Where(e => candidateKeys.Contains(ToDataKey(MapExpenseToData(e))) || ruleCandidateKeys.Contains(ToDataKey(MapExpenseToData(e))))
            .ToList();

        var rawAlerts = new List<AlertDto>();
        foreach (var e in mergedOutlierExpenses)
        {
            var deviation = mean == 0 ? 0 : ((e.Total - mean) / mean) * 100;
            var severity = NormalizeRiskLevel(null, null, deviation);
            var method = "RuleFallback";
            var zScore = 0d;

            try
            {
                var detect = await _anomalyService.DetectSingleEntryAnomalyAsync(MapExpenseToData(e), threshold);
                severity = NormalizeRiskLevel(detect.RiskLevel, detect.Score, deviation);
                method = detect.Method ?? "Unknown";
                zScore = detect.Score.HasValue ? Math.Round(detect.Score.Value, 2) : 0;
            }
            catch
            {
                // Per-item fallback: keep dashboard resilient if anomaly engine fails for one row.
            }

            rawAlerts.Add(new AlertDto
            {
                Title = (string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category) + " Expense Spike",
                Category = string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category,
                Amount = Math.Round(e.Total, 2),
                Average = Math.Round(mean, 2),
                Deviation = Math.Round(deviation, 2),
                Severity = severity,
                Explanation = GenerateExplanation(e.Category, e.Total, mean, deviation, severity, method),
                Suggestion = GenerateSuggestion(e.Category, severity),
                CreatedAt = e.Date,
                ZScore = zScore,
                Occurrences = 1,
                Method = method
            });
        }

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
                    Occurrences = occurrences,
                    Method = representative.Method
                };
            })
            .Where(a => includeLow || string.Equals(a.Severity, "Low", StringComparison.OrdinalIgnoreCase) == false)
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenByDescending(a => Math.Abs(a.Deviation))
            .Take(top)
            .ToList();

        return Ok(alerts);
    }

    private static ExpenseData MapExpenseToData(Expense e)
    {
        return new ExpenseData
        {
            Date = e.Date,
            Type = e.Type,
            Payee = e.Payee,
            Category = e.Category,
            Total = e.Total,
            Description = e.Description
        };
    }

    private static string ToDataKey(ExpenseData d)
    {
        return d.Date.Date.ToString("yyyy-MM-dd") + "|" +
               (d.Type ?? string.Empty).Trim() + "|" +
               (d.Payee ?? string.Empty).Trim() + "|" +
               (d.Category ?? string.Empty).Trim() + "|" +
               d.Total.ToString("0.00");
    }

    private static string NormalizeRiskLevel(string? riskLevel, double? score, double deviation)
    {
        var normalized = "Low";

        if (deviation > 100) normalized = "High";
        else if (deviation > 50) normalized = "Medium";

        if (score.HasValue)
        {
            if (score.Value >= 3) normalized = "High";
            else if (score.Value >= 2 && normalized == "Low") normalized = "Medium";
        }

        var risk = (riskLevel ?? string.Empty).Trim();
        if (risk.Contains("高") && normalized == "Low") return "Medium";
        if (risk.Contains("中") && normalized == "Low") return "Medium";
        if (risk.Contains("低")) return normalized;
        return normalized;
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

    private static string GenerateExplanation(string? category, double amount, double average, double deviation, string severity, string? method)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category;
        var sign = deviation >= 0 ? "+" : "";
        var m = string.IsNullOrWhiteSpace(method) ? "AnomalyEngine" : method;
        return c + " spending is " + sign + deviation.ToString("0.##") + "% vs average (" +
               amount.ToString("0.00") + " vs " + average.ToString("0.00") + "), marked " + severity +
               " risk by " + m + ".";
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
    public string Method { get; set; } = "";
}
