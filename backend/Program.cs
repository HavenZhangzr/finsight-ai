using Microsoft.EntityFrameworkCore;
using QB_SortifyDetectAI.Extensions; // 引用你的扩展类所在命名空间

var builder = WebApplication.CreateBuilder(args);

// 添加Swagger服务
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// 添加EF Core和SQLite支持
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=quickbooks.db"));


// 3. 用扩展方法注册分类服务和异常检测服务
builder.Services.AddExpenseServices();

var app = builder.Build();

// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     DatabaseSeeder.Seed(db);
// }

// Swagger中间件
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();