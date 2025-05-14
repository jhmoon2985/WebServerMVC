using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IClientService
    {
        Task<string> RegisterClient(string clientId, string connectionId, double latitude, double longitude, string gender, string preferredGender, int maxDistance);
        Task<Client> GetClientById(string clientId);
        Task UpdateClientAll(string clientId, double latitude, double longitude, string gender, bool IsMatched, string MatchedWithClientId);
        Task UpdateClientLocation(string clientId, double latitude, double longitude);
        Task UpdateClientGender(string clientId, string gender);
        Task UpdateClientPreferences(string clientId, string preferredGender, int maxDistance);
        Task UpdateClientMatchClientId(string clientId, string MatchedWithClientId);
        Task RemoveClient(string clientId);
        // IClientService.cs에 다음 메서드 추가
        Task<List<Client>> GetAllClients();
        Task UpdateClient(Client client);
    }
}