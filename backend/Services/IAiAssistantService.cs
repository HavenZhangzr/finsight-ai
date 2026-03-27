public interface IAiAssistantService
{
    Task<string> GenerateAnswerAsync(string question, AiPromptContext context, CancellationToken cancellationToken = default);
}

public class AiPromptContext
{
    public double TotalExpense { get; set; }
    public double ChangePercent { get; set; }
    public string TopCategory { get; set; } = "N/A";
    public List<AiAnomalyDto> Anomalies { get; set; } = new();
}
