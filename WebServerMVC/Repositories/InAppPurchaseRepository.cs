using Microsoft.EntityFrameworkCore;
using WebServerMVC.Data;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;

namespace WebServerMVC.Repositories
{
    public class InAppPurchaseRepository : IInAppPurchaseRepository
    {
        private readonly AppDbContext _context;

        public InAppPurchaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InAppPurchase?> GetPurchaseById(string purchaseId)
        {
            return await _context.InAppPurchases.FindAsync(purchaseId);
        }

        public async Task<InAppPurchase?> GetByPurchaseToken(string purchaseToken)
        {
            return await _context.InAppPurchases
                .FirstOrDefaultAsync(p => p.PurchaseToken == purchaseToken);
        }

        public async Task<List<InAppPurchase>> GetPurchasesByClientId(string clientId)
        {
            return await _context.InAppPurchases
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.PurchasedAt)
                .ToListAsync();
        }

        public async Task AddPurchase(InAppPurchase purchase)
        {
            await _context.InAppPurchases.AddAsync(purchase);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePurchase(InAppPurchase purchase)
        {
            _context.InAppPurchases.Update(purchase);
            await _context.SaveChangesAsync();
        }

        public async Task<List<InAppPurchase>> GetRecentPurchases(int count)
        {
            return await _context.InAppPurchases
                .OrderByDescending(p => p.PurchasedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<InAppPurchase>> GetPurchasesByStatus(PurchaseStatus status)
        {
            return await _context.InAppPurchases
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.PurchasedAt)
                .ToListAsync();
        }
    }
}

