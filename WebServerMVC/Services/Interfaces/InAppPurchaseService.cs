using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IInAppPurchaseService
    {
        Task<PurchaseVerificationResponse> VerifyGooglePurchase(GooglePurchaseVerificationRequest request);
        Task<PurchaseVerificationResponse> VerifyOneStorePurchase(OneStorePurchaseVerificationRequest request);
        Task<InAppPurchase?> GetPurchaseById(string purchaseId);
        Task<List<InAppPurchase>> GetPurchasesByClientId(string clientId);
        Task<bool> ConsumePurchase(string purchaseId);
        Task<List<ProductInfo>> GetAvailableProducts();
    }
}