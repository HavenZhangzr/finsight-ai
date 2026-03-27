using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;

/*
 * 费用分类服务
 * 这个服务负责训练和使用ML.NET模型来分类费用。
 */
public class ExpenseClassificationService : IExpenseClassificationService
{
    private readonly MLContext _mlContext;
    private readonly AppDbContext _db;
    private ITransformer? _model;
    private const string MODEL_PATH = "Models/expense_classification_model.zip";
    // private const string DATA_PATH = "Data/expenses.csv";

    public ExpenseClassificationService(AppDbContext db)
    {
        _mlContext = new MLContext();
        _db = db;
        // 启动时自动检测load-or-train
        InitializeModel(MODEL_PATH);
    }

    // 初始化模型：有zip就load，无zip就train
    private void InitializeModel(string modelPath)
    {
        if (File.Exists(modelPath))
        {
            LoadModel(modelPath);
        }
        else
        {
            Train(modelPath);
        }
    }


    // 加载模型
    private void LoadModel(string modelPath)
    {
        using var fileStream = File.OpenRead(modelPath);
        _model = _mlContext.Model.Load(fileStream, out _);
    }

    // 训练模型
    public void Train(string modelPath = MODEL_PATH)
    {
        // // 加载数据: 把CSV文件中的内容，按ExpenseData模型的结构读入DataView对象。
        // IDataView dataView = _mlContext.Data.LoadFromTextFile<ExpenseData>(
        //     path: csvPath,
        //     hasHeader: true,
        //     separatorChar: ','
        // );

        // 从数据库获取数据（全部符合特征和标签的）
        var data = _db.Expenses
            .Where(x => !string.IsNullOrEmpty(x.Category))
            .ToList();
        // 生成IDataView
        IDataView dataView = _mlContext.Data.LoadFromEnumerable(data);

        // 定义流水线
        var pipeline = _mlContext.Transforms.Text.FeaturizeText("PayeeFeaturized", nameof(ExpenseData.Payee)) // 对Payee文本特征化（TF-IDF/向量化，方便机器学习算法理解，机器学习底层都是数学计算，文本（字符串）不能直接训练。）
            .Append(_mlContext.Transforms.Text.FeaturizeText("TypeFeaturized", nameof(ExpenseData.Type)))      // Type文本特征化
            .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeaturized", nameof(ExpenseData.Description))) // Description文本特征化
            .Append(_mlContext.Transforms.Conversion.ConvertType(
                    new[] { new InputOutputColumnPair("TotalSingle", nameof(ExpenseData.Total)) }, DataKind.Single)) // 将 Total 转换为单精度浮点数
            .Append(_mlContext.Transforms.Concatenate("Features",
                                                    "TotalSingle",
                                                    "PayeeFeaturized",
                                                    "TypeFeaturized",
                                                    "DescFeaturized")) // 拼接（Concatenate）成全部“Features”（这里用总金额和Payee和Type和Description）
            .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ExpenseData.Category))) // 将Category映射为“数字label”
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features")) // 用多分类算法SdcaMaximumEntropy训练模型（这里用SDCA最大熵）
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel")); // 最后把预测结果再映射回原来的分类名字符串（方便你后续展示）

        // 训练模型: 把流水线应用到数据上，生成训练好的模型。
        _model = pipeline.Fit(dataView);

        // 可选：保存模型（把模型持久化为zip文件，方便下次加载不用重新训练）
        _mlContext.Model.Save(_model, dataView.Schema, modelPath);
    }

    // 预测类别
    /* 注意：PredictionEngine 实例本身：ML.NET 文档明确说明，PredictionEngine 不是线程安全的，不能被多个线程共享/复用（比如定义为类字段，所有请求都用同一个 PredictionEngine）。
    但是这里，每次 predict 都会用 _mlContext.Model.CreatePredictionEngine 新建一个 PredictionEngine 实例，仅本方法局部使用，不和其它请求或线程共享。
    .NET 的每个请求（或每个线程）都只是调用自己这一份 PredictionEngine，互不影响。
    这种临时创建-临时用-立即丢弃的做法完全安全。
    简而言之，如果一个类需要被设计为单例模式（Singleton），那么它的成员变量应该是“无状态的”（只读，不可修改，或每次访问都是独立数据），这样才能保证在多线程/并发环境下线程安全。
    如果类确实要被多线程共享修改成员变量，必须保证线程安全，否则就会出错。设计时能无状态就无状态，或者同步/安全地处理有状态。
    */
    public CategoryScoreResult PredictWithScore(ExpenseData sample)
    {
        // 判断模型是否已训练（如果没训练会抛出错误）
        if (_model == null) throw new InvalidDataException("Model not trained yet.");
        // 创建预测引擎，把单条样本（ExpenseData结构）输入模型，得到预测结果（ExpensePrediction结构）
        var predEngine = _mlContext.Model.CreatePredictionEngine<ExpenseData, ExpensePrediction>(_model);
        // 输出预测类别，返回 ExpensePrediction.Category（预测标签）
        var prediction = predEngine.Predict(sample);
        // 取最大概率
        float maxConfidence = prediction.Score.Max();
        return new CategoryScoreResult
        {
            Category = prediction.Category ?? "Unknown",
            Score = maxConfidence
        };
    }
}