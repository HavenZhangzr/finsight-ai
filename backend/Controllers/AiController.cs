using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAiAssistantService _aiAssistant;

    public AiController(AppDbContext db, IAiAssistantService aiAssistant)
    {
        _db = db;
        _aiAssistant = aiAssistant;
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

        // Module 3 v2: OpenAI-backed answer generation.
        var answer = await _aiAssistant.GenerateAnswerAsync(question, context, cancellationToken);

        // Legacy mock logic intentionally kept (commented) for quick fallback if needed.
        // var answer = BuildAnswerMock(question, context.TotalExpense, context.ChangePercent, context.TopCategory, context.Anomalies);

        return Ok(new AiAskResponse
        {
            Answer = answer,
            Context = new AiContextDto
            {
                TotalExpense = Math.Round(context.TotalExpense, 2),
                ChangePercent = Math.Round(context.ChangePercent, 2),
                TopCategory = context.TopCategory,
                Anomalies = context.Anomalies
            }
        });
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

    /*
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
    */
}

public class AiAskRequest
{
    public string Question { get; set; } = "";
}

public class AiAskResponse
{
    public string Answer { get; set; } = "";
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
