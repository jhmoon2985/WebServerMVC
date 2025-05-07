using System;
using System.Threading.Tasks;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace WebServerMVC.Hubs
{
    [Authorize]
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
                await _matchingService.EndMatch(clientId);
            }

            await base.OnDisconnectedAsync(exception);
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        }

        public async Task Register(string existingClientId = null)
        {
            string clientId = await _clientService.RegisterClient(Context.ConnectionId, existingClientId);
            Context.Items["ClientId"] = clientId;

            await Clients.Caller.SendAsync("Registered", new { ClientId = clientId });
        }

        public async Task UpdateLocation(double latitude, double longitude)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientLocation(clientId, latitude, longitude);
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