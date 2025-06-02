using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace WebServerMVC.Services
{
    public class GooglePlayService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GooglePlayService> _logger;

        // 생성자를 최소한의 의존성만 요구하도록 단순화
        public GooglePlayService(
            IConfiguration configuration,
            ILogger<GooglePlayService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ProductPurchase?> VerifyPurchase(string productId, string purchaseToken)
        {
            try
            {
                // Google Play 설정이 없으면 null 반환 (개발 환경 대응)
                var packageName = _configuration["GooglePlay:PackageName"];
                var serviceAccountKey = _configuration["GooglePlay:ServiceAccountKey"];

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(serviceAccountKey))
                {
                    _logger.LogWarning("Google Play configuration is not set. Skipping verification in development mode.");
                    return new ProductPurchase
                    {
                        PurchaseState = 1, // Purchased
                        OrderId = $"dev_order_{Guid.NewGuid()}",
                        PurchaseTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }

                var service = await GetAndroidPublisherService();
                var request = service.Purchases.Products.Get(packageName, productId, purchaseToken);
                var purchase = await request.ExecuteAsync();

                return purchase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google Play purchase verification failed for product {productId}");

                // 개발 환경에서는 성공으로 처리
                if (_configuration.GetValue<bool>("DetailedErrors", false))
                {
                    return new ProductPurchase
                    {
                        PurchaseState = 1, // Purchased
                        OrderId = $"dev_order_{Guid.NewGuid()}",
                        PurchaseTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }

                return null;
            }
        }

        public async Task<bool> AcknowledgePurchase(string productId, string purchaseToken)
        {
            try
            {
                var packageName = _configuration["GooglePlay:PackageName"];
                var serviceAccountKey = _configuration["GooglePlay:ServiceAccountKey"];

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(serviceAccountKey))
                {
                    _logger.LogWarning("Google Play configuration is not set. Skipping acknowledgment in development mode.");
                    return true;
                }

                var service = await GetAndroidPublisherService();
                var acknowledgeRequest = new ProductPurchasesAcknowledgeRequest();
                var request = service.Purchases.Products.Acknowledge(acknowledgeRequest, packageName, productId, purchaseToken);

                await request.ExecuteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google Play purchase acknowledgment failed for product {productId}");

                // 개발 환경에서는 성공으로 처리
                return _configuration.GetValue<bool>("DetailedErrors", false);
            }
        }

        private async Task<AndroidPublisherService> GetAndroidPublisherService()
        {
            var serviceAccountKeyPath = _configuration["GooglePlay:ServiceAccountKey"];

            if (string.IsNullOrEmpty(serviceAccountKeyPath))
            {
                throw new InvalidOperationException("Google Play Service Account Key path is not configured");
            }

            if (!File.Exists(serviceAccountKeyPath))
            {
                throw new FileNotFoundException($"Service Account Key file not found: {serviceAccountKeyPath}");
            }

            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountKeyPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            }

            return new AndroidPublisherService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WebServerMVC"
            });
        }

        public async Task<bool> CheckConnection()
        {
            try
            {
                var packageName = _configuration["GooglePlay:PackageName"];
                var serviceAccountKey = _configuration["GooglePlay:ServiceAccountKey"];

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(serviceAccountKey))
                {
                    return false;
                }

                var service = await GetAndroidPublisherService();
                return service != null;
            }
            catch
            {
                return false;
            }
        }
    }
}