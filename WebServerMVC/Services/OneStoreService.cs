using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WebServerMVC.Models;

namespace WebServerMVC.Services
{
    public class OneStoreService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OneStoreService> _logger;
        private readonly IDistributedCache _cache;

        // ONE Store API 엔드포인트
        private const string ONESTORE_TOKEN_URL = "https://apis.onestore.co.kr/v2/oauth/token";
        private const string ONESTORE_VERIFY_URL = "https://apis.onestore.co.kr/v2/purchase/verify";
        private const string ONESTORE_SANDBOX_TOKEN_URL = "https://sandbox-apis.onestore.co.kr/v2/oauth/token";
        private const string ONESTORE_SANDBOX_VERIFY_URL = "https://sandbox-apis.onestore.co.kr/v2/purchase/verify";

        public OneStoreService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OneStoreService> logger,
            IDistributedCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// ONE Store 구매 검증
        /// </summary>
        public async Task<OneStorePurchaseResponse?> VerifyPurchase(string productId, string purchaseToken)
        {
            try
            {
                // 1️⃣ Access Token 획득
                var accessToken = await GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to get ONE Store access token");
                    return null;
                }

                // 2️⃣ 검증 요청 데이터 구성
                var requestData = new
                {
                    purchaseToken = purchaseToken,
                    productId = productId
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 3️⃣ API 엔드포인트 결정 (환경별)
                var verifyUrl = GetVerifyUrl();

                // 4️⃣ HTTP 요청 설정
                using var request = new HttpRequestMessage(HttpMethod.Post, verifyUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = content;

                _logger.LogDebug($"ONE Store verification request: {productId}");

                // 5️⃣ API 호출
                using var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var purchaseResponse = JsonSerializer.Deserialize<OneStorePurchaseResponse>(responseContent);
                    
                    _logger.LogInformation($"ONE Store purchase verified successfully: {productId}");
                    return purchaseResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"ONE Store verification failed: {response.StatusCode}, {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying ONE Store purchase: {productId}");
                return null;
            }
        }

        /// <summary>
        /// ONE Store 구매 상태 조회
        /// </summary>
        public async Task<OneStorePurchaseResponse?> GetPurchaseStatus(string purchaseId)
        {
            try
            {
                var accessToken = await GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return null;
                }

                var url = $"{GetApiBaseUrl()}/v2/purchase/status/{purchaseId}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OneStorePurchaseResponse>(responseContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ONE Store purchase status: {purchaseId}");
                return null;
            }
        }

        /// <summary>
        /// ONE Store 구매 취소/환불
        /// </summary>
        public async Task<bool> CancelPurchase(string purchaseId, string reason = "")
        {
            try
            {
                var accessToken = await GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return false;
                }

                var requestData = new
                {
                    purchaseId = purchaseId,
                    reason = reason
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{GetApiBaseUrl()}/v2/purchase/cancel";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = content;

                using var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"ONE Store purchase cancelled: {purchaseId}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"ONE Store cancel failed: {response.StatusCode}, {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling ONE Store purchase: {purchaseId}");
                return false;
            }
        }

        /// <summary>
        /// ONE Store Access Token 획득 (캐시 적용)
        /// </summary>
        public async Task<string?> GetAccessToken()
        {
            const string cacheKey = "onestore_access_token";

            try
            {
                // 1️⃣ 캐시에서 토큰 확인
                var cachedToken = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedToken))
                {
                    _logger.LogDebug("Using cached ONE Store access token");
                    return cachedToken;
                }

                // 2️⃣ 새 토큰 획득
                var clientId = _configuration["OneStore:ClientId"];
                var clientSecret = _configuration["OneStore:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("ONE Store ClientId or ClientSecret not configured");
                    return null;
                }

                var tokenUrl = GetTokenUrl();
                
                var requestData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                _logger.LogDebug("Requesting new ONE Store access token");

                using var response = await _httpClient.PostAsync(tokenUrl, requestData);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    if (tokenResponse.TryGetProperty("access_token", out var accessTokenElement))
                    {
                        var accessToken = accessTokenElement.GetString();
                        
                        // 3️⃣ 토큰 캐시 저장 (55분, 만료 5분 전까지)
                        var expiryTime = TimeSpan.FromMinutes(55);
                        if (tokenResponse.TryGetProperty("expires_in", out var expiresInElement))
                        {
                            var expiresIn = expiresInElement.GetInt32();
                            expiryTime = TimeSpan.FromSeconds(expiresIn - 300); // 5분 여유
                        }

                        await _cache.SetStringAsync(cacheKey, accessToken, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = expiryTime
                        });

                        _logger.LogInformation($"ONE Store access token acquired and cached for {expiryTime.TotalMinutes} minutes");
                        return accessToken;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get ONE Store access token: {response.StatusCode}, {errorContent}");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ONE Store access token");
                return null;
            }
        }

        /// <summary>
        /// ONE Store 토큰 캐시 무효화
        /// </summary>
        public async Task InvalidateTokenCache()
        {
            await _cache.RemoveAsync("onestore_access_token");
            _logger.LogInformation("ONE Store access token cache invalidated");
        }

        /// <summary>
        /// ONE Store 연결 상태 확인
        /// </summary>
        public async Task<bool> CheckConnection()
        {
            try
            {
                var accessToken = await GetAccessToken();
                return !string.IsNullOrEmpty(accessToken);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 환경별 토큰 URL 결정
        /// </summary>
        private string GetTokenUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            return environment == "sandbox" ? ONESTORE_SANDBOX_TOKEN_URL : ONESTORE_TOKEN_URL;
        }

        /// <summary>
        /// 환경별 검증 URL 결정
        /// </summary>
        private string GetVerifyUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            return environment == "sandbox" ? ONESTORE_SANDBOX_VERIFY_URL : ONESTORE_VERIFY_URL;
        }

        /// <summary>
        /// 환경별 API Base URL 결정
        /// </summary>
        private string GetApiBaseUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            return environment == "sandbox" 
                ? "https://sandbox-apis.onestore.co.kr" 
                : "https://apis.onestore.co.kr";
        }
    }

    /// <summary>
    /// ONE Store 구매 응답 모델 (상세버전)
    /// </summary>
    public class OneStorePurchaseResponse
    {
        public string PurchaseId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public int PurchaseState { get; set; } // 0: 구매완료, 1: 취소됨, 2: 환불됨
        public long PurchaseTime { get; set; }
        public string? DeveloperPayload { get; set; }
        public int Quantity { get; set; } = 1;
        public string? OriginalJson { get; set; }
        public string? Signature { get; set; }

        /// <summary>
        /// 구매 상태가 유효한지 확인
        /// </summary>
        public bool IsValidPurchase => PurchaseState == 0;

        /// <summary>
        /// 구매 시간을 DateTime으로 변환
        /// </summary>
        public DateTime PurchaseDateTime => DateTimeOffset.FromUnixTimeMilliseconds(PurchaseTime).DateTime;
    }

    /// <summary>
    /// ONE Store 에러 응답 모델
    /// </summary>
    public class OneStoreErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string ErrorDescription { get; set; } = string.Empty;
        public int ErrorCode { get; set; }
    }
}