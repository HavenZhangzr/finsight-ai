public class AnomalyExplainResponse
{
    public string Summary { get; set; } = "";
    public List<string> Causes { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public string Model { get; set; } = "";
    public string? FallbackReason { get; set; }
}
