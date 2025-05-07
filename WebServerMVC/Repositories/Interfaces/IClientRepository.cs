using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Repositories.Interfaces
{
    public interface IClientRepository
    {
        Task<Client> GetClientById(string clientId);
        Task<Client> GetClientByConnectionId(string connectionId);
        Task AddClient(Client client);
        Task UpdateClient(Client client);
        Task DeleteClient(string clientId);
        // IClientRepository.cs에 다음 메서드 추가
        Task<List<Client>> GetAllClients();
    }
}