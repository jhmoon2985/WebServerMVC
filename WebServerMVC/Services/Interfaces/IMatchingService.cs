using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IMatchingService
    {
        Task AddToWaitingQueue(string clientId, string connectionId, double latitude, double longitude, string gender, string preferredGender, int maxDistance);
        Task ProcessMatchingQueue();
        Task EndMatch(string clientId);
        Task<Client> GetMatchedClient(string clientId);
        Task RemoveFromWaitingQueue(string clientId);
    }
}