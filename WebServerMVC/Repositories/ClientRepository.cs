using System.Threading.Tasks;
using WebServerMVC.Data;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace WebServerMVC.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly AppDbContext _context;

        public ClientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Client> GetClientById(string clientId)
        {
            return await _context.Clients.FindAsync(clientId);
        }

        public async Task<Client> GetClientByConnectionId(string connectionId)
        {
            return await _context.Clients
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
        }

        public async Task AddClient(Client client)
        {
            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateClient(Client client)
        {
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteClient(string clientId)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                _context.Clients.Remove(client);
                await _context.SaveChangesAsync();
            }
        }
        // ClientRepository.cs에 다음 메서드 구현 추가
        public async Task<List<Client>> GetAllClients()
        {
            return await _context.Clients.ToListAsync();
        }
    }
}