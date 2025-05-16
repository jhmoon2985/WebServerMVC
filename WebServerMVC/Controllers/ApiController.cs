using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebServerMVC.Hubs;
using WebServerMVC.Utilities;
using WebServerMVC.Repositories.Interfaces;

namespace WebServerMVC.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly IMatchingService _matchingService;
        private readonly IImageService _imageService;
        private readonly IMessageService _messageService;
        private readonly IMatchRepository _matchRepository;
        private readonly IHubContext<ChatHub> _hubContext;

        public ApiController(
            IClientService clientService,
            IMatchingService matchingService,
            IImageService imageService,
            IMessageService messageService,
            IMatchRepository matchRepository,
            IHubContext<ChatHub> hubContext)
        {
            _clientService = clientService;
            _matchingService = matchingService;
            _imageService = imageService;
            _messageService = messageService;
            _matchRepository = matchRepository;
            _hubContext = hubContext;
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
        // 최근 매치 목록 가져오기
        [HttpGet("matches/recent")]
        public async Task<IActionResult> GetRecentMatches()
        {
            try
            {
                // 최근 매치 10개 가져오기
                // 이를 위해 MatchRepository에 GetRecentMatches 메서드 추가 필요
                var matchRepository = HttpContext.RequestServices.GetRequiredService<IMatchRepository>();
                var recentMatches = await matchRepository.GetRecentMatches(10);

                return Ok(recentMatches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"매치 목록 조회 오류: {ex.Message}" });
            }
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
            string PreferredGender = "any";
            int MaxDistance = 10000;

            string clientId = await _clientService.RegisterClient(
                request.ExistingClientId,
                request.ConnectionId,
                latitude,
                longitude,
                gender,
                PreferredGender,
                MaxDistance);

            return Ok(new { ClientId = clientId });
        }

        [HttpPost("client/{clientId}/location")]
        public async Task<IActionResult> UpdateLocation(string clientId, [FromBody] UpdateLocationRequest request)
        {
            await _clientService.UpdateClientLocation(clientId, request.Latitude, request.Longitude);

            return Ok();
        }

        [HttpPost("client/{clientId}/preferences")]
        public async Task<IActionResult> UpdatePreferences(string clientId, [FromBody] UpdatePreferencesRequest request)
        {
            await _clientService.UpdateClientPreferences(clientId, request.PreferredGender, request.MaxDistance);
            return Ok();
        }

        // 이미지 업로드 엔드포인트
        [HttpPost("client/{clientId}/image")]
        public async Task<IActionResult> UploadImage(string clientId, IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { message = "이미지 파일이 없습니다." });
            }

            var client = await _clientService.GetClientById(clientId);
            if (client == null)
            {
                return NotFound(new { message = "클라이언트를 찾을 수 없습니다." });
            }

            if (!client.IsMatched || string.IsNullOrEmpty(client.MatchedWithClientId))
            {
                return BadRequest(new { message = "매칭된 상대가 없습니다." });
            }

            try
            {
                // 이미지 서비스를 통해 이미지 저장
                var imageMessage = await _imageService.SaveImage(clientId, client.MatchedWithClientId, image);

                // 이미지 메시지 정보도 저장
                string groupName = ChatUtilities.CreateChatGroupName(clientId, client.MatchedWithClientId);
                // 메시지 저장 (이미지 메시지)
                await _messageService.SaveTextMessage(clientId, groupName, $"[IMAGE:{imageMessage.Id}]");

                // 상대방에게 이미지 메시지 전송
                var partner = await _clientService.GetClientById(client.MatchedWithClientId);
                if (partner != null && !string.IsNullOrEmpty(partner.ConnectionId))
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("ReceiveImageMessage", new
                    {
                        SenderId = clientId,
                        ImageId = imageMessage.Id,
                        ThumbnailUrl = imageMessage.ThumbnailUrl,
                        ImageUrl = imageMessage.FileUrl,
                        Timestamp = DateTime.UtcNow
                    });
                }

                return Ok(new
                {
                    imageId = imageMessage.Id,
                    thumbnailUrl = imageMessage.ThumbnailUrl,
                    imageUrl = imageMessage.FileUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"이미지 업로드 오류: {ex.Message}" });
            }
        }

        // 이미지 조회 엔드포인트
        [HttpGet("image/{imageId}")]
        public async Task<IActionResult> GetImage(string imageId)
        {
            var imageBytes = await _imageService.GetImageBytes(imageId);

            if (imageBytes == null)
            {
                return NotFound(new { message = "이미지를 찾을 수 없습니다." });
            }

            return File(imageBytes, "image/jpeg"); // 실제 이미지 타입에 맞게 조정 필요
        }

        // 썸네일 조회 엔드포인트
        [HttpGet("image/{imageId}/thumbnail")]
        public async Task<IActionResult> GetThumbnail(string imageId)
        {
            var thumbnailBytes = await _imageService.GetThumbnailBytes(imageId);

            if (thumbnailBytes == null)
            {
                return NotFound(new { message = "썸네일을 찾을 수 없습니다." });
            }

            return File(thumbnailBytes, "image/jpeg");
        }

        // ApiController.cs에 새로운 요청 모델 추가
        public class UpdatePreferencesRequest
        {
            public string PreferredGender { get; set; } = "any";
            public int MaxDistance { get; set; } = 10000;
        }
    }

    public class RegisterClientRequest
    {
        public string ConnectionId { get; set; }
        public string ExistingClientId { get; set; }
        public double Latitude { get; set; } = 37.5642135;  // 기본값 설정
        public double Longitude { get; set; } = 127.0016985;  // 기본값 설정
        public string Gender { get; set; } = "male";  // 기본값 설정
        public string PreferredGender { get; set; } = "any";
        public int MaxDistance { get; set; } = 10000;
    }

    public class UpdateLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}