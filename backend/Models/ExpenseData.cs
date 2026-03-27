using Microsoft.ML.Data;

/*
 * 费用数据模型
 * 这是输入数据结构，对应CSV每一行的字段。用它告诉ML.NET：“每条账单/交易，包含这些特征”。
 */
public class ExpenseData
{
    [LoadColumn(0)]
    public DateTime Date { get; set; }
    [LoadColumn(1)]
    public string? Type { get; set; }
    [LoadColumn(2)]
    public string? Payee { get; set; }
    [LoadColumn(3)]
    public string? Category { get; set; } // 分类目标Label
    [LoadColumn(4)]
    public double Total { get; set; }
    [LoadColumn(5)]
    public string? Description { get; set; }  // 自由文本描述
}

/*
 * 费用预测模型
 * 这是输出数据结构，表示模型的预测结果。用它告诉ML.NET：“这是我想要预测的内容”。
 */
public class ExpensePrediction
{
    [ColumnName("PredictedLabel")]
    public string? Category;
    public float[]? Score { get; set; } // 每个类别的置信度分数数组
}

/*
 * 提供给前端的“分类+置信度”结构
 */
public class CategoryScoreResult
{
    public string Category { get; set; }
    public float Score { get; set; }
}

/*
 * 异常检测预测结构
 * 这是异常检测模型的输出数据结构。用它告诉ML.NET：“这是我想要的检测结果”。
 */
public class AnomalyPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; } // 是否异常 (true 表示异常)
    
    public float Score { get; set; } // 异常分数 (值越高表示越偏离正常数据)
}

public class AnomalyResult
{
    public bool IsAnomaly { get; set; }
    public double? Score { get; set; } // 异常分数
    public string? Method { get; set; } // 可选：ZScore/IsolationForest
    public string? RiskLevel { get; set; } // 新增风险分级字段，便于前端拓展
}