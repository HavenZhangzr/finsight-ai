/*
 * 费用分类服务接口
 * 负责训练和使用ML.NET模型来分类费用。
 */
public interface IExpenseClassificationService
{
    /// <summary>
    /// 训练模型，根据指定的模型文件路径生成分类模型。
    /// </summary>
    /// <param name="modelPath">模型文件的路径。</param>
    void Train(string modelPath = "Models/expense_classification_model.zip");

    /// <summary>
    /// 预测单条费用数据的类别。
    /// </summary>
    /// <param name="sample">需要预测的费用数据样本。</param>
    /// <returns>预测的分类名称。</returns>
    // string Predict(ExpenseData sample);
    CategoryScoreResult PredictWithScore(ExpenseData sample);
}