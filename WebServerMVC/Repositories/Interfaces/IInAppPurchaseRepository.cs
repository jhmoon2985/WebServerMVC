using WebServerMVC.Models;

namespace WebServerMVC.Repositories.Interfaces
{
    public interface IInAppPurchaseRepository
    {
        Task<InAppPurchase?> GetPurchaseById(string purchaseId);
        Task<InAppPurchase?> GetByPurchaseToken(string purchaseToken);
        Task<List<InAppPurchase>> GetPurchasesByClientId(string clientId);
        Task AddPurchase(InAppPurchase purchase);
        Task UpdatePurchase(InAppPurchase purchase);
        Task<List<InAppPurchase>> GetRecentPurchases(int count);
        Task<List<InAppPurchase>> GetPurchasesByStatus(PurchaseStatus status);
    }
}