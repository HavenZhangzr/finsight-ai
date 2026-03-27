using Microsoft.Extensions.Configuration;

namespace QB_SortifyDetectAI.Services
{
    // 简单文件持久化的TokenStore。复杂场景可用数据库。
    public class QuickBooksTokenStore
    {
        private readonly string TokenFile = "quickbooks_tokens.json"; // 存储文件

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? RealmId { get; set; }

        public void Load(IConfiguration configuration)
        {
            if (File.Exists(TokenFile))
            {
                var json = File.ReadAllText(TokenFile);
                var obj = System.Text.Json.JsonDocument.Parse(json).RootElement;
                AccessToken = obj.GetProperty("AccessToken").GetString()!;
                RefreshToken = obj.GetProperty("RefreshToken").GetString()!;
                RealmId = obj.GetProperty("RealmId").GetString()!;
            }
            else
            {
                // 配置文件初始状态
                AccessToken = configuration["QuickBooks:AccessToken"]!;
                RefreshToken = configuration["QuickBooks:RefreshToken"]!;
                RealmId = configuration["QuickBooks:RealmId"]!;
            }
        }

        public void Save()
        {
            var obj = new
            {
                AccessToken,
                RefreshToken,
                RealmId
            };
            File.WriteAllText(TokenFile, System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}