using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.EntityFrameworkCore;


public class ExpenseAnomalyDetectionService : IExpenseAnomalyDetectionService
{
    // ML.NET 上下文: 一个 ML.NET 的核心类，用于管理机器学习相关的模型训练、预测管道。它是 ML.NET 的入口点。
    private readonly MLContext _mlContext;
    private readonly AppDbContext _db;

    private ITransformer? _isolationForestModel;
    private const string ISF_MODEL_PATH = "Models/isolation_forest_model.zip";

    // 构造函数负责初始化 _mlContext 和 _quickBooksService，实现依赖注入。
    public ExpenseAnomalyDetectionService(AppDbContext db)
    {
        _db = db;
        _mlContext = new MLContext();
        InitializeIsolationForestModel(ISF_MODEL_PATH);
    }

    private void InitializeIsolationForestModel(string modelPath)
    {
        if (File.Exists(modelPath))
        {
            using var fileStream = File.OpenRead(modelPath);
            _isolationForestModel = _mlContext.Model.Load(fileStream, out _);
        }
        else
        {
            // 首次训练
            var historicalExpenses = _db.Expenses.ToList();
            var historicalData = historicalExpenses.Select(MapExpenseToData).ToList();
            _isolationForestModel = TrainIsolationForestModel(historicalData);
            // 持久化保存
            var dataView = _mlContext.Data.LoadFromEnumerable(historicalData);
            _mlContext.Model.Save(_isolationForestModel, dataView.Schema, modelPath);
        }
    }
    // 辅助方法: 使用 Z-Score 统计方法检测异常（单条）
    private bool IsOutlierByZScoreSingle(List<ExpenseData> historicalData, ExpenseData newEntry, double threshold)
    {
        if (historicalData.Count == 0) return false;
        var mean = historicalData.Average(e => e.Total);
        var stddev = Math.Sqrt(historicalData.Sum(e => Math.Pow(e.Total - mean, 2)) / historicalData.Count);
        if (stddev == 0) return false;
        return Math.Abs((newEntry.Total - mean) / stddev) > threshold;
    }

    // 辅助方法: 使用 Z-Score 统计方法检测异常（批量）
    private List<ExpenseData> DetectOutliersByZScore(
    List<ExpenseData> historicalData,
    List<ExpenseData> toDetect,
    double threshold)
    {
        if (historicalData == null || historicalData.Count == 0) return new List<ExpenseData>();
        var mean = historicalData.Average(e => e.Total);
        var stddev = Math.Sqrt(historicalData.Sum(e => Math.Pow(e.Total - mean, 2)) / historicalData.Count);
        if (stddev == 0) return new List<ExpenseData>();
        return toDetect.Where(e => Math.Abs((e.Total - mean) / stddev) > threshold).ToList();
    }

    // 辅助方法: IsolationForest模型检测异常（单条/批量）
    private IEnumerable<(ExpenseData Data, bool IsOutlier)> PredictIsolationForest(
        ITransformer model, IList<ExpenseData> toDetect)
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(toDetect);
        var transformed = model.Transform(dataView);
        var predictions = _mlContext.Data.CreateEnumerable<AnomalyPrediction>(transformed, reuseRowObject: false).ToList();
        return toDetect.Select((data, idx) => (data, predictions[idx].PredictedLabel == true));
    }

    // 训练 Isolation Forest 模型
    private ITransformer TrainIsolationForestModel(IEnumerable<ExpenseData> trainingData)
    {
    //     foreach (var d in trainingData)
    // {
    //     Console.WriteLine($"[训练] Type={d.Type}, Payee={d.Payee}, Total={d.Total}");
    // }

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
        var pipeline = _mlContext.Transforms.Text.FeaturizeText("PayeeFeaturized", nameof(ExpenseData.Payee))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("TypeFeaturized", nameof(ExpenseData.Type)))
                        .Append(_mlContext.Transforms.Conversion.ConvertType(
                            new[] { new InputOutputColumnPair("TotalSingle", nameof(ExpenseData.Total)) }, DataKind.Single))
                        .Append(_mlContext.Transforms.Concatenate("Features", "TotalSingle", "PayeeFeaturized", "TypeFeaturized"))
                        .Append(_mlContext.AnomalyDetection.Trainers.RandomizedPca());

        
        return pipeline.Fit(dataView);
    }

    public void RetrainIsolationForestModel()
    {
        var historicalExpenses = _db.Expenses.ToList();
        var historicalData = historicalExpenses.Select(MapExpenseToData).ToList();
        _isolationForestModel = TrainIsolationForestModel(historicalData);
        var dataView = _mlContext.Data.LoadFromEnumerable(historicalData);
        _mlContext.Model.Save(_isolationForestModel, dataView.Schema, ISF_MODEL_PATH);
    }

    // 获取历史数据的 Isolation Forest 异常分数
    private List<double> GetHistoricalIsolationScores(ITransformer model, IEnumerable<ExpenseData> data)
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(data);
//         foreach (var col in dataView.Schema)
// {
//     Console.WriteLine($"Predict Schema: {col.Name} - {col.Type}");
// }
        var transformed = model.Transform(dataView);
