using System;
using System.Threading.Tasks;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebServerMVC.Utilities;

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

        public async Task AddToWaitingQueue(string clientId, string connectionId, double latitude, double longitude, string gender, string preferredGender, int maxDistance)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client != null)
            {
                client.IsMatched = false;
                client.MatchedWithClientId = null;

                await _clientService.UpdateClientAll(clientId, latitude, longitude, gender, false, null);
                //await _clientService.UpdateClientLocation(clientId, longitude, longitude);
                //await _clientService.UpdateClientGender(clientId, gender);
                //_waitingQueue.Enqueue(clientId, connectionId, gender);
                // 수정된 WaitingQueue.Enqueue 메서드 호출
                _waitingQueue.Enqueue(clientId, connectionId, gender, preferredGender, maxDistance, latitude, longitude);


                // 클라이언트에게 대기열 입장 알림
                await _hubContext.Clients.Client(connectionId).SendAsync("EnqueuedToWaiting");
            }
        }

        public async Task ProcessMatchingQueue()
        {
            var waitingClients = _waitingQueue.GetAllWaitingClients().ToList();

            // 매칭 시도할 클라이언트들을 순회
            for (int i = 0; i < waitingClients.Count; i++)
            {
                var client = waitingClients[i];

                // 이미 처리된 클라이언트 건너뛰기
                if (!_waitingQueue.TryRemoveClient(client.ClientId, out _))
                    continue;

                // 매칭 파트너 찾기
                if (_waitingQueue.TryFindMatch(client, _locationService, out var partner))
                {
                    // 파트너를 대기열에서 제거
                    _waitingQueue.TryRemoveClient(partner.ClientId, out _);

                    // 두 클라이언트 매칭 처리
                    await MatchClients(client, partner);
                }
                else
                {
                    // 매칭 파트너가 없으면 다시 대기열에 추가
                    _waitingQueue.Enqueue(
                        client.ClientId,
                        client.ConnectionId,
                        client.Gender,
                        client.PreferredGender,
                        client.MaxDistance,
                        client.Latitude,
                        client.Longitude);
                }
            }
        }
        private async Task MatchClients(WaitingClient client1, WaitingClient client2)
        {
            // DB에서 클라이언트 정보 가져오기
            var dbClient1 = await _clientService.GetClientById(client1.ClientId);
            var dbClient2 = await _clientService.GetClientById(client2.ClientId);

            if (dbClient1 == null || dbClient2 == null)
                return;

            // 매칭 상태 업데이트
            dbClient1.IsMatched = true;
            dbClient1.MatchedWithClientId = client2.ClientId;
            dbClient2.IsMatched = true;
            dbClient2.MatchedWithClientId = client1.ClientId;

            await _clientService.UpdateClient(dbClient1);
            await _clientService.UpdateClient(dbClient2);

            // 거리 계산
            var distance = _locationService.CalculateDistance(
                client1.Latitude, client1.Longitude,
                client2.Latitude, client2.Longitude);

            // 그룹 이름 생성
            string groupName = ChatUtilities.CreateChatGroupName(client1.ClientId, client2.ClientId);

            // 매칭 기록 저장
            var clientMatch = new ClientMatch
            {
                Id = Guid.NewGuid().ToString(),
                ClientId1 = client1.ClientId,
                ClientId2 = client2.ClientId,
                MatchedAt = DateTime.UtcNow,
                Distance = distance,
                ChatGroupName = groupName
            };

            await _matchRepository.AddMatch(clientMatch);

            // 매칭된 클라이언트들에게 알림
            await _hubContext.Clients.Client(client1.ConnectionId).SendAsync("Matched", new
            {
                PartnerGender = client2.Gender,
                Distance = distance,
                GroupName = groupName
            });

            await _hubContext.Clients.Client(client2.ConnectionId).SendAsync("Matched", new
            {
                PartnerGender = client1.Gender,
                Distance = distance,
                GroupName = groupName
            });

            // 그룹 생성
            await _hubContext.Groups.AddToGroupAsync(client1.ConnectionId, groupName);
            await _hubContext.Groups.AddToGroupAsync(client2.ConnectionId, groupName);
        }
        public async Task EndMatch(string clientId)
        {
            var client = await _clientService.GetClientById(clientId);
            if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
            {
                var partner = await _clientService.GetClientById(client.MatchedWithClientId);

                // 활성 매칭 조회
                var matches = await _matchRepository.GetMatchesByClientId(clientId);
                var activeMatch = matches.FirstOrDefault(m =>
                    m.EndedAt == null &&
                    ((m.ClientId1 == clientId && m.ClientId2 == client.MatchedWithClientId) ||
                     (m.ClientId2 == clientId && m.ClientId1 == client.MatchedWithClientId)));

                string groupName;

                // 활성 매칭이 있으면 종료 시간 업데이트
                if (activeMatch != null)
                {
                    // 저장된 그룹 이름 사용 (필드가 추가된 경우)
                    groupName = !string.IsNullOrEmpty(activeMatch.ChatGroupName)
                        ? activeMatch.ChatGroupName
                        : ChatUtilities.CreateChatGroupName(clientId, client.MatchedWithClientId);

                    activeMatch.EndedAt = DateTime.UtcNow;
                    await _matchRepository.UpdateMatch(activeMatch);
                }
                else
                {
                    // 매칭 기록이 없는 경우 그룹 이름 새로 생성
                    groupName = ChatUtilities.CreateChatGroupName(clientId, client.MatchedWithClientId);
                }

                if (partner != null)
                {
                    // 파트너에게 매칭 종료 알림
                    await _hubContext.Clients.Client(partner.ConnectionId).SendAsync("MatchEnded");

                    // 파트너를 다시 대기열에 넣기
                    await AddToWaitingQueue(partner.ClientId, partner.ConnectionId, partner.Latitude, partner.Longitude, partner.Gender, partner.PreferredGender, partner.MaxDistance);
                }

                // 매칭 종료 및 그룹 제거
                if (!string.IsNullOrEmpty(client.ConnectionId) && partner != null)
                {
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
        public Task RemoveFromWaitingQueue(string clientId)
        {
            _waitingQueue.TryRemoveClient(clientId, out _);
            return Task.CompletedTask;
        }
    }
}