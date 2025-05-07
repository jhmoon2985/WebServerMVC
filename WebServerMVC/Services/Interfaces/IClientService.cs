using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IClientService
    {
        Task<string> RegisterClient(string connectionId, string existingClientId = null);
        Task<Client> GetClientById(string clientId);
        Task UpdateClientLocation(string clientId, double latitude, double longitude);
        Task UpdateClientGender(string clientId, string gender);
        Task RemoveClient(string clientId);
        // IClientService.cs에 다음 메서드 추가
        Task<List<Client>> GetAllClients();
    }
}