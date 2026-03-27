using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QB_SortifyDetectAI.Services;
using System.Net.Http.Headers;
using System.Text;

namespace QB_SortifyDetectAI.Controllers
{
    [ApiController]
    [Route("quickbooks")]
    public class QuickBooksController : ControllerBase
    {
        private readonly QuickBooksTokenStore _tokenStore;
        private readonly QuickBooksApiClient _apiClient;
        private readonly IConfiguration _config;

        public QuickBooksController(IConfiguration config)
        {
            _config = config;
            _tokenStore = new QuickBooksTokenStore();
            _tokenStore.Load(config);
            _apiClient = new QuickBooksApiClient(
                _tokenStore,
                config["QuickBooks:ClientId"]!,
                config["QuickBooks:ClientSecret"]!,
                config["QuickBooks:RedirectUrl"]!
            );
        }

        // QuickBooks 授权回调
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state, string realmId)
        {
            var clientId = _config["QuickBooks:ClientId"]!;
            var clientSecret = _config["QuickBooks:ClientSecret"]!;
            var redirectUri = _config["QuickBooks:RedirectUrl"]!;

            using var httpClient = new HttpClient();
            var tokenUrl = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
            var credentials = $"{clientId}:{clientSecret}";
            var credentialsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentialsBase64);

            var data = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri }
            };

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(data));
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenJson = System.Text.Json.JsonDocument.Parse(result).RootElement;
                _tokenStore.AccessToken = tokenJson.GetProperty("access_token").GetString()!;
                _tokenStore.RefreshToken = tokenJson.GetProperty("refresh_token").GetString()!;
                _tokenStore.RealmId = realmId ?? _config["QuickBooks:RealmId"]!;
                _tokenStore.Save();
            }

            return Content(result, "application/json");
        }

        // 示例：调用 QuickBooks API 并自动刷新token
        [HttpGet("api-demo")]
        public async Task<IActionResult> ApiDemo()
        {
            try
            {
                var result = await _apiClient.CallApiWithAutoRefresh(async http =>
                {
                    var uri = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{_tokenStore.RealmId}/query?query=select * from Customer";
                    var req = new HttpRequestMessage(HttpMethod.Get, uri);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    req.Headers.Add("Accept", "application/json");
                    req.Headers.Add("User-Agent", "QBTesting");
                    req.Headers.Add("Authorization", $"Bearer {_tokenStore.AccessToken}");
                    return await http.SendAsync(req);
                });

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest("API调用失败：" + ex.Message);
            }
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var result = await _apiClient.CallApiWithAutoRefresh(async http =>
                {
                    var uri = $"https://sandbox-quickbooks.api.intuit.com/v3/company/{_tokenStore.RealmId}/query?query=select * from Customer";
                    var req = new HttpRequestMessage(HttpMethod.Get, uri);
                    req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    req.Headers.Add("Accept", "application/json");
                    req.Headers.Add("User-Agent", "QBTesting");
                    return await http.SendAsync(req);
                });

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest("调用QuickBooks API失败: " + ex.Message);
            }
        }
        
    }
}