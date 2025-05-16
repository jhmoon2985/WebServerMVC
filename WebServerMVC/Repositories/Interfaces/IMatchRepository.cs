using System.Collections.Generic;
using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Repositories.Interfaces
{
    public interface IMatchRepository
    {
        Task<ClientMatch> GetMatchById(string matchId);
        Task<List<ClientMatch>> GetMatchesByClientId(string clientId);
        Task AddMatch(ClientMatch match);
        Task UpdateMatch(ClientMatch match);
        Task<List<ClientMatch>> GetRecentMatches(int count);
    }
}