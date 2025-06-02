using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;
using WebServerMVC.Services;

namespace WebServerMVC.Controllers
{
    [Authorize]
    public class PurchaseAdminController : Controller
    {
        private readonly IInAppPurchaseService _purchaseService;
        private readonly IChatClientService _clientService;
        private readonly OneStoreService _oneStoreService;
        private readonly MockGooglePlayService _mockGooglePlayService;
        private readonly ILogger<PurchaseAdminController> _logger;

        public PurchaseAdminController(
            IInAppPurchaseService purchaseService,
            IChatClientService clientService,
            OneStoreService oneStoreService,
            MockGooglePlayService mockGooglePlayService,
            ILogger<PurchaseAdminController> logger)
        {
            _purchaseService = purchaseService;
            _clientService = clientService;
            _oneStoreService = oneStoreService;
            _mockGooglePlayService = mockGooglePlayService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // 최근 구매 내역 조회
                var recentPurchases = await _purchaseService.GetPurchasesByClientId(""); // 모든 구매 내역

                // 통계 정보 생성
                var stats = new PurchaseStatsViewModel
                {
                    TotalPurchases = recentPurchases.Count,
                    TotalAmount = recentPurchases.Sum(p => p.Amount),
                    TotalPoints = recentPurchases.Sum(p => p.Points),
                    GooglePurchases = recentPurchases.Count(p => p.Store == "google"),
                    OneStorePurchases = recentPurchases.Count(p => p.Store == "onestore"),
                    VerifiedPurchases = recentPurchases.Count(p => p.Status == PurchaseStatus.Verified),
                    ConsumedPurchases = recentPurchases.Count(p => p.Status == PurchaseStatus.Consumed),
                    FailedPurchases = recentPurchases.Count(p => p.Status == PurchaseStatus.Failed),
                    TodayPurchases = recentPurchases.Count(p => p.PurchasedAt.Date == DateTime.Today),
                    RecentPurchases = recentPurchases.OrderByDescending(p => p.PurchasedAt).Take(20).ToList()
                };

                // 서비스 연결 상태 확인
                ViewBag.OneStoreConnection = await _oneStoreService.CheckConnection();
                ViewBag.GooglePlayConnection = await _mockGooglePlayService.CheckConnection();
                ViewBag.OneStoreConfigValid = _oneStoreService.ValidateConfiguration();

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase admin index");
                TempData["Error"] = "구매 정보를 불러오는 중 오류가 발생했습니다.";
                return View(new PurchaseStatsViewModel());
            }
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                var purchase = await _purchaseService.GetPurchaseById(id);
                if (purchase == null)
                {
                    return NotFound();
                }

                var client = await _clientService.GetClientById(purchase.ClientId);
                ViewBag.Client = client;

                return View(purchase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading purchase details: {id}");
                TempData["Error"] = "구매 상세 정보를 불러오는 중 오류가 발생했습니다.";
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Products()
        {
            try
            {
                var products = await _purchaseService.GetAvailableProducts();
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                TempData["Error"] = "상품 정보를 불러오는 중 오류가 발생했습니다.";
                return View(new List<ProductInfo>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> ManualPointGrant(string clientId, int points, string reason)
        {
            if (string.IsNullOrEmpty(clientId) || points <= 0)
            {
                TempData["Error"] = "유효하지 않은 요청입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var client = await _clientService.GetClientById(clientId);
                if (client == null)
                {
                    TempData["Error"] = "존재하지 않는 클라이언트입니다.";
                    return RedirectToAction("Index");
                }

                var newPoints = await _clientService.AddPoints(clientId, points);

                _logger.LogInformation($"Manual point grant: Client={clientId}, Points={points}, Reason={reason}, Admin={User.Identity?.Name}");

                TempData["Success"] = $"{points} 포인트가 수동으로 지급되었습니다. (사유: {reason ?? "없음"})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error granting manual points to client {clientId}");
                TempData["Error"] = $"포인트 지급 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ConsumePurchase(string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                TempData["Error"] = "유효하지 않은 구매 ID입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var success = await _purchaseService.ConsumePurchase(purchaseId);
                if (success)
                {
                    _logger.LogInformation($"Purchase consumed by admin: {purchaseId}, Admin={User.Identity?.Name}");
                    TempData["Success"] = "구매가 소비 처리되었습니다.";
                }
                else
                {
                    TempData["Error"] = "소비 처리할 수 없는 구매입니다.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error consuming purchase {purchaseId}");
                TempData["Error"] = $"구매 소비 처리 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = purchaseId });
        }

        /// <summary>
        /// OneStore 연결 테스트
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TestOneStoreConnection()
        {
            try
            {
                var isConnected = await _oneStoreService.CheckConnection();
                var configValid = _oneStoreService.ValidateConfiguration();

                if (isConnected && configValid)
                {
                    TempData["Success"] = "OneStore 연결이 성공적으로 확인되었습니다.";
                }
                else if (!configValid)
                {
                    TempData["Error"] = "OneStore 설정이 올바르지 않습니다. appsettings.json을 확인하세요.";
                }
                else
                {
                    TempData["Error"] = "OneStore 연결에 실패했습니다.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing OneStore connection");
                TempData["Error"] = $"OneStore 연결 테스트 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// OneStore 토큰 캐시 무효화
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> InvalidateOneStoreToken()
        {
            try
            {
                await _oneStoreService.InvalidateTokenCache();
                TempData["Success"] = "OneStore 토큰 캐시가 무효화되었습니다.";
                _logger.LogInformation($"OneStore token cache invalidated by admin: {User.Identity?.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating OneStore token");
                TempData["Error"] = $"토큰 캐시 무효화 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// 구매 통계 API (AJAX용)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPurchaseStats()
        {
            try
            {
                var service = _purchaseService as HybridInAppPurchaseService;
                if (service != null)
                {
                    var stats = await service.GetPurchaseStats();
                    return Json(stats);
                }

                return Json(new { Error = "통계 서비스를 사용할 수 없습니다." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase stats via API");
                return Json(new { Error = "통계 조회 중 오류가 발생했습니다." });
            }
        }

        /// <summary>
        /// 클라이언트별 구매 내역 조회
        /// </summary>
        public async Task<IActionResult> ClientPurchases(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                TempData["Error"] = "클라이언트 ID가 필요합니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var client = await _clientService.GetClientById(clientId);
                if (client == null)
                {
                    TempData["Error"] = "존재하지 않는 클라이언트입니다.";
                    return RedirectToAction("Index");
                }

                var purchases = await _purchaseService.GetPurchasesByClientId(clientId);

                var viewModel = new ClientPurchaseViewModel
                {
                    Client = client,
                    Purchases = purchases.OrderByDescending(p => p.PurchasedAt).ToList(),
                    TotalSpent = purchases.Sum(p => p.Amount),
                    TotalPoints = purchases.Sum(p => p.Points)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading client purchases: {clientId}");
                TempData["Error"] = "클라이언트 구매 내역을 불러오는 중 오류가 발생했습니다.";
                return RedirectToAction("Index");
            }
        }
    }
}