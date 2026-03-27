// using System;
// using System.Collections.Generic;
// using System.Net.Http;
// using System.Net.Http.Headers;
// using System.Text.Json;
// using System.Threading.Tasks;
// using QB_SortifyDetectAI.Services;

// public class QuickBooksService : IQuickBooksService
// {
//     private readonly HttpClient _httpClient;
//     private readonly QuickBooksTokenStore _tokenStore;
//     private readonly string _baseUrl = "https://sandbox-quickbooks.api.intuit.com";

//     public QuickBooksService(HttpClient httpClient, QuickBooksTokenStore tokenStore)
//     {
//         _httpClient = httpClient;
//         _tokenStore = tokenStore;
//     }

//     public async Task<List<ExpenseData>> GetExpensesAsync()
//     {
//         var url = $"{_baseUrl}/v3/company/{_tokenStore.RealmId}/query?query=SELECT * FROM Purchase";

//         var request = new HttpRequestMessage(HttpMethod.Get, url);
//         // 使用最新token
//         request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
//         request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
//         request.Headers.Add("Environment", "sandbox");

//         var response = await _httpClient.SendAsync(request);

//         // ------------------------------BEGIN 调试------------------------------
//         var responseContent2 = await response.Content.ReadAsStringAsync(); // ⭐️ 获取完整body
//         // 临时日志 输出到控制台或日志文件
//         Console.WriteLine("QuickBooks API Response Content:\n" + responseContent2);

//         if (!response.IsSuccessStatusCode)
//         {
//             // 抛出带 body 的异常，方便调试
//             throw new Exception($"Failed to fetch expenses: {response.StatusCode} - {response.ReasonPhrase}\n{responseContent2}");
//         }
//         // ------------------------------END 调试------------------------------

//         // if (!response.IsSuccessStatusCode)
//         // {
//         //     throw new Exception($"Failed to fetch expenses: {response.StatusCode} - {response.ReasonPhrase}");
//         // }

//         var responseContent = await response.Content.ReadAsStringAsync();

//         // 反序列化为 List<ExpenseData>
//         return JsonSerializer.Deserialize<List<ExpenseData>>(responseContent, new JsonSerializerOptions
//         {
//             PropertyNameCaseInsensitive = true
//         }) ?? new List<ExpenseData>();
//     }
// }