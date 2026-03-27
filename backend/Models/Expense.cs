public class Expense
{
    public int Id { get; set; }              // 数据库主键
    public DateTime Date { get; set; }       // 业务时间
    public string Type { get; set; } = "";   // 类型
    public string Payee { get; set; } = "";  // 支付对象
    public string Category { get; set; } = "";// 类别
    public double Total { get; set; }        // 金额
    public string? Description { get; set; } // 自由文本描述
}