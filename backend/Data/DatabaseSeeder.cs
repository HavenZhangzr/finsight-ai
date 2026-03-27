using System.Globalization;
using CsvHelper;

public static class DatabaseSeeder
{
    private class ExpenseCsvModel
    {
        public DateTime Date { get; set; }
        public string? Type { get; set; }
        public string? Payee { get; set; }
        public string? Category { get; set; }
        public float Total { get; set; }
        public string? Description { get; set; } 
    }

    public static void Seed(AppDbContext db)
    {
        if (!db.Expenses.Any())
        {
            using var reader = new StreamReader("Data/ExpensesSeed.csv");
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<ExpenseCsvModel>().ToList();
            db.Expenses.AddRange(records.Select(r => new Expense
            {
                Date = r.Date,
                Type = r.Type ?? "",
                Payee = r.Payee ?? "",
                Category = r.Category ?? "",
                Total = r.Total,
                Description = r.Description ?? ""
            }));
            db.SaveChanges();
        }
    }
}