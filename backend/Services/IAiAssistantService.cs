public interface IAiAssistantService
{
    Task<string> GenerateAnswerAsync(string question, AiPromptContext context, CancellationToken cancellationToken = default);
    Task<AnomalyExplainResponse> GenerateAnomalyExplanationAsync(AnomalyExplainRequest request, CancellationToken cancellationToken = default);
}

public class AiPromptContext
{
    public double TotalExpense { get; set; }
    public double ChangePercent { get; set; }
    public string TopCategory { get; set; } = "N/A";
    public List<AiAnomalyDto> Anomalies { get; set; } = new();
    public AiAlertContext? AlertContext { get; set; }
}

public class AiAlertContext
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public double Average { get; set; }
    public double Deviation { get; set; }
    public string Severity { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Suggestion { get; set; } = "";
}