//         foreach (var pred in _mlContext.Data.CreateEnumerable<AnomalyPrediction>(transformed, false))
// {
//     Console.WriteLine($"Raw Score: {pred.Score} | PredictedLabel: {pred.PredictedLabel}");
// }
        return _mlContext.Data.CreateEnumerable<AnomalyPrediction>(transformed, reuseRowObject: false)
            .Select(pred => (double)pred.Score)    //Pred.Score本身就是double?
            .Where(s => !double.IsNaN(s) && !double.IsInfinity(s))
            .ToList();
    }

    // 归一化异常分数
    private static double NormalizeScore(double raw, double min, double max)
    {
        if (max == min) return 0;
        var norm = (raw - min) / (max - min);
        if (norm < 0) return 0;
        if (norm > 1) return 1;
        return norm;
    }

    // 检测单条数据是否异常
    public async Task<AnomalyResult> DetectSingleEntryAnomalyAsync(ExpenseData newEntry, double threshold)
    {
        // 从数据库获取历史记录。
        var historicalExpenses = await _db.Expenses.ToListAsync();
        var historicalData = historicalExpenses.Select(MapExpenseToData).ToList();

        // 阶段 1: 使用 Z-Score 检测
        // if (IsOutlierByZScoreSingle(historicalData, newEntry, threshold))
        //     return true;
        double zScore = 0;
        bool zAnomaly = false;
        string risk = "正常范围";
        if (historicalData.Count > 0)
        {
            var mean = historicalData.Average(e => e.Total);
            var stddev = Math.Sqrt(historicalData.Sum(e => Math.Pow(e.Total - mean, 2)) / historicalData.Count);
            if (stddev != 0)
            {
                zScore = Math.Abs((newEntry.Total - mean) / stddev);
                zAnomaly = zScore > threshold;
            }
            if (zScore > threshold)
                risk = "高风险";
            else if (zScore > threshold - 1)
                risk = "中风险";
            else
                risk = "正常范围";
        }
        if (zAnomaly)
        {
            return new AnomalyResult
            {
                IsAnomaly = true,
                Score = zScore,
                Method = "ZScore",
                RiskLevel = risk
            };
        }

        // 阶段 2: 使用 Isolation Forest 检测
        // if (historicalData.Count < 5) // 防止少量数据报错
        //     return false;
        // if (historicalData.Count < 5)
        //     return new AnomalyResult { IsAnomaly = false, Score = zScore, Method = "ZScore", RiskLevel = "正常范围" };

        // var model = TrainIsolationForestModel(historicalData);
        var model = _isolationForestModel;
        if (model == null || historicalData.Count < 5)
            return new AnomalyResult { IsAnomaly = false, Score = zScore, Method = "ZScore", RiskLevel = "正常范围" };

        // 得到历史数据下所有分数（归一化分数用以风险评估）
        var historicalScores = GetHistoricalIsolationScores(model, historicalData);
        if (historicalScores.Count == 0)
        {
            return new AnomalyResult { IsAnomaly = zAnomaly, Score = zScore, Method = "ZScore", RiskLevel = risk };
        }

        double minScore = historicalScores.Min();
        double maxScore = historicalScores.Max();

        var dataView = _mlContext.Data.LoadFromEnumerable(new List<ExpenseData> { newEntry });
        var transformed = model.Transform(dataView);
        var prediction = _mlContext.Data.CreateEnumerable<AnomalyPrediction>(transformed, reuseRowObject: false)
            .FirstOrDefault();

        if (prediction == null)
        {
            return new AnomalyResult { IsAnomaly = zAnomaly, Score = zScore, Method = "ZScore", RiskLevel = risk };
        }

        double? rawScore = prediction.Score;

        // double? score = (double)prediction.Score;
        // if (score.HasValue && (double.IsNaN(score.Value) || double.IsInfinity(score.Value)))
        // {
        //     score = null;
        // }
        double? normScore = null;
        if (rawScore.HasValue && !double.IsNaN(rawScore.Value) && !double.IsInfinity(rawScore.Value))
        {
            normScore = NormalizeScore(rawScore.Value, minScore, maxScore);
        }

        risk = "";
        if (!normScore.HasValue)
            risk = "分数不可用";
        else if (normScore.Value >= 0.7)
            risk = "高风险";
        else if (normScore.Value >= 0.4)
            risk = "中风险";
        else if (normScore.Value >= 0.3)
            risk = "低风险";
        else
            risk = "正常范围";

        // 通常 prediction.Score 大于某个阈值即异常
        return new AnomalyResult
        {
            IsAnomaly = prediction.PredictedLabel,
            Score = normScore,
            Method = "IsolationForest",
            RiskLevel = risk
        };
    }

    // 批量检测：通过两阶段检测 Z-Score(统计方法) 和 Isolation Forest(机器学习算法) 筛选异常账单。
    public async Task<List<ExpenseData>> GetAmountOutliersAsync(List<ExpenseData> expenses, double threshold)
    {
        // 从数据库获取历史记录。
        var historicalExpenses = await _db.Expenses.ToListAsync();
        var historicalData = historicalExpenses.Select(MapExpenseToData).ToList();

        // 阶段 1：使用 Z-Score 检测初步异常账单。
        var zScoreOutliers = DetectOutliersByZScore(historicalData, expenses, threshold);
        var normalData = expenses.Except(zScoreOutliers).ToList(); // 剔除异常账单后剩下的正常账单。

        // 阶段 2：使用 Isolation Forest 模型进一步筛选异常账单。
        if (historicalData.Count < 5)   // 特征化后最好至少10条，可根据实际设警告或降级用z-score
            return zScoreOutliers;

        // var model = TrainIsolationForestModel(historicalData);
        var model = _isolationForestModel;
        var preds = PredictIsolationForest(model, normalData);
        var mlOutliers = preds.Where(x => x.IsOutlier).Select(x => x.Data);

        // Console.WriteLine($"zScoreOutliers count: {zScoreOutliers.Count}");
        // Console.WriteLine($"normalData count: {normalData.Count}");
        // Console.WriteLine($"historicalData count: {historicalData.Count}");
        // Console.WriteLine($"mlOutliers count: {mlOutliers.Count()}");

        // 合并两阶段的异常检测结果。
        return zScoreOutliers.Union(mlOutliers).Distinct().ToList();
    }

    // 按 Payee 分组检测异常(适用于分析特定收款人的账单是否集中出现异常)
    public async Task<Dictionary<string, List<ExpenseData>>> GetGroupOutliersByPayeeAsync(
        List<ExpenseData> expenses, double threshold)
    {
        var groupedOutliers = new Dictionary<string, List<ExpenseData>>();
        foreach (var group in expenses.GroupBy(e => e.Payee))
        {
            var outliers = await GetAmountOutliersAsync(group.ToList(), threshold);
            if (outliers.Any())
                groupedOutliers[group.Key ?? "Unknown"] = outliers;
        }
        return groupedOutliers;
    }

    //辅助方法: 将数据库实体 Expense 映射为服务使用的 ExpenseData。
    private static ExpenseData MapExpenseToData(Expense e) => new ExpenseData
    {
        Date = e.Date,
        Type = e.Type,
        Payee = e.Payee,
        Category = e.Category,
        Total = e.Total
    };

//     private static ExpenseData MapExpenseToData(Expense e)
// {
//     var mapped = new ExpenseData
//     {
//         Date = e.Date,
//         Type = e.Type,
//         Payee = e.Payee,
//         Category = e.Category,
//         Total = e.Total
//     };
//     // ★ 在这里加
//     Console.WriteLine($"[映射] Type={mapped.Type}, Payee={mapped.Payee}, Total={mapped.Total}");
//     return mapped;
// }
}