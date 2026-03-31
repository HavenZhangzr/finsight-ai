using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAiAssistantService _aiAssistant;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AiController(AppDbContext db, IAiAssistantService aiAssistant, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _aiAssistant = aiAssistant;
        _config = config;
        _env = env;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiAskResponse>> Ask([FromBody] AiAskRequest request, CancellationToken cancellationToken)
    {
        var question = (request.Question ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            return BadRequest(new { message = "Question is required." });
        }

        var context = await BuildPromptContextAsync(cancellationToken);

        if (request.AlertContext != null)
        {
            context.AlertContext = new AiAlertContext
            {
                Title = request.AlertContext.Title ?? string.Empty,
                Category = request.AlertContext.Category ?? string.Empty,
                Amount = request.AlertContext.Amount,
                Average = request.AlertContext.Average,
                Deviation = request.AlertContext.Deviation,
                Severity = request.AlertContext.Severity ?? string.Empty,
                Explanation = request.AlertContext.Explanation ?? string.Empty,
                Suggestion = request.AlertContext.Suggestion ?? string.Empty
            };
        }

        var configuredModel = _config["OpenAI:Model"] ?? "gpt-4o-mini";
        var useMockFallback = _config.GetValue<bool?>("OpenAI:UseMockFallback") ?? true;
        string answer;
        string modelLabel;
        string? fallbackReason = null;

        try
        {
            answer = await _aiAssistant.GenerateAnswerAsync(question, context, cancellationToken);
            modelLabel = configuredModel;
        }
        catch (Exception ex) when (useMockFallback)
        {
            answer = BuildAnswerMock(question, context.TotalExpense, context.ChangePercent, context.TopCategory, context.Anomalies);
            modelLabel = "Mock AI";
            if (_env.IsDevelopment())
            {
                fallbackReason = ex.Message;
            }
        }

        return Ok(new AiAskResponse
        {
            Answer = answer,
            Model = modelLabel,
            FallbackReason = fallbackReason,
            Context = new AiContextDto
            {
                TotalExpense = Math.Round(context.TotalExpense, 2),
                ChangePercent = Math.Round(context.ChangePercent, 2),
                TopCategory = context.TopCategory,
                Anomalies = context.Anomalies
            }
        });
    }

    [HttpPost("explain-anomaly")]
    public async Task<ActionResult<AnomalyExplainResponse>> ExplainAnomaly([FromBody] AnomalyExplainRequest request, CancellationToken cancellationToken)
    {
        if (request.Current <= 0)
        {
            return BadRequest(new { message = "Current amount must be greater than 0." });
        }

        var configuredModel = _config["OpenAI:Model"] ?? "gpt-4o-mini";
        var useMockFallback = _config.GetValue<bool?>("OpenAI:UseMockFallback") ?? true;

        try
        {
            var response = await _aiAssistant.GenerateAnomalyExplanationAsync(request, cancellationToken);
            response.Model = configuredModel;
            return Ok(response);
        }
        catch (Exception ex) when (useMockFallback)
        {
            var fallback = BuildAnomalyExplainMock(request);
            fallback.Model = "Mock AI";
            if (_env.IsDevelopment())
            {
                fallback.FallbackReason = ex.Message;
            }
            return Ok(fallback);
        }
    }

    private async Task<AiPromptContext> BuildPromptContextAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        var thisMonthExpenses = await _db.Expenses
            .Where(e => e.Date >= monthStart)
            .ToListAsync(cancellationToken);

        var prevMonthExpenses = await _db.Expenses
            .Where(e => e.Date >= prevMonthStart && e.Date < monthStart)
            .ToListAsync(cancellationToken);

        var thisMonthTotal = thisMonthExpenses.Sum(e => e.Total);
        var prevMonthTotal = prevMonthExpenses.Sum(e => e.Total);
        var changePercent = prevMonthTotal == 0 ? 0 : ((thisMonthTotal - prevMonthTotal) / prevMonthTotal) * 100;

        var topCategory = thisMonthExpenses
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Total)
            .Select(x => x.Category)
            .FirstOrDefault() ?? "N/A";

        var allExpenses = await _db.Expenses.ToListAsync(cancellationToken);
        var anomalies = BuildAnomalies(allExpenses).Take(3).ToList();

        return new AiPromptContext
        {
            TotalExpense = thisMonthTotal,
            ChangePercent = changePercent,
            TopCategory = topCategory,
            Anomalies = anomalies
        };
    }

    private static List<AiAnomalyDto> BuildAnomalies(List<Expense> expenses)
    {
        if (expenses.Count == 0) return new List<AiAnomalyDto>();

        var mean = expenses.Average(e => e.Total);
        var variance = expenses.Sum(e => Math.Pow(e.Total - mean, 2)) / expenses.Count;
        var stdDev = Math.Sqrt(variance);

        if (stdDev == 0) return new List<AiAnomalyDto>();

        return expenses
            .Select(e =>
            {
                var z = Math.Abs((e.Total - mean) / stdDev);
                return new AiAnomalyDto
                {
                    Category = string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category,
                    Amount = Math.Round(e.Total, 2),
                    Average = Math.Round(mean, 2),
                    ZScore = Math.Round(z, 2)
                };
            })
            .Where(x => x.ZScore >= 1.8)
            .OrderByDescending(x => x.ZScore)
            .ToList();
    }

    private static AnomalyExplainResponse BuildAnomalyExplainMock(AnomalyExplainRequest request)
    {
        var deviationText = SignedPercent(request.Deviation);
        var risk = string.IsNullOrWhiteSpace(request.RiskLevel) ? "Unknown" : request.RiskLevel;

        var urgent = risk.Contains("高", StringComparison.OrdinalIgnoreCase) || request.Deviation >= 100;

        return new AnomalyExplainResponse
        {
            Summary = "" +
                request.Category + " spending is " + deviationText + " versus average ($" +
                request.Avg.ToString("0.##") + " -> $" + request.Current.ToString("0.##") + "), classified as " + risk + ".",
            Causes = new List<string>
            {
                "Potential one-time purchase or exceptional invoice.",
                "Possible data entry issue (amount or category mismatch).",
                "Unusual vendor or billing cycle timing."
            },
            Actions = urgent
                ? new List<string>
                {
                    "Verify amount and source invoice immediately.",
                    "Confirm whether this cost is one-time or recurring.",
                    "Review category assignment before final approval."
                }
                : new List<string>
                {
                    "Review this entry against recent similar expenses.",
                    "Validate supporting documents and vendor details.",
                    "Monitor this category in the next cycle."
                }
        };
    }

    private static string BuildAnswerMock(
        string question,
        double thisMonthTotal,
        double changePercent,
        string topCategory,
        List<AiAnomalyDto> anomalies)
    {
        var q = question.ToLowerInvariant();
        var topAnomaly = anomalies.FirstOrDefault();

        if (q.Contains("increase") || q.Contains("rise") || q.Contains("up"))
        {
            if (topAnomaly != null)
            {
                return "Expenses increased mainly due to a spike in " + topAnomaly.Category +
                       ". This month is " + SignedPercent(changePercent) +
                       " versus last month. Review high-value transactions in that category first.";
            }

            return "This month total spending is " + thisMonthTotal.ToString("0.00") +
                   " and change vs last month is " + SignedPercent(changePercent) +
                   ". The largest category this month is " + topCategory + ".";
        }

        if (q.Contains("biggest") || q.Contains("issue") || q.Contains("risk"))
        {
            if (topAnomaly != null)
            {
                return "The biggest issue right now is " + topAnomaly.Category +
                       " spending at " + topAnomaly.Amount.ToString("0.00") +
                       " (historical average " + topAnomaly.Average.ToString("0.00") +
                       ", z-score " + topAnomaly.ZScore.ToString("0.00") + ").";
            }

            return "No strong anomalies were detected. Focus on your top category " + topCategory +
                   " and monitor weekly changes.";
        }

        if (q.Contains("reduce") || q.Contains("save") || q.Contains("cost"))
        {
            var first = topAnomaly?.Category ?? topCategory;
            return "To reduce costs: 1) audit recent " + first +
                   " expenses, 2) set a monthly cap for top categories, 3) review any transaction above your normal average before approval.";
        }

        return "Summary: this month spending is " + thisMonthTotal.ToString("0.00") +
               " with change " + SignedPercent(changePercent) +
               ". Top category is " + topCategory +
               ". Ask about increase, biggest issue, or cost reduction for focused guidance.";
    }

    private static string SignedPercent(double value)
    {
        if (value >= 0) return "+" + value.ToString("0.0") + "%";
        return value.ToString("0.0") + "%";
    }
}

public class AiAskRequest
{
    public string Question { get; set; } = "";
    public AiAskAlertContext? AlertContext { get; set; }
}

public class AiAskAlertContext
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public double Amount { get; set; }
    public double Average { get; set; }
    public double Deviation { get; set; }
    public string? Severity { get; set; }
    public string? Explanation { get; set; }
    public string? Suggestion { get; set; }
}

public class AiAskResponse
{
    public string Answer { get; set; } = "";
    public string Model { get; set; } = "";
    public string? FallbackReason { get; set; }
    public AiContextDto Context { get; set; } = new AiContextDto();
}

public class AiContextDto
{
    public double TotalExpense { get; set; }
    public double ChangePercent { get; set; }
    public string TopCategory { get; set; } = "";
    public List<AiAnomalyDto> Anomalies { get; set; } = new List<AiAnomalyDto>();
}

public class AiAnomalyDto
{
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public double Average { get; set; }
    public double ZScore { get; set; }
}
