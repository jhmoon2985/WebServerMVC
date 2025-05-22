using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;
using System.Collections.Generic;

namespace WebServerMVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly IClientService _clientService;
        private readonly IMatchingService _matchingService;
        private readonly IMessageService _messageService;
        private readonly IImageService _imageService;

        public AdminController(
            IClientService clientService,
            IMatchingService matchingService,
            IMessageService messageService,
            IImageService imageService)
        {
            _clientService = clientService;
            _matchingService = matchingService;
            _messageService = messageService;
            _imageService = imageService;
        }

        public async Task<IActionResult> Index()
        {
            var clients = await _clientService.GetAllClients();
            return View(clients);
        }

        public async Task<IActionResult> Details(string id)
        {
            var client = await _clientService.GetClientById(id);
            if (client == null)
            {
                return NotFound();
            }

            var matchedClient = await _matchingService.GetMatchedClient(id);
            ViewBag.MatchedClient = matchedClient;

            return View(client);
        }

        // 메시지 관리 페이지
        public IActionResult Messages()
        {
            return View();
        }

        // 클라이언트별 메시지 조회
        public async Task<IActionResult> ClientMessages(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                return RedirectToAction("Messages");
            }

            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound();
            }

            var messages = await _messageService.GetMessagesByClientId(clientId);

            // 메시지 표시를 위한 ViewModel
            var viewModel = new ClientMessageViewModel
            {
                Client = client,
                Messages = messages
            };

            return View(viewModel);
        }

        // 매치별 메시지 조회
        public async Task<IActionResult> MatchMessages(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                return RedirectToAction("Messages");
            }

            var messages = await _messageService.GetMessagesByMatchId(matchId);

            // 메시지 표시를 위한 ViewModel
            var viewModel = new MatchMessageViewModel
            {
                MatchId = matchId,
                Messages = messages
            };

            // 클라이언트 정보 추가
            foreach (var message in messages)
            {
                var sender = await _clientService.GetClientById(message.SenderId);
                if (sender != null)
                {
                    viewModel.ClientNames[message.SenderId] = $"Client {sender.ClientId.Substring(0, 8)}";
                }
            }

            return View(viewModel);
        }
        // AdminController.cs에 포인트 관리 관련 메서드 추가
        public async Task<IActionResult> AddPoints(string clientId, int amount)
        {
            if (string.IsNullOrEmpty(clientId) || amount <= 0)
            {
                TempData["Error"] = "유효하지 않은 요청입니다.";
                return RedirectToAction("Details", new { id = clientId });
            }

            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound();
            }

            try
            {
                // 포인트 추가
                int newPoints = await _clientService.AddPoints(clientId, amount);

                TempData["Success"] = $"{amount} 포인트가 추가되었습니다. 현재 포인트: {newPoints}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"포인트 추가 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = clientId });
        }

        public async Task<IActionResult> SubtractPoints(string clientId, int amount)
        {
            if (string.IsNullOrEmpty(clientId) || amount <= 0)
            {
                TempData["Error"] = "유효하지 않은 요청입니다.";
                return RedirectToAction("Details", new { id = clientId });
            }

            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound();
            }

            try
            {
                // 포인트 차감
                bool success = await _clientService.SubtractPoints(clientId, amount);

                if (success)
                {
                    TempData["Success"] = $"{amount} 포인트가 차감되었습니다. 현재 포인트: {client.Points - amount}";
                }
                else
                {
                    TempData["Error"] = "포인트가 부족합니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"포인트 차감 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = clientId });
        }

        public async Task<IActionResult> ActivatePreference(string clientId, string preferredGender, int maxDistance)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                TempData["Error"] = "유효하지 않은 요청입니다.";
                return RedirectToAction("Details", new { id = clientId });
            }

            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound();
            }

            if (client.Points < 1000)
            {
                TempData["Error"] = "포인트가 부족합니다. 최소 1000포인트가 필요합니다.";
                return RedirectToAction("Details", new { id = clientId });
            }

            try
            {
                // 선호도 활성화
                bool success = await _clientService.ActivatePreference(clientId, preferredGender, maxDistance);

                if (success)
                {
                    TempData["Success"] = "선호도 활성화에 성공했습니다.";
                }
                else
                {
                    TempData["Error"] = "선호도 활성화에 실패했습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"선호도 활성화 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = clientId });
        }
        public async Task<IActionResult> Points()
        {
            var clients = await _clientService.GetAllClients();
            return View(clients.OrderByDescending(c => c.Points).ToList());
        }
        [HttpPost]
        public async Task<IActionResult> ClearConnectionId(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                TempData["Error"] = "유효하지 않은 클라이언트 ID입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                await _clientService.ClearConnectionId(clientId);
                TempData["Success"] = "ConnectionId가 초기화되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"ConnectionId 초기화 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = clientId });
        }

        // 모든 오프라인 연결 정리
        [HttpPost]
        public async Task<IActionResult> ClearAllOfflineConnections()
        {
            try
            {
                await _clientService.ClearAllOfflineConnections();
                TempData["Success"] = "모든 오프라인 연결이 정리되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"연결 정리 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}