using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebServerMVC.Data;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace WebServerMVC.Repositories
{
    public class MatchRepository : IMatchRepository
    {
        private readonly AppDbContext _context;

        public MatchRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClientMatch> GetMatchById(string matchId)
        {
            return await _context.Matches.FindAsync(matchId);
        }

        public async Task<List<ClientMatch>> GetMatchesByClientId(string clientId)
        {
            return await _context.Matches
                .Where(m => m.ClientId1 == clientId || m.ClientId2 == clientId)
                .OrderByDescending(m => m.MatchedAt)
                .ToListAsync();
        }

        public async Task AddMatch(ClientMatch match)
        {
            await _context.Matches.AddAsync(match);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMatch(ClientMatch match)
        {
            _context.Matches.Update(match);
            await _context.SaveChangesAsync();
        }
        public async Task<List<ClientMatch>> GetRecentMatches(int count)
        {
            return await _context.Matches
                .OrderByDescending(m => m.MatchedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}