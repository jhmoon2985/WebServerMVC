using System.Collections.Concurrent;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Models
{
    // Models/WaitingQueue.cs 수정
    public class WaitingQueue
    {
        // 기존 큐 대신 모든 대기 중인 클라이언트를 딕셔너리에 저장
        private readonly ConcurrentDictionary<string, WaitingClient> _waitingClients = new ConcurrentDictionary<string, WaitingClient>();

        public void Enqueue(string clientId, string connectionId, string gender, string preferredGender, int maxDistance, double latitude, double longitude)
        {
            var waitingClient = new WaitingClient
            {
                ClientId = clientId,
                ConnectionId = connectionId,
                Gender = gender,
                PreferredGender = preferredGender,
                MaxDistance = maxDistance,
                Latitude = latitude,
                Longitude = longitude,
                EnqueuedAt = DateTime.UtcNow
            };

            _waitingClients[clientId] = waitingClient;
        }

        public bool TryFindMatch(WaitingClient client, ILocationService locationService, out WaitingClient match)
        {
            match = null;

            // 모든 대기 중인 클라이언트를 확인
            foreach (var potentialMatch in _waitingClients.Values)
            {
                // 자기 자신은 제외
                if (potentialMatch.ClientId == client.ClientId)
                    continue;

                // 성별 선호도 확인
                bool genderMatches =
                    (client.PreferredGender == "any" || client.PreferredGender == potentialMatch.Gender) &&
                    (potentialMatch.PreferredGender == "any" || potentialMatch.PreferredGender == client.Gender);

                if (!genderMatches)
                    continue;

                // 거리 계산
                double distance = locationService.CalculateDistance(
                    client.Latitude, client.Longitude,
                    potentialMatch.Latitude, potentialMatch.Longitude);

                // 거리 선호도 확인 (양쪽 모두 거리 제한 내에 있어야 함)
                if (distance > client.MaxDistance || distance > potentialMatch.MaxDistance)
                    continue;

                // 매칭 조건 충족
                match = potentialMatch;
                return true;
            }

            return false;
        }

        public bool TryRemoveClient(string clientId, out WaitingClient client)
        {
            return _waitingClients.TryRemove(clientId, out client);
        }

        public int Count => _waitingClients.Count;

        public IEnumerable<WaitingClient> GetAllWaitingClients()
        {
            return _waitingClients.Values;
        }
    }

    // 대기 중인 클라이언트 정보를 담는 클래스
    public class WaitingClient
    {
        public string ClientId { get; set; }
        public string ConnectionId { get; set; }
        public string Gender { get; set; }
        public string PreferredGender { get; set; }
        public int MaxDistance { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime EnqueuedAt { get; set; }
    }
}