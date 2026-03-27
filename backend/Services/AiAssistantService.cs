using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class AiAssistantService : IAiAssistantService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AiAssistantService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GenerateAnswerAsync(string question, AiPromptContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Set OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        var model = _config["OpenAI:Model"] ?? "gpt-4.1-mini";
        var payload = new
        {
            model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt() },
                new { role = "user", content = BuildUserPrompt(question, context) }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (resp.IsSuccessStatusCode == false)
        {
            throw new InvalidOperationException("OpenAI request failed: " + (int)resp.StatusCode + " " + raw);
        }

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI returned empty content.");
        }

        return content.Trim();
    }

    private static string BuildSystemPrompt()
    {
        var nl = Environment.NewLine;
        return
            "You are a financial assistant helping small business owners understand their expenses." + nl +
            "Your job is to:" + nl +
            "- Analyze financial data" + nl +
            "- Explain anomalies clearly" + nl +
            "- Provide practical and actionable suggestions" + nl +
            "Avoid generic answers. Be specific and concise.";
    }

    private static string BuildUserPrompt(string question, AiPromptContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Financial Summary:");
        sb.AppendLine("- Total Expense: $" + context.TotalExpense.ToString("0.00"));
        sb.AppendLine("- Change: " + SignedPercent(context.ChangePercent));
        sb.AppendLine("- Top Category: " + (string.IsNullOrWhiteSpace(context.TopCategory) ? "N/A" : context.TopCategory));
        sb.AppendLine();

        sb.AppendLine("Anomalies:");
        if (context.Anomalies.Count == 0)
        {
            sb.AppendLine("- No significant anomalies detected.");
        }
        else
        {
            for (var i = 0; i < context.Anomalies.Count; i++)
            {
                var a = context.Anomalies[i];
                var deviation = a.Average == 0 ? 0 : ((a.Amount - a.Average) / a.Average) * 100;
                sb.AppendLine((i + 1).ToString() + ". " + a.Category);
                sb.AppendLine("   - Amount: $" + a.Amount.ToString("0.00"));
                sb.AppendLine("   - Average: $" + a.Average.ToString("0.00"));
                sb.AppendLine("   - Deviation: " + SignedPercent(deviation));
            }
        }

        sb.AppendLine();

        if (context.AlertContext != null)
        {
            sb.AppendLine("Focused Alert Context:");
            sb.AppendLine("- Title: " + context.AlertContext.Title);
            sb.AppendLine("- Category: " + context.AlertContext.Category);
            sb.AppendLine("- Amount: $" + context.AlertContext.Amount.ToString("0.00"));
            sb.AppendLine("- Average: $" + context.AlertContext.Average.ToString("0.00"));
            sb.AppendLine("- Deviation: " + SignedPercent(context.AlertContext.Deviation));
            sb.AppendLine("- Severity: " + context.AlertContext.Severity);
            if (string.IsNullOrWhiteSpace(context.AlertContext.Explanation) == false)
            {
                sb.AppendLine("- Explanation: " + context.AlertContext.Explanation);
            }
            if (string.IsNullOrWhiteSpace(context.AlertContext.Suggestion) == false)
            {
                sb.AppendLine("- Suggestion: " + context.AlertContext.Suggestion);
            }
            sb.AppendLine();
        }

        sb.AppendLine("User Question:");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine(BuildOutputInstruction(question));

        return sb.ToString().Trim();
    }

    private static string BuildOutputInstruction(string question)
    {
        var nl = Environment.NewLine;
        var q = question.ToLowerInvariant();

        if (q.Contains("why") || q.Contains("increase") || q.Contains("rise"))
        {
            return "Answer in 3 parts:" + nl +
                   "1. Main Reason: explain the primary cause." + nl +
                   "2. Supporting Details: reference anomaly data." + nl +
                   "3. Suggested Action: provide 1-2 practical actions." + nl +
                   "Keep the answer under 120 words.";
        }

        if (q.Contains("biggest") || q.Contains("issue") || q.Contains("risk"))
        {
            return "Answer in 3 parts:" + nl +
                   "1. Biggest Issue: identify the most critical anomaly." + nl +
                   "2. Why It Matters: explain business impact briefly." + nl +
                   "3. Next Action: provide one concrete action." + nl +
                   "Keep the answer under 120 words.";
        }

        if (q.Contains("how") || q.Contains("reduce") || q.Contains("save") || q.Contains("cost"))
        {
            return "Answer in 3 parts:" + nl +
                   "1. Cost Drivers: identify top drivers." + nl +
                   "2. Reduction Plan: provide 2-3 concrete ways to reduce cost." + nl +
                   "3. Priority: suggest what to do first this week." + nl +
                   "Keep the answer under 140 words.";
        }

        return "Answer in 3 parts:" + nl +
               "1. Key Insight" + nl +
               "2. Supporting Data" + nl +
               "3. Suggested Action" + nl +
               "Keep the answer under 120 words.";
    }

    private static string SignedPercent(double value)
    {
        var sign = value >= 0 ? "+" : "";
        return sign + value.ToString("0.##") + "%";
    }
}
