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
        var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";
        var content = await SendChatCompletionAsync(
            BuildSystemPrompt(),
            BuildUserPrompt(question, context),
            model,
            0.2,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI returned empty content.");
        }

        return content.Trim();
    }

    public async Task<AnomalyExplainResponse> GenerateAnomalyExplanationAsync(AnomalyExplainRequest request, CancellationToken cancellationToken = default)
    {
        var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";
        var raw = await SendChatCompletionAsync(
            BuildAnomalySystemPrompt(),
            BuildAnomalyUserPrompt(request),
            model,
            0.1,
            cancellationToken);

        var parsed = ParseJsonResponse(raw);
        parsed.Model = model;

        if (string.IsNullOrWhiteSpace(parsed.Summary))
        {
            throw new InvalidOperationException("OpenAI anomaly explanation returned empty summary.");
        }

        if (parsed.Causes.Count == 0)
        {
            parsed.Causes.Add("Unexpected expense behavior compared with historical baseline.");
        }

        if (parsed.Actions.Count == 0)
        {
            parsed.Actions.Add("Review this transaction and verify supporting details.");
        }

        return parsed;
    }

    private async Task<string> SendChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        double temperature,
        CancellationToken cancellationToken)
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

        var payload = new
        {
            model,
            temperature,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
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
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static string BuildSystemPrompt()
    {
        var nl = Environment.NewLine;
        return
            "You are a financial decision assistant for a business expense management product." + nl +
            "Your job is to analyze overall expense patterns and anomalies, then give clear, actionable guidance." + nl +
            "Focus on trends, biggest issues, and cost optimization opportunities." + nl +
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
                var anomaly = context.Anomalies[i];
                var deviation = anomaly.Average == 0 ? 0 : ((anomaly.Amount - anomaly.Average) / anomaly.Average) * 100;
                sb.AppendLine((i + 1).ToString() + ". " + anomaly.Category);
                sb.AppendLine("   - Amount: $" + anomaly.Amount.ToString("0.00"));
                sb.AppendLine("   - Average: $" + anomaly.Average.ToString("0.00"));
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
        sb.AppendLine(BuildOutputInstruction());

        return sb.ToString().Trim();
    }

    private static string BuildAnomalySystemPrompt()
    {
        return "You are an AI financial assistant analyzing a specific expense anomaly. Return valid JSON only.";
    }

    private static string BuildAnomalyUserPrompt(AnomalyExplainRequest request)
    {
        return
            "Analyze this anomaly and return JSON only:" + Environment.NewLine +
            "- Category: " + request.Category + Environment.NewLine +
            "- Current amount: $" + request.Current.ToString("0.##") + Environment.NewLine +
            "- Average amount: $" + request.Avg.ToString("0.##") + Environment.NewLine +
            "- Deviation: " + request.Deviation.ToString("0.##") + "%" + Environment.NewLine +
            "- Risk level: " + request.RiskLevel + Environment.NewLine +
            Environment.NewLine +
            "Return JSON with this exact shape:" + Environment.NewLine +
            "{" + Environment.NewLine +
            "  \"summary\": \"...\"," + Environment.NewLine +
            "  \"causes\": [\"...\", \"...\"]," + Environment.NewLine +
            "  \"actions\": [\"...\", \"...\"]" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "Rules:" + Environment.NewLine +
            "- Keep it concise" + Environment.NewLine +
            "- Use real numbers provided" + Environment.NewLine +
            "- Causes: 2-3 items" + Environment.NewLine +
            "- Actions: practical and actionable" + Environment.NewLine +
            "- Summary must state whether this looks normal or suspicious";
    }

    private static AnomalyExplainResponse ParseJsonResponse(string raw)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var trimmed = raw.Trim();

        try
        {
            var parsed = JsonSerializer.Deserialize<AnomalyExplainResponse>(trimmed, options);
            if (parsed != null)
            {
                return parsed;
            }
        }
        catch
        {
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var jsonOnly = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            var parsed = JsonSerializer.Deserialize<AnomalyExplainResponse>(jsonOnly, options);
            if (parsed != null)
            {
                return parsed;
            }
        }

        throw new InvalidOperationException("Unable to parse AI anomaly explanation JSON.");
    }

    private static string BuildOutputInstruction()
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
            "Summary:" + nl +
            "Short one-line explanation" + nl +
            "" + nl +
            "Key insights:" + nl +
            "• Key insight 1" + nl +
            "• Key insight 2" + nl +
            "" + nl +
            "Recommended actions:" + nl +
            "• Action 1" + nl +
            "• Action 2";
    }

    private static string SignedPercent(double value)
    {
        var sign = value >= 0 ? "+" : "";
        return sign + value.ToString("0.##") + "%";
    }
}
