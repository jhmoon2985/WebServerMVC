using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Controllers
{
    [Authorize]
    public class PurchaseAdminController : Controller
    {
        private readonly IInAppPurchaseService _purchaseService;
        private readonly IChatClientService _clientService;

        public PurchaseAdminController(
            IInAppPurchaseService purchaseService,
            IChatClientService clientService)
        {
            _purchaseService = purchaseService;
            _clientService = clientService;
        }

        public async Task<IActionResult> Index()
        {
            var recentPurchases = await _purchaseService.GetPurchasesByClientId(""); // 모든 구매 내역
            return View(recentPurchases);
        }

        public async Task<IActionResult> Details(string id)
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

        public async Task<IActionResult> Products()
        {
            var products = await _purchaseService.GetAvailableProducts();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> ManualPointGrant(string clientId, int points, string reason)
        {
            if (string.IsNullOrEmpty(clientId) || points <= 0)
            {
                TempData["Error"] = "유효하지 않은 요청입니다.";
                return RedirectToAction("Index", "Admin");
            }

            try
            {
                var newPoints = await _clientService.AddPoints(clientId, points);
                TempData["Success"] = $"{points} 포인트가 수동으로 지급되었습니다. (사유: {reason})";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"포인트 지급 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", "Admin", new { id = clientId });
        }
    }
}