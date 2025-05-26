using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Services
{
    public class OptimizedInAppPurchaseService : IInAppPurchaseService
    {
        private readonly IInAppPurchaseRepository _purchaseRepository;
        private readonly IChatClientService _clientService;
        private readonly IDistributedCache _cache;
        private readonly GooglePlayService _googlePlayService;
        private readonly OneStoreService _oneStoreService; // 🆕 ONE Store 서비스 추가
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OptimizedInAppPurchaseService> _logger;

        // 메모리 캐시된 상품 정보
        private static readonly List<ProductInfo> _products = new()
        {
            new ProductInfo { ProductId = "points_1000", Name = "1,000 포인트", Points = 1000, Price = 1100 },
            new ProductInfo { ProductId = "points_5000", Name = "5,000 포인트", Points = 5000, Price = 5500 },
            new ProductInfo { ProductId = "points_10000", Name = "10,000 포인트", Points = 10000, Price = 11000 },
            new ProductInfo { ProductId = "points_50000", Name = "50,000 포인트", Points = 50000, Price = 55000 }
        };

        public OptimizedInAppPurchaseService(
            IInAppPurchaseRepository purchaseRepository,
            IChatClientService clientService,
            IDistributedCache cache,
            GooglePlayService googlePlayService,
            OneStoreService oneStoreService, // 🆕 ONE Store 서비스 주입
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OptimizedInAppPurchaseService> logger)
        {
            _purchaseRepository = purchaseRepository;
            _clientService = clientService;
            _cache = cache;
            _googlePlayService = googlePlayService;
            _oneStoreService = oneStoreService; // 🆕
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<PurchaseVerificationResponse> VerifyGooglePurchase(GooglePurchaseVerificationRequest request)
        {
            try
            {
                // 1. 기본 검증
                var basicValidation = await ValidateBasicPurchaseRequest(request.ClientId, request.ProductId, request.PurchaseToken);
                if (!basicValidation.IsValid)
                {
                    return basicValidation.Response;
                }

                var product = basicValidation.Product;

                // 2. Google Play Console API로 검증
                var googlePurchase = await _googlePlayService.VerifyPurchase(request.ProductId, request.PurchaseToken);
                if (googlePurchase == null || googlePurchase.PurchaseState != 1) // 1 = Purchased
                {
                    return new PurchaseVerificationResponse
                    {
                        Success = false,
                        Message = "구글 플레이 검증에 실패했습니다."
                    };
                }

                // 3. 구매 기록 저장 및 포인트 지급
                var purchase = await ProcessSuccessfulPurchase(
                    request.ClientId, 
                    "google", 
                    request.ProductId, 
                    request.PurchaseToken,
                    googlePurchase.OrderId,
                    product,
                    JsonSerializer.Serialize(googlePurchase));

                // 4. 구매 확인 처리 (Google Play에서 요구)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _googlePlayService.AcknowledgePurchase(request.ProductId, request.PurchaseToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to acknowledge Google purchase: {purchase.Id}");
                    }
                });

                _logger.LogInformation($"Google purchase verified: {purchase.Id}, Client: {request.ClientId}, Points: {product.Points}");

                return new PurchaseVerificationResponse
                {
                    Success = true,
                    Message = "구매가 성공적으로 검증되었습니다.",
                    PointsAwarded = product.Points,
                    PurchaseId = purchase.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google purchase verification failed: {request.ProductId}");
                return new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "구매 검증 중 오류가 발생했습니다."
                };
            }
        }

        public async Task<PurchaseVerificationResponse> VerifyOneStorePurchase(OneStorePurchaseVerificationRequest request)
        {
            try
            {
                // 1. 기본 검증
                var basicValidation = await ValidateBasicPurchaseRequest(request.ClientId, request.ProductId, request.PurchaseToken);
                if (!basicValidation.IsValid)
                {
                    return basicValidation.Response;
                }

                var product = basicValidation.Product;

                // 2. ONE Store API로 검증 (🆕 전용 서비스 사용)
                var oneStoreResponse = await _oneStoreService.VerifyPurchase(request.ProductId, request.PurchaseToken);
                if (oneStoreResponse == null || !oneStoreResponse.IsValidPurchase)
                {
                    return new PurchaseVerificationResponse
                    {
                        Success = false,
                        Message = "원스토어 검증에 실패했습니다."
                    };
                }

                // 3. 구매 기록 저장 및 포인트 지급
                var purchase = await ProcessSuccessfulPurchase(
                    request.ClientId,
                    "onestore",
                    request.ProductId,
                    request.PurchaseToken,
                    oneStoreResponse.PurchaseId,
                    product,
                    JsonSerializer.Serialize(oneStoreResponse));

                _logger.LogInformation($"OneStore purchase verified: {purchase.Id}, Client: {request.ClientId}, Points: {product.Points}");

                return new PurchaseVerificationResponse
                {
                    Success = true,
                    Message = "구매가 성공적으로 검증되었습니다.",
                    PointsAwarded = product.Points,
                    PurchaseId = purchase.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"OneStore purchase verification failed: {request.ProductId}");
                return new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "구매 검증 중 오류가 발생했습니다."
                };
            }
        }

        private async Task<(bool IsValid, ProductInfo Product, PurchaseVerificationResponse Response)> ValidateBasicPurchaseRequest(
            string clientId, string productId, string purchaseToken)
        {
            // 중복 검증 방지
            var existingPurchase = await _purchaseRepository.GetByPurchaseToken(purchaseToken);
            if (existingPurchase != null && existingPurchase.Status == PurchaseStatus.Verified)
            {
                return (false, null, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "이미 처리된 구매입니다."
                });
            }

            // 상품 정보 확인
            var product = _products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null)
            {
                return (false, null, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "유효하지 않은 상품입니다."
                });
            }

            return (true, product, null);
        }

        private async Task<InAppPurchase> ProcessSuccessfulPurchase(
            string clientId, string store, string productId, string purchaseToken,
            string transactionId, ProductInfo product, string verificationData)
        {
            // 구매 기록 저장
            var purchase = new InAppPurchase
            {
                ClientId = clientId,
                Store = store,
                ProductId = productId,
                PurchaseToken = purchaseToken,
                TransactionId = transactionId ?? "",
                Points = product.Points,
                Amount = product.Price,
                Status = PurchaseStatus.Verified,
                VerifiedAt = DateTime.UtcNow,
                VerificationData = verificationData
            };

            await _purchaseRepository.AddPurchase(purchase);

            // 클라이언트에게 포인트 지급
            await _clientService.AddPoints(clientId, product.Points);

            return purchase;
        }

        // IInAppPurchaseService 인터페이스 구현
        public async Task<InAppPurchase?> GetPurchaseById(string purchaseId)
        {
            return await _purchaseRepository.GetPurchaseById(purchaseId);
        }

        public async Task<List<InAppPurchase>> GetPurchasesByClientId(string clientId)
        {
            return await _purchaseRepository.GetPurchasesByClientId(clientId);
        }

        public async Task<bool> ConsumePurchase(string purchaseId)
        {
            var purchase = await _purchaseRepository.GetPurchaseById(purchaseId);
            if (purchase != null && purchase.Status == PurchaseStatus.Verified)
            {
                purchase.Status = PurchaseStatus.Consumed;
                await _purchaseRepository.UpdatePurchase(purchase);
                return true;
            }
            return false;
        }

        public async Task<List<ProductInfo>> GetAvailableProducts()
        {
            return await Task.FromResult(_products.Where(p => p.IsActive).ToList());
        }
    }
}