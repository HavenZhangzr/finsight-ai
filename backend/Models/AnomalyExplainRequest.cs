public class AnomalyExplainRequest
{
    public string Category { get; set; } = "";
    public decimal Current { get; set; }
    public decimal Avg { get; set; }
    public double Deviation { get; set; }
    public string RiskLevel { get; set; } = "";
}
