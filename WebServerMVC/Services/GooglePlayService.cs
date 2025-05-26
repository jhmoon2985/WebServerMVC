using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Distributed;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Services
{
    public class GooglePlayService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GooglePlayService> _logger;
        private readonly IDistributedCache _cache;

        public GooglePlayService(
            IConfiguration configuration,
            ILogger<GooglePlayService> logger,
            IDistributedCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task<ProductPurchase?> VerifyPurchase(string productId, string purchaseToken)
        {
            try
            {
                var service = await GetAndroidPublisherService();
                var packageName = _configuration["GooglePlay:PackageName"];

                var request = service.Purchases.Products.Get(packageName, productId, purchaseToken);
                var purchase = await request.ExecuteAsync();

                return purchase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google Play purchase verification failed for product {productId}");
                return null;
            }
        }

        public async Task<bool> AcknowledgePurchase(string productId, string purchaseToken)
        {
            try
            {
                var service = await GetAndroidPublisherService();
                var packageName = _configuration["GooglePlay:PackageName"];

                var acknowledgeRequest = new ProductPurchasesAcknowledgeRequest();
                var request = service.Purchases.Products.Acknowledge(acknowledgeRequest, packageName, productId, purchaseToken);
                
                await request.ExecuteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google Play purchase acknowledgment failed for product {productId}");
                return false;
            }
        }

        private async Task<AndroidPublisherService> GetAndroidPublisherService()
        {
            var serviceAccountKeyPath = _configuration["GooglePlay:ServiceAccountKey"];
            
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
    }
}
