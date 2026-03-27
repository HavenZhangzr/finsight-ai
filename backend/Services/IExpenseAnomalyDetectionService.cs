using System.Collections.Generic;
using System.Threading.Tasks;

/*
 * 异常检测服务接口
 * 负责检测账单中金额的全局异常和分组异常
 */
public interface IExpenseAnomalyDetectionService
{
    /// <summary>
    /// 检测账单中的全局异常值。
    /// </summary>
    /// <param name="expenses">账单列表。</param>
    /// <param name="threshold">阈值，用于过滤异常值。</param>
    /// <returns>异常账单列表。</returns>
    Task<List<ExpenseData>> GetAmountOutliersAsync(List<ExpenseData> expenses, double threshold);

    /// <summary>
    /// 按收款人分组，检测每组中的异常值。
    /// </summary>
    /// <param name="expenses">账单列表。</param>
    /// <param name="threshold">阈值，用于过滤组内的异常值。</param>
    /// <returns>按收款人分组的异常账单。</returns>
    Task<Dictionary<string, List<ExpenseData>>> GetGroupOutliersByPayeeAsync(List<ExpenseData> expenses, double threshold);

    /// <summary>
    /// 检测单条账单数据是否异常。
    /// </summary>
    /// <param name="newEntry">单条账单数据。</param>
    /// <param name="threshold">阈值。</param>
    /// <returns>是否异常（true 表示异常）。</returns>
    Task<AnomalyResult> DetectSingleEntryAnomalyAsync(ExpenseData newEntry, double threshold);

    void RetrainIsolationForestModel();
}