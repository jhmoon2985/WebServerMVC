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
                _logger.LogInformation($"Starting OneStore purchase verification for product: {productId}");

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

                _logger.LogDebug($"OneStore verification request to: {verifyUrl}");

                // 5️⃣ API 호출 (재시도 로직 포함)
                OneStorePurchaseResponse? purchaseResponse = null;
                var maxRetries = 3;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var response = await _httpClient.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            purchaseResponse = JsonSerializer.Deserialize<OneStorePurchaseResponse>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            _logger.LogInformation($"OneStore purchase verified successfully: {productId}, PurchaseState: {purchaseResponse?.PurchaseState}");
                            break;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning($"OneStore verification failed (attempt {attempt}/{maxRetries}): {response.StatusCode}, {errorContent}");

                            // 토큰 만료 등의 경우 캐시 무효화
                            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            {
                                await InvalidateTokenCache();
                            }

                            if (attempt == maxRetries)
                            {
                                return null;
                            }

                            // 재시도 전 대기
                            await Task.Delay(1000 * attempt);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, $"OneStore API request failed (attempt {attempt}/{maxRetries})");
                        if (attempt == maxRetries) throw;
                        await Task.Delay(1000 * attempt);
                    }
                }

                return purchaseResponse;
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
                    return JsonSerializer.Deserialize<OneStorePurchaseResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"OneStore purchase status check failed: {response.StatusCode}, {errorContent}");
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
                    _logger.LogError("ONE Store ClientId or ClientSecret not configured in appsettings.json");
                    return null;
                }

                var tokenUrl = GetTokenUrl();

                var requestData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                _logger.LogDebug($"Requesting new ONE Store access token from: {tokenUrl}");

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
                            expiryTime = TimeSpan.FromSeconds(Math.Max(expiresIn - 300, 300)); // 최소 5분
                        }

                        await _cache.SetStringAsync(cacheKey, accessToken, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = expiryTime
                        });

                        _logger.LogInformation($"ONE Store access token acquired and cached for {expiryTime.TotalMinutes:F1} minutes");
                        return accessToken;
                    }
                    else
                    {
                        _logger.LogError("ONE Store token response does not contain access_token");
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
                var isConnected = !string.IsNullOrEmpty(accessToken);

                _logger.LogInformation($"ONE Store connection check: {(isConnected ? "Connected" : "Disconnected")}");
                return isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ONE Store connection");
                return false;
            }
        }

        /// <summary>
        /// 환경별 토큰 URL 결정
        /// </summary>
        private string GetTokenUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            var url = environment == "sandbox" ? ONESTORE_SANDBOX_TOKEN_URL : ONESTORE_TOKEN_URL;
            _logger.LogDebug($"Using OneStore token URL: {url} (environment: {environment ?? "production"})");
            return url;
        }

        /// <summary>
        /// 환경별 검증 URL 결정
        /// </summary>
        private string GetVerifyUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            var url = environment == "sandbox" ? ONESTORE_SANDBOX_VERIFY_URL : ONESTORE_VERIFY_URL;
            _logger.LogDebug($"Using OneStore verify URL: {url} (environment: {environment ?? "production"})");
            return url;
        }

        /// <summary>
        /// 환경별 API Base URL 결정
        /// </summary>
        private string GetApiBaseUrl()
        {
            var environment = _configuration["OneStore:Environment"]?.ToLower();
            var baseUrl = environment == "sandbox"
                ? "https://sandbox-apis.onestore.co.kr"
                : "https://apis.onestore.co.kr";
            return baseUrl;
        }

        /// <summary>
        /// OneStore 서비스 설정 검증
        /// </summary>
        public bool ValidateConfiguration()
        {
            var clientId = _configuration["OneStore:ClientId"];
            var clientSecret = _configuration["OneStore:ClientSecret"];
            var environment = _configuration["OneStore:Environment"];

            var isValid = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);

            _logger.LogInformation($"OneStore configuration validation: {(isValid ? "Valid" : "Invalid")} " +
                                 $"(Environment: {environment ?? "production"})");

            if (!isValid)
            {
                _logger.LogError("OneStore configuration is missing. Please set OneStore:ClientId and OneStore:ClientSecret in appsettings.json");
            }

            return isValid;
        }

        /// <summary>
        /// 구매 토큰 검증 (캐시 포함)
        /// </summary>
        public async Task<bool> ValidatePurchaseToken(string purchaseToken)
        {
            if (string.IsNullOrEmpty(purchaseToken))
            {
                return false;
            }

            try
            {
                // 캐시에서 검증 결과 확인
                var cacheKey = $"onestore_token_validation_{purchaseToken}";
                var cachedResult = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedResult))
                {
                    return bool.Parse(cachedResult);
                }

                // 토큰 형식 기본 검증 (OneStore 토큰은 Base64 형태)
                var isValidFormat = IsValidTokenFormat(purchaseToken);

                // 캐시에 결과 저장 (5분)
                await _cache.SetStringAsync(cacheKey, isValidFormat.ToString(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

                return isValidFormat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating purchase token: {purchaseToken}");
                return false;
            }
        }

        /// <summary>
        /// 토큰 형식 검증
        /// </summary>
        private bool IsValidTokenFormat(string token)
        {
            try
            {
                // OneStore 토큰은 일반적으로 Base64 인코딩된 문자열
                var tokenBytes = Convert.FromBase64String(token);
                return tokenBytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 구매 내역 조회 (여러 개)
        /// </summary>
        public async Task<List<OneStorePurchaseResponse>> GetPurchaseHistory(string[] purchaseIds)
        {
            var results = new List<OneStorePurchaseResponse>();

            if (purchaseIds == null || purchaseIds.Length == 0)
            {
                return results;
            }

            var tasks = purchaseIds.Select(async purchaseId =>
            {
                try
                {
                    var result = await GetPurchaseStatus(purchaseId);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error getting purchase status for {purchaseId}");
                    return null;
                }
            });

            var responses = await Task.WhenAll(tasks);

            foreach (var response in responses)
            {
                if (response != null)
                {
                    results.Add(response);
                }
            }

            return results;
        }

        /// <summary>
        /// 서비스 상태 정보 조회
        /// </summary>
        public async Task<OneStoreServiceStatus> GetServiceStatus()
        {
            var status = new OneStoreServiceStatus();

            try
            {
                status.IsConfigured = ValidateConfiguration();
                status.IsConnected = await CheckConnection();
                status.Environment = _configuration["OneStore:Environment"] ?? "production";
                status.LastChecked = DateTime.UtcNow;

                if (status.IsConnected)
                {
                    var accessToken = await GetAccessToken();
                    status.HasValidToken = !string.IsNullOrEmpty(accessToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OneStore service status");
                status.ErrorMessage = ex.Message;
            }

            return status;
        }
    }

    /// <summary>
    /// ONE Store 구매 응답 모델 (향상된 버전)
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

        /// <summary>
        /// 구매 상태를 한국어로 표시
        /// </summary>
        public string PurchaseStateText => PurchaseState switch
        {
            0 => "구매완료",
            1 => "취소됨",
            2 => "환불됨",
            _ => "알 수 없음"
        };
    }

    /// <summary>
    /// ONE Store 에러 응답 모델
    /// </summary>
    public class OneStoreErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string ErrorDescription { get; set; } = string.Empty;
        public int ErrorCode { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>
    /// ONE Store 토큰 응답 모델
    /// </summary>
    public class OneStoreTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public string? Scope { get; set; }
    }

    /// <summary>
    /// ONE Store 서비스 상태 모델
    /// </summary>
    public class OneStoreServiceStatus
    {
        public bool IsConfigured { get; set; }
        public bool IsConnected { get; set; }
        public bool HasValidToken { get; set; }
        public string Environment { get; set; } = "production";
        public DateTime LastChecked { get; set; }
        public string? ErrorMessage { get; set; }
    }
}