using System;
using System.ComponentModel.DataAnnotations;

namespace WebServerMVC.Models
{
    public class InAppPurchase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClientId { get; set; }
        public string Store { get; set; } // "google" or "onestore"
        public string ProductId { get; set; }
        public string PurchaseToken { get; set; }
        public string TransactionId { get; set; }
        public int Points { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "KRW";
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedAt { get; set; }
        public string? VerificationData { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum PurchaseStatus
    {
        Pending,      // 검증 대기
        Verified,     // 검증 완료
        Failed,       // 검증 실패
        Consumed,     // 소비 완료
        Refunded      // 환불됨
    }

    // 구글 플레이 검증 요청/응답 모델
    public class GooglePurchaseVerificationRequest
    {
        [Required]
        public string ProductId { get; set; }
        [Required]
        public string PurchaseToken { get; set; }
        [Required]
        public string ClientId { get; set; }
    }

    public class GooglePurchaseResponse
    {
        public int ConsumptionState { get; set; }
        public int PurchaseState { get; set; }
        public long PurchaseTimeMillis { get; set; }
        public int AcknowledgmentState { get; set; }
        public string? DeveloperPayload { get; set; }
        public string? OrderId { get; set; }
    }

    // 원스토어 검증 요청/응답 모델
    public class OneStorePurchaseVerificationRequest
    {
        [Required]
        public string ProductId { get; set; }
        [Required]
        public string PurchaseToken { get; set; }
        [Required]
        public string ClientId { get; set; }
    }

    public class OneStorePurchaseResponse
    {
        public string PurchaseId { get; set; }
        public string ProductId { get; set; }
        public int PurchaseState { get; set; }
        public long PurchaseTime { get; set; }
        public string? DeveloperPayload { get; set; }
        public int Quantity { get; set; }
    }

    // API 응답 모델
    public class PurchaseVerificationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? PointsAwarded { get; set; }
        public string? PurchaseId { get; set; }
    }

    // 상품 정보 모델
    public class ProductInfo
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public int Points { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "KRW";
        public bool IsActive { get; set; } = true;
    }
}