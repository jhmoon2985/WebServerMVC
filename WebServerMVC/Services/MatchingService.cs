using System;
using System.Threading.Tasks;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace WebServerMVC.Services
{
    public class MatchingService : IMatchingService
    {
        private readonly WaitingQueue _waitingQueue;
        private readonly IClientService _clientService;
        private readonly IMatchRepository _matchRepository;
        private readonly ILocationService _locationService;
        private readonly IHubContext<ChatHub> _hubContext;

        public MatchingService(
            WaitingQueue waitingQueue,
            IClientService clientService,
            IMatchRepository matchRepository,
            ILocationService locationService,
            IHubContext<ChatHub> hubContext)
        {
            _waitingQueue = waitingQueue;
            _clientService = clientService;
            _matchRepository = matchRepository;
            _locationService = locationService;
            _hubContext = hubContext;
        }

        public async Task AddToWaitingQueue(string clientId, string connectionId, string gender)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client != null)
            {
                client.IsMatched = false;
                client.MatchedWithClientId = null;
                await _clientService.UpdateClientGender(clientId, gender);
                _waitingQueue.Enqueue(clientId, connectionId, gender);

                // 클라이언트에게 대기열 입장 알림
                await _hubContext.Clients.Client(connectionId).SendAsync("EnqueuedToWaiting");
            }
        }

        public async Task ProcessMatchingQueue()
        {
            while (_waitingQueue.TryDequeueMatch(out var match))
            {
                var (clientId1, connectionId1, clientId2, connectionId2) = match;

                var client1 = await _clientService.GetClientById(clientId1);
                var client2 = await _clientService.GetClientById(clientId2);

                if (client1 == null || client2 == null)
                    continue;

                client1.IsMatched = true;
                client1.MatchedWithClientId = clientId2;
                client2.IsMatched = true;
                client2.MatchedWithClientId = clientId1;

                await _clientService.UpdateClientLocation(clientId1, client1.Latitude, client1.Longitude);
                await _clientService.UpdateClientLocation(clientId2, client2.Latitude, client2.Longitude);

                // 거리 계산
                var distance = _locationService.CalculateDistance(
                    client1.Latitude, client1.Longitude,
                    client2.Latitude, client2.Longitude);

                // 매칭 기록 저장
                await _matchRepository.AddMatch(new ClientMatch
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId1 = clientId1,
                    ClientId2 = clientId2,
                    MatchedAt = DateTime.UtcNow,
                    Distance = distance
                });

                // 매칭된 클라이언트들에게 알림
                await _hubContext.Clients.Client(connectionId1).SendAsync("Matched", new
                {
                    PartnerGender = client2.Gender,
                    Distance = distance
                });

                await _hubContext.Clients.Client(connectionId2).SendAsync("Matched", new
                {
                    PartnerGender = client1.Gender,
                    Distance = distance
                });

                // 그룹 생성
                string groupName = $"chat_{clientId1}_{clientId2}";
                await _hubContext.Groups.AddToGroupAsync(connectionId1, groupName);
                await _hubContext.Groups.AddToGroupAsync(connectionId2, groupName);
            }
        }

        public async Task EndMatch(string clientId)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
            {
                var partner = await _clientService.GetClientById(client.MatchedWithClientId);

                // 매칭 해제
                client.IsMatched = false;
                client.MatchedWithClientId = null;

                if (partner != null)
                {
                    partner.IsMatched = false;
                    partner.MatchedWithClientId = null;

                    // 파트너에게 매칭 종료 알림
                    await _hubContext.Clients.Client(partner.ConnectionId).SendAsync("MatchEnded");

                    // 파트너를 다시 대기열에 넣기
                    await AddToWaitingQueue(partner.ClientId, partner.ConnectionId, partner.Gender);
                }

                // 매칭 종료 및 그룹 제거
                if (!string.IsNullOrEmpty(client.ConnectionId) && partner != null)
                {
                    string groupName = $"chat_{client.ClientId}_{partner.ClientId}";
                    await _hubContext.Groups.RemoveFromGroupAsync(client.ConnectionId, groupName);

                    if (!string.IsNullOrEmpty(partner.ConnectionId))
                    {
                        await _hubContext.Groups.RemoveFromGroupAsync(partner.ConnectionId, groupName);
                    }
                }
            }
        }

        public async Task<Client> GetMatchedClient(string clientId)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
            {
                return await _clientService.GetClientById(client.MatchedWithClientId);
            }

            return null;
        }
    }
}