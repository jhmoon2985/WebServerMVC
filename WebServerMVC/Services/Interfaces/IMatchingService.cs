using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IMatchingService
    {
        Task AddToWaitingQueue(string clientId, string connectionId, string gender);
        Task ProcessMatchingQueue();
        Task EndMatch(string clientId);
        Task<Client> GetMatchedClient(string clientId);
    }
}