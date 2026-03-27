using System.Net.Http.Headers;
using System.Text;

namespace QB_SortifyDetectAI.Services
{
    public class QuickBooksApiClient
    {
        private readonly QuickBooksTokenStore _tokenStore;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;

        public QuickBooksApiClient(QuickBooksTokenStore tokenStore, string clientId, string clientSecret, string redirectUri)
        {
            _tokenStore = tokenStore;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _redirectUri = redirectUri;
        }

        // 刷新access_token
        public async Task<bool> RefreshTokenAsync()
        {
            using var httpClient = new HttpClient();
            var credentials = $"{_clientId}:{_clientSecret}";
            var credentialsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentialsBase64);

            var data = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", _tokenStore.RefreshToken ?? string.Empty  },
                { "redirect_uri", _redirectUri ?? string.Empty}
            };

            var response = await httpClient.PostAsync("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer", new FormUrlEncodedContent(data));
            if (!response.IsSuccessStatusCode) return false;

            var content = await response.Content.ReadAsStringAsync();
            var tokenJson = System.Text.Json.JsonDocument.Parse(content).RootElement;

            _tokenStore.AccessToken = tokenJson.GetProperty("access_token").GetString()!;
            _tokenStore.RefreshToken = tokenJson.GetProperty("refresh_token").GetString()!;
            _tokenStore.Save();

            return true;
        }

        // 自动刷新的API调用器（高复用）
        public async Task<string> CallApiWithAutoRefresh(Func<HttpClient, Task<HttpResponseMessage>> apiCall)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

            var response = await apiCall(httpClient);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // access_token失效，自动刷新
                var refreshed = await RefreshTokenAsync();
                if (!refreshed) throw new Exception("Token刷新失败，请重新授权");

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
                response = await apiCall(httpClient);
            }
            return await response.Content.ReadAsStringAsync();
        }
    }
}