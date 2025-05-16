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
    }
}