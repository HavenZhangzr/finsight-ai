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
        var configuredKey = _config["OpenAI:ApiKey"];
        var apiKey = string.IsNullOrWhiteSpace(configuredKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : configuredKey;
        apiKey = apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Set OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";
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
            "You are an AI financial assistant for a business expense management product." + nl +
            "Your job is to analyze expense patterns and anomalies, then give clear, actionable guidance." + nl +
            "Write like a product assistant, not an academic report." + nl +
            "Keep answers concise, practical, and easy to scan.";
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
        return
            "Instructions:" + nl +
            "- Be concise and practical." + nl +
            "- Use simple business language." + nl +
            "- Highlight key numbers (amounts and percentages)." + nl +
            "- Do NOT write long paragraphs." + nl +
            "- Each bullet MUST be on a separate line." + nl +
            "- DO NOT put multiple bullets in one sentence." + nl +
            "- Use clean line breaks." + nl +
            "" + nl +
            "Output EXACTLY in this format:" + nl +
            "⚠️ Summary:" + nl +
            "Short one-line explanation" + nl +
            "" + nl +
            "• Key insight 1" + nl +
            "• Key insight 2" + nl +
            "" + nl +
            "💡 Suggested actions:" + nl +
            "• Action 1" + nl +
            "• Action 2" + nl +
            "" + nl +
            "Bad example (DO NOT do this):" + nl +
            "• A • B • C in one paragraph" + nl +
            "Good example:" + nl +
            "• A" + nl +
            "• B" + nl +
            "• C";
    }

    private static string SignedPercent(double value)
    {
        var sign = value >= 0 ? "+" : "";
        return sign + value.ToString("0.##") + "%";
    }
}
