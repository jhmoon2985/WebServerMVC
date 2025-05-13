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

            // 기본 위치 (서울 중심부)
            double latitude = 35.5642135;
            double longitude = 127.0016985;
            string gender = "male"; // 기본값

            string clientId = await _clientService.RegisterClient(
                request.ExistingClientId,
                request.ConnectionId,
                latitude,
                longitude,
                gender);

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
        public double Latitude { get; set; } = 37.5642135;  // 기본값 설정
        public double Longitude { get; set; } = 127.0016985;  // 기본값 설정
        public string Gender { get; set; } = "male";  // 기본값 설정
    }

    public class UpdateLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}