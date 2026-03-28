using Microsoft.EntityFrameworkCore;
using QB_SortifyDetectAI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "quickbooks.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddExpenseServices();
builder.Services.AddHttpClient<IAiAssistantService, AiAssistantService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
