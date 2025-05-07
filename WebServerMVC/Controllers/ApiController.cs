using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly IMatchingService _matchingService;

        public ApiController(
            IClientService clientService,
            IMatchingService matchingService)
        {
            _clientService = clientService;
            _matchingService = matchingService;
        }

        [HttpGet("client/{clientId}")]
        public async Task<IActionResult> GetClient(string clientId)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound();
            }

            return Ok(client);
        }

        [HttpGet("client/{clientId}/match")]
        public async Task<IActionResult> GetMatch(string clientId)
        {
            var matchedClient = await _matchingService.GetMatchedClient(clientId);
            if (matchedClient == null)
            {
                return NotFound(new { message = "No active match found" });
            }

            return Ok(new
            {
                PartnerGender = matchedClient.Gender,
                PartnerClientId = matchedClient.ClientId
            });
        }

        [HttpPost("client/register")]
        public async Task<IActionResult> RegisterClient([FromBody] RegisterClientRequest request)
        {
            if (string.IsNullOrEmpty(request.ConnectionId))
            {
                return BadRequest(new { message = "ConnectionId is required" });
            }

            string clientId = await _clientService.RegisterClient(request.ConnectionId, request.ExistingClientId);

            return Ok(new { ClientId = clientId });
        }

        [HttpPost("client/{clientId}/location")]
        public async Task<IActionResult> UpdateLocation(string clientId, [FromBody] UpdateLocationRequest request)
        {
            await _clientService.UpdateClientLocation(clientId, request.Latitude, request.Longitude);

            return Ok();
        }
    }

    public class RegisterClientRequest
    {
        public string ConnectionId { get; set; }
        public string ExistingClientId { get; set; }
    }

    public class UpdateLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}