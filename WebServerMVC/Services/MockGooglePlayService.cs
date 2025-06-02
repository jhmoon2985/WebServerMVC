using WebServerMVC.Models;

namespace WebServerMVC.Services
{
    /// <summary>
    /// Google Play API 임시 비활성화용 Mock 서비스
    /// 실제 Google Play API를 호출하지 않고 성공 응답을 반환
    /// </summary>
    public class MockGooglePlayService
    {
        private readonly ILogger<MockGooglePlayService> _logger;

        public MockGooglePlayService(ILogger<MockGooglePlayService> logger)
        {
            _logger = logger;
        }

        public async Task<GooglePurchaseResponse?> VerifyPurchase(string productId, string purchaseToken)
        {
            //_logger.LogInformation($"[MOCK] Google Play verification for product: {productId}, token: {purchaseToken?.Substring(0, Math.Min(10, purchaseToken.Length ?? 0))}...");

            // API 호출 시뮬레이션
            await Task.Delay(200);

            return new GooglePurchaseResponse
            {
                ConsumptionState = 0,
                PurchaseState = 1, // Purchased
                PurchaseTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                AcknowledgmentState = 1,
                OrderId = $"mock_google_order_{Guid.NewGuid().ToString("N")[..12]}"
            };
        }

        public async Task<bool> AcknowledgePurchase(string productId, string purchaseToken)
        {
            _logger.LogInformation($"[MOCK] Google Play acknowledgment for product: {productId}");

            await Task.Delay(100);
            return true;
        }

        public async Task<bool> CheckConnection()
        {
            await Task.Delay(10);
            _logger.LogInformation("[MOCK] Google Play connection check - always returns true");
            return true;
        }
    }
}