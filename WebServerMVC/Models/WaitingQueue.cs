using System.Collections.Concurrent;

namespace WebServerMVC.Models
{
    public class WaitingQueue
    {
        private readonly ConcurrentQueue<string> _maleQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _femaleQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, string> _clientIdConnectionMap = new ConcurrentDictionary<string, string>();

        public void Enqueue(string clientId, string connectionId, string gender)
        {
            _clientIdConnectionMap[clientId] = connectionId;

            if (gender.ToLower() == "male")
                _maleQueue.Enqueue(clientId);
            else
                _femaleQueue.Enqueue(clientId);
        }

        public bool TryDequeueMatch(out (string ClientId1, string ConnectionId1, string ClientId2, string ConnectionId2) match)
        {
            match = default;

            if (_maleQueue.IsEmpty || _femaleQueue.IsEmpty)
                return false;

            if (_maleQueue.TryDequeue(out string maleClientId) &&
                _femaleQueue.TryDequeue(out string femaleClientId))
            {
                _clientIdConnectionMap.TryGetValue(maleClientId, out string maleConnectionId);
                _clientIdConnectionMap.TryGetValue(femaleClientId, out string femaleConnectionId);

                match = (maleClientId, maleConnectionId, femaleClientId, femaleConnectionId);
                return true;
            }

            return false;
        }

        public void Remove(string clientId)
        {
            _clientIdConnectionMap.TryRemove(clientId, out _);
        }
    }
}