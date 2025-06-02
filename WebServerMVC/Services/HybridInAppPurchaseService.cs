using Microsoft.Extensions.Caching.Distributed;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Services
{
    public class HybridInAppPurchaseService : IInAppPurchaseService
    {
        private readonly IInAppPurchaseRepository _purchaseRepository;
        private readonly IChatClientService _clientService;
        private readonly IDistributedCache _cache;
        private readonly MockGooglePlayService _mockGooglePlayService;
        private readonly OneStoreService _oneStoreService;
        private readonly ILogger<HybridInAppPurchaseService> _logger;

        // 메모리 캐시된 상품 정보
        private static readonly List<ProductInfo> _products = new()
        {
            new ProductInfo { ProductId = "points_1000", Name = "1,000 포인트", Points = 1000, Price = 1100 },
            new ProductInfo { ProductId = "points_5000", Name = "5,000 포인트", Points = 5000, Price = 5500 },
            new ProductInfo { ProductId = "points_10000", Name = "10,000 포인트", Points = 10000, Price = 11000 },
            new ProductInfo { ProductId = "points_50000", Name = "50,000 포인트", Points = 50000, Price = 55000 },
            new ProductInfo { ProductId = "points_100000", Name = "100,000 포인트", Points = 100000, Price = 110000 }
        };

        public HybridInAppPurchaseService(
            IInAppPurchaseRepository purchaseRepository,
            IChatClientService clientService,
            IDistributedCache cache,
            MockGooglePlayService mockGooglePlayService,
            OneStoreService oneStoreService,
            ILogger<HybridInAppPurchaseService> logger)
        {
            _purchaseRepository = purchaseRepository;
            _clientService = clientService;
            _cache = cache;
            _mockGooglePlayService = mockGooglePlayService;
            _oneStoreService = oneStoreService;
            _logger = logger;
        }

        public async Task<PurchaseVerificationResponse> VerifyGooglePurchase(GooglePurchaseVerificationRequest request)
        {
            try
            {
                _logger.LogInformation($"Processing Google purchase verification (MOCK MODE) for client: {request.ClientId}, product: {request.ProductId}");

                // 1. 기본 검증
                var basicValidation = await ValidateBasicPurchaseRequest(request.ClientId, request.ProductId, request.PurchaseToken);
                if (!basicValidation.IsValid)
                {
                    return basicValidation.Response;
                }

                var product = basicValidation.Product;

                // 2. Mock Google Play로 검증 (실제 API 호출 없음)
                var googlePurchase = await _mockGooglePlayService.VerifyPurchase(request.ProductId, request.PurchaseToken);
                if (googlePurchase == null || googlePurchase.PurchaseState != 1)
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
                    System.Text.Json.JsonSerializer.Serialize(googlePurchase));

                // 4. Mock 구매 확인 처리
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mockGooglePlayService.AcknowledgePurchase(request.ProductId, request.PurchaseToken);
                        _logger.LogInformation($"Google purchase acknowledged (MOCK): {purchase.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to acknowledge Google purchase (MOCK): {purchase.Id}");
                    }
                });

                _logger.LogInformation($"Google purchase verified (MOCK): {purchase.Id}, Client: {request.ClientId}, Points: {product.Points}");

                return new PurchaseVerificationResponse
                {
                    Success = true,
                    Message = "구매가 성공적으로 검증되었습니다. (개발 모드)",
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
                _logger.LogInformation($"Processing OneStore purchase verification (REAL API) for client: {request.ClientId}, product: {request.ProductId}");

                // 1. 기본 검증
                var basicValidation = await ValidateBasicPurchaseRequest(request.ClientId, request.ProductId, request.PurchaseToken);
                if (!basicValidation.IsValid)
                {
                    return basicValidation.Response;
                }

                var product = basicValidation.Product;

                // 2. 실제 OneStore API로 검증
                var oneStoreResponse = await _oneStoreService.VerifyPurchase(request.ProductId, request.PurchaseToken);
                if (oneStoreResponse == null || !oneStoreResponse.IsValidPurchase)
                {
                    _logger.LogWarning($"OneStore verification failed for product: {request.ProductId}, client: {request.ClientId}");
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
                    System.Text.Json.JsonSerializer.Serialize(oneStoreResponse));

                _logger.LogInformation($"OneStore purchase verified successfully: {purchase.Id}, Client: {request.ClientId}, Points: {product.Points}");

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
            // 클라이언트 존재 확인
            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return (false, null, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "유효하지 않은 클라이언트입니다."
                });
            }

            // 중복 검증 방지 (캐시 활용)
            var cacheKey = $"purchase_token_{purchaseToken}";
            var cachedResult = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedResult))
            {
                return (false, null, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "이미 처리된 구매입니다."
                });
            }

            // DB에서도 중복 확인
            var existingPurchase = await _purchaseRepository.GetByPurchaseToken(purchaseToken);
            if (existingPurchase != null && existingPurchase.Status == PurchaseStatus.Verified)
            {
                // 캐시에 저장 (1시간)
                await _cache.SetStringAsync(cacheKey, "processed", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

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

            // 처리 완료를 캐시에 기록 (중복 방지용)
            var cacheKey = $"purchase_token_{purchaseToken}";
            await _cache.SetStringAsync(cacheKey, "processed", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

            _logger.LogInformation($"Purchase processed successfully: Store={store}, Client={clientId}, Points={product.Points}, PurchaseId={purchase.Id}");

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

                _logger.LogInformation($"Purchase consumed: {purchaseId}");
                return true;
            }
            return false;
        }

        public async Task<List<ProductInfo>> GetAvailableProducts()
        {
            return await Task.FromResult(_products.Where(p => p.IsActive).ToList());
        }

        /// <summary>
        /// 구매 통계 조회 (관리자용)
        /// </summary>
        public async Task<object> GetPurchaseStats()
        {
            try
            {
                var allPurchases = await _purchaseRepository.GetRecentPurchases(1000);

                return new
                {
                    TotalPurchases = allPurchases.Count,
                    TotalAmount = allPurchases.Sum(p => p.Amount),
                    TotalPoints = allPurchases.Sum(p => p.Points),
                    GooglePurchases = allPurchases.Count(p => p.Store == "google"),
                    OneStorePurchases = allPurchases.Count(p => p.Store == "onestore"),
                    RecentPurchases = allPurchases.Take(10).Select(p => new
                    {
                        p.Id,
                        p.ClientId,
                        p.Store,
                        p.ProductId,
                        p.Points,
                        p.Amount,
                        p.Status,
                        p.PurchasedAt
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase stats");
                return new { Error = "통계 조회 중 오류가 발생했습니다." };
            }
        }
    }
}