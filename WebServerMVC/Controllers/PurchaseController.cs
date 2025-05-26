using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Controllers
{
    [ApiController]
    [Route("api/purchase")]
    public class PurchaseController : ControllerBase
    {
        private readonly IInAppPurchaseService _purchaseService;
        private readonly IChatClientService _clientService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<PurchaseController> _logger;

        public PurchaseController(
            IInAppPurchaseService purchaseService,
            IChatClientService clientService,
            IHubContext<ChatHub> hubContext,
            ILogger<PurchaseController> logger)
        {
            _purchaseService = purchaseService;
            _clientService = clientService;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// 구글 플레이 인앱결제 검증
        /// </summary>
        [HttpPost("verify/google")]
        public async Task<IActionResult> VerifyGooglePurchase([FromBody] GooglePurchaseVerificationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "잘못된 요청 데이터입니다."
                });
            }

            try
            {
                var result = await _purchaseService.VerifyGooglePurchase(request);

                // 성공시 클라이언트에게 실시간 알림
                if (result.Success && result.PointsAwarded.HasValue)
                {
                    var client = await _clientService.GetClientById(request.ClientId);
                    if (client != null && !string.IsNullOrEmpty(client.ConnectionId))
                    {
                        await _hubContext.Clients.Client(client.ConnectionId).SendAsync("PointsUpdated", new
                        {
                            points = client.Points,
                            preferenceActiveUntil = client.PreferenceActiveUntil,
                            purchaseInfo = new
                            {
                                pointsAdded = result.PointsAwarded,
                                purchaseId = result.PurchaseId
                            }
                        });
                    }
                }

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Google purchase verification error for client {request.ClientId}");
                return StatusCode(500, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 원스토어 인앱결제 검증
        /// </summary>
        [HttpPost("verify/onestore")]
        public async Task<IActionResult> VerifyOneStorePurchase([FromBody] OneStorePurchaseVerificationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "잘못된 요청 데이터입니다."
                });
            }

            try
            {
                var result = await _purchaseService.VerifyOneStorePurchase(request);

                // 성공시 클라이언트에게 실시간 알림
                if (result.Success && result.PointsAwarded.HasValue)
                {
                    var client = await _clientService.GetClientById(request.ClientId);
                    if (client != null && !string.IsNullOrEmpty(client.ConnectionId))
                    {
                        await _hubContext.Clients.Client(client.ConnectionId).SendAsync("PointsUpdated", new
                        {
                            points = client.Points,
                            preferenceActiveUntil = client.PreferenceActiveUntil,
                            purchaseInfo = new
                            {
                                pointsAdded = result.PointsAwarded,
                                purchaseId = result.PurchaseId
                            }
                        });
                    }
                }

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"OneStore purchase verification error for client {request.ClientId}");
                return StatusCode(500, new PurchaseVerificationResponse
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 사용 가능한 상품 목록 조회
        /// </summary>
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await _purchaseService.GetAvailableProducts();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products");
                return StatusCode(500, new { message = "상품 정보를 가져오는데 실패했습니다." });
            }
        }

        /// <summary>
        /// 클라이언트 구매 내역 조회
        /// </summary>
        [HttpGet("client/{clientId}/purchases")]
        public async Task<IActionResult> GetClientPurchases(string clientId)
        {
            try
            {
                var purchases = await _purchaseService.GetPurchasesByClientId(clientId);
                return Ok(purchases.Select(p => new
                {
                    p.Id,
                    p.Store,
                    p.ProductId,
                    p.Points,
                    p.Amount,
                    p.Currency,
                    p.Status,
                    p.PurchasedAt,
                    p.VerifiedAt
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting purchases for client {clientId}");
                return StatusCode(500, new { message = "구매 내역을 가져오는데 실패했습니다." });
            }
        }

        /// <summary>
        /// 구매 상태 조회
        /// </summary>
        [HttpGet("status/{purchaseId}")]
        public async Task<IActionResult> GetPurchaseStatus(string purchaseId)
        {
            try
            {
                var purchase = await _purchaseService.GetPurchaseById(purchaseId);
                if (purchase == null)
                {
                    return NotFound(new { message = "구매 정보를 찾을 수 없습니다." });
                }

                return Ok(new
                {
                    purchase.Id,
                    purchase.Status,
                    purchase.Points,
                    purchase.PurchasedAt,
                    purchase.VerifiedAt,
                    purchase.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting purchase status {purchaseId}");
                return StatusCode(500, new { message = "구매 상태를 가져오는데 실패했습니다." });
            }
        }

        /// <summary>
        /// 구매 소비 처리 (소모품인 경우)
        /// </summary>
        [HttpPost("consume/{purchaseId}")]
        public async Task<IActionResult> ConsumePurchase(string purchaseId)
        {
            try
            {
                var success = await _purchaseService.ConsumePurchase(purchaseId);
                if (success)
                {
                    return Ok(new { message = "구매가 소비 처리되었습니다." });
                }
                else
                {
                    return BadRequest(new { message = "소비 처리할 수 없는 구매입니다." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error consuming purchase {purchaseId}");
                return StatusCode(500, new { message = "구매 소비 처리에 실패했습니다." });
            }
        }
    }
}