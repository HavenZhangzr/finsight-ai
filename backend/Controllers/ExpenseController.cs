using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController] // 标记为WebApi控制器，自动处理参数校验、路由等
[Route("api/[controller]")] // API 路径自动变成 api/expense（类名去除Controller后的小写）
public class ExpenseController : ControllerBase
{
    private readonly IExpenseClassificationService _classifier;
    private readonly IExpenseAnomalyDetectionService _anomalyDetector;
    private readonly AppDbContext _db;

    /* 构造函数，注入ExpenseClassificationService服务
     * 在Program.cs中注册了这个服务, 框架（如 ASP.NET Core）会自动扫描构造函数的参数，
     * 如果发现参数类型是已经注册到服务容器的类型，就会自动创建对象并赋值。
    */
    /* 详细解释
    构造函数注入机制（反射+服务容器）
    当有请求进入，比如Web API被访问，框架要创建ExpenseController实例。
    它会看这个控制器的构造函数参数有哪些（类型是什么）。
    查找服务容器：有没有注册过这种类型？如果找到了（比如ExpenseClassificationService），就自动用约定好的生命周期（SingleTon/Scoped/Transient）创建或复用对象。
    有依赖就递归注入依赖，直到所有依赖都能“拼成一条链”。
    全过程用到了反射和服务容器的查找机制。
    Program.cs注册         控制器声明依赖          框架请求到达
    ↓                        ↓                     ↓
    AddSingleton注册 -----> 构造函数参数 -----> 反射读取需要类型 -----> 容器new对象并传入 -----> 控制器正常工作
    */
    /*
    1. 依赖注入：为什么用反射？
    服务容器（如 .NET Core 的 IServiceProvider）并不知道你的类有多少个构造函数、每个构造函数参数是什么类型，你可能写了很多种 public MyService(...)。
    框架需要自动分析每个构造函数的参数列表，然后依次查容器注册、递归创建，每一步都要用到“类型信息”。
    类型信息的动态读取/分析/实例化，这正是反射做的事情。
    2. 实际过程简述
    查找构造函数
    框架通过反射：type.GetConstructors() 获取类的构造器
    决定用哪个构造器（通常选参数最多且类型都能满足的）
    获取每个参数类型
    依然靠反射：ctor.GetParameters() 得到需要注入的每个依赖的类型
    递归构建依赖
    对每个参数类型，容器继续查找有没有注册；如果有，再次通过反射new出来，直到都能注入
    实例化目标对象
    用 Activator.CreateInstance(type, 参数数组) 或类似反射API实例化对象，并自动把依赖参数传进构造函数
    为什么要递归？
    因为在实际软件开发中，一个类的依赖，往往还依赖其他类。这种依赖链如果多级，必须要用递归才能层层自动构造出整条依赖树。
    */
    public ExpenseController(IExpenseClassificationService classifier, 
                            IExpenseAnomalyDetectionService anomalyDetector,
                            AppDbContext db)
    {
        _classifier = classifier;
        _anomalyDetector = anomalyDetector;
        _db = db;
    }


    [HttpPost("auto-category")] // POST api/expense/auto-category
    // 预测类别的API
    public IActionResult PredictCategory([FromBody] ExpenseData input) // 从请求体里反序列化ExpenseData对象:([FromBody] ExpenseData input) ：框架会自动把用户POST来的账单输入(JSON格式)解析为ExpenseData对象。
    {
        if (input.Total == null)
        {
            input.Total = 0;
        }

        // 调服务的Predict（预测）方法，把输入数据送进已训练的模型，返回预测结果（类别）。
        var category = _classifier.PredictWithScore(input);
        //返回一个JSON对象：{ category: xxx }，即预测的分类结果，HTTP响应码200。
        return Ok(new { category });
    }

    // 训练模型的API（可选，实际项目只需本地调用）
    [HttpPost("train-category")] // POST api/expense/train
    public IActionResult TrainCategoryModel()
    {
        // 调用服务的训练方法，用本地CSV文件重新训练模型并保存
        _classifier.Train();
        return Ok("Category Model trained and saved.");
    }
    // 训练模型的API（可选，实际项目只需本地调用）
    [HttpPost("train-anomaly")] // POST api/expense/train
    public IActionResult TrainAnomalyModel()
    {
        // 调用服务的训练方法，用本地CSV文件重新训练模型并保存
        _anomalyDetector.RetrainIsolationForestModel();
        return Ok("IsolationForest Model trained and saved.");
    }

    // 单条数据异常检测 API
    [HttpPost("detect-single")]
    public async Task<IActionResult> DetectSingleEntryAnomaly([FromBody] ExpenseData input, double threshold = 3.0)
    {
        var result = await _anomalyDetector.DetectSingleEntryAnomalyAsync(input, threshold);
        return Ok(new { result });
    }

    // 全局异常检测 API
    [HttpPost("detect-global-outliers")]
    public async Task<IActionResult> DetectGlobalOutliers([FromBody] List<ExpenseData> expenses, double threshold = 3.0)
    {
        // 调用全局异常检测服务
        var outliers = await _anomalyDetector.GetAmountOutliersAsync(expenses, threshold);
        return Ok(outliers);
    }

    // 按 Payee 分组检测异常 API
    [HttpPost("detect-payee-outliers")]
    public async Task<IActionResult> DetectPayeeOutliers([FromBody] List<ExpenseData> expenses, double threshold = 3.0)
    {
        // 调用按Payee分组异常检测服务
        var groupedOutliers = await _anomalyDetector.GetGroupOutliersByPayeeAsync(expenses, threshold);
        return Ok(groupedOutliers);
    }

    // 查（全部和分页）
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Expense>>> GetExpenses()
    {
        return await _db.Expenses.ToListAsync();
    }

    // 查（单条）
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound();
        return expense;
    }

    // 增（新增一条）
    [HttpPost]
    public async Task<ActionResult<Expense>> CreateExpense(Expense expense)
    {
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, expense);
    }

    // 改（修改一条）
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(int id, Expense expense)
    {
        if (id != expense.Id)
            return BadRequest();

        _db.Entry(expense).State = EntityState.Modified;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_db.Expenses.Any(e => e.Id == id))
                return NotFound();
            throw;
        }
        return NoContent();
    }

    // 删（删除一条）
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound();

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}