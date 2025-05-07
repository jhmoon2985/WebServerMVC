using Microsoft.AspNetCore.Mvc;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly IClientService _clientService;
        private readonly IMatchingService _matchingService;

        public AdminController(IClientService clientService, IMatchingService matchingService)
        {
            _clientService = clientService;
            _matchingService = matchingService;
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
    }
}