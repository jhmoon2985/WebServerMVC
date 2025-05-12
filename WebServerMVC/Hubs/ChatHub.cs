using System;
using System.Threading.Tasks;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace WebServerMVC.Hubs
{
    //[Authorize]
    public class ChatHub : Hub
    {
        private readonly IClientService _clientService;
        private readonly IMatchingService _matchingService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            IClientService clientService,
            IMatchingService matchingService,
            ILogger<ChatHub> logger)
        {
            _clientService = clientService;
            _matchingService = matchingService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                // 활성 매칭 종료
                await _matchingService.EndMatch(clientId);

                // 클라이언트 상태 업데이트
                var client = await _clientService.GetClientById(clientId);
                if (client != null)
                {
                    // ConnectionId를 비우고 IsMatched 상태 해제
                    client.ConnectionId = string.Empty;
                    client.IsMatched = false;
                    client.MatchedWithClientId = null;

                    // DB에 업데이트
                    try
                    {
                        await _clientService.UpdateClientLocation(clientId, client.Latitude, client.Longitude);
                        _logger.LogInformation($"Client {clientId} disconnected and state updated in database.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error updating client state on disconnect: {ex.Message}");
                    }
                }

                // 대기열에서 제거
                _matchingService.RemoveFromWaitingQueue(clientId);
            }

            await base.OnDisconnectedAsync(exception);
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        }

        public async Task Register(string existingClientId = null)
        {
            try
            {
                _logger.LogInformation($"Register called with existingClientId: {existingClientId}");

                string clientId = await _clientService.RegisterClient(Context.ConnectionId, existingClientId);
                Context.Items["ClientId"] = clientId;

                _logger.LogInformation($"Client registered: {clientId}");

                await Clients.Caller.SendAsync("Registered", new { ClientId = clientId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Register error: {ex.Message} | StackTrace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("RegisterError", ex.Message);
                throw; // 클라이언트에게 오류 전파
            }
        }

        public async Task UpdateLocation(double latitude, double longitude)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientLocation(clientId, latitude, longitude);
            }
        }

        public async Task UpdateGender(string gender)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientGender(clientId, gender);
            }
        }

        public async Task JoinWaitingQueue(string gender)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _matchingService.AddToWaitingQueue(clientId, Context.ConnectionId, gender);

                // 매칭 프로세스 시작
                await _matchingService.ProcessMatchingQueue();
            }
        }

        public async Task SendMessage(string message)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                var client = await _clientService.GetClientById(clientId);
                if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
                {
                    var partner = await _clientService.GetClientById(client.MatchedWithClientId);
                    if (partner != null)
                    {
                        string groupName = $"chat_{client.ClientId}_{partner.ClientId}";
                        await Clients.Group(groupName).SendAsync("ReceiveMessage", new
                        {
                            SenderId = clientId,
                            Message = message,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        public async Task EndChat()
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _matchingService.EndMatch(clientId);

                // 다시 대기열에 추가
                var client = await _clientService.GetClientById(clientId);
                if (client != null)
                {
                    await _matchingService.AddToWaitingQueue(clientId, Context.ConnectionId, client.Gender);
                    await _matchingService.ProcessMatchingQueue();
                }
            }
        }
        // ChatHub.cs에 추가
        public async Task GetClientStats()
        {
            var clientService = Context.GetHttpContext().RequestServices.GetRequiredService<IClientService>();
            var clients = await clientService.GetAllClients();

            var stats = new
            {
                TotalClients = clients.Count,
                OnlineClients = clients.Count(c => !string.IsNullOrEmpty(c.ConnectionId)),
                MatchedClients = clients.Count(c => c.IsMatched),
                WaitingClients = clients.Count(c => !string.IsNullOrEmpty(c.ConnectionId) && !c.IsMatched)
            };

            await Clients.Caller.SendAsync("ReceiveClientStats", stats);
        }
    }
}