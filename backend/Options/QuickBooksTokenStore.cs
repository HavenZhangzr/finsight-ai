// namespace QB_SortifyDetectAI.Options
// {
//     public class QuickBooksTokenStore
//     {
//         public string? AccessToken { get; set; }
//         public string? RefreshToken { get; set; }
//         public string? RealmId { get; set; }

//         // 加载和保存方法, 可根据你的实际需求扩展
//         public void Load(Microsoft.Extensions.Configuration.IConfiguration config)
//         {
//             var section = config.GetSection("QuickBooks");
//             AccessToken = section["AccessToken"];
//             RefreshToken = section["RefreshToken"];
//             RealmId = section["RealmId"];
//         }

//         // 可扩展保存方法（例如保存到文件/数据库等）
//         public void Save()
//         {
//             // TODO: 实现保存到安全存储的功能
//         }
//     }
// }