using WebServerMVC.Models;

namespace WebServerMVC.Models
{
    /// <summary>
    /// 구매 통계 뷰모델
    /// </summary>
    public class PurchaseStatsViewModel
    {
        public int TotalPurchases { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalPoints { get; set; }
        public int GooglePurchases { get; set; }
        public int OneStorePurchases { get; set; }
        public int VerifiedPurchases { get; set; }
        public int ConsumedPurchases { get; set; }
        public int FailedPurchases { get; set; }
        public int TodayPurchases { get; set; }
        public List<InAppPurchase> RecentPurchases { get; set; } = new();

        /// <summary>
        /// 평균 구매 금액
        /// </summary>
        public decimal AveragePurchaseAmount => TotalPurchases > 0 ? TotalAmount / TotalPurchases : 0;

        /// <summary>
        /// 평균 구매 포인트
        /// </summary>
        public double AveragePurchasePoints => TotalPurchases > 0 ? (double)TotalPoints / TotalPurchases : 0;

        /// <summary>
        /// Google Play 구매 비율
        /// </summary>
        public double GooglePurchasePercentage => TotalPurchases > 0 ? (double)GooglePurchases / TotalPurchases * 100 : 0;

        /// <summary>
        /// OneStore 구매 비율
        /// </summary>
        public double OneStorePurchasePercentage => TotalPurchases > 0 ? (double)OneStorePurchases / TotalPurchases * 100 : 0;

        /// <summary>
        /// 성공 구매 비율
        /// </summary>
        public double SuccessRate => TotalPurchases > 0 ? (double)VerifiedPurchases / TotalPurchases * 100 : 0;
    }

    /// <summary>
    /// 클라이언트 구매 내역 뷰모델
    /// </summary>
    public class ClientPurchaseViewModel
    {
        public Client Client { get; set; } = new();
        public List<InAppPurchase> Purchases { get; set; } = new();
        public decimal TotalSpent { get; set; }
        public int TotalPoints { get; set; }

        /// <summary>
        /// 첫 구매일
        /// </summary>
        public DateTime? FirstPurchaseDate => Purchases.Any() ? Purchases.Min(p => p.PurchasedAt) : null;

        /// <summary>
        /// 마지막 구매일
        /// </summary>
        public DateTime? LastPurchaseDate => Purchases.Any() ? Purchases.Max(p => p.PurchasedAt) : null;

        /// <summary>
        /// 평균 구매 간격 (일)
        /// </summary>
        public double AveragePurchaseInterval
        {
            get
            {
                if (Purchases.Count <= 1) return 0;
                var first = FirstPurchaseDate;
                var last = LastPurchaseDate;
                if (first.HasValue && last.HasValue)
                {
                    var totalDays = (last.Value - first.Value).TotalDays;
                    return totalDays / (Purchases.Count - 1);
                }
                return 0;
            }
        }

        /// <summary>
        /// 가장 많이 구매한 상품
        /// </summary>
        public string? MostPurchasedProduct
        {
            get
            {
                return Purchases
                    .GroupBy(p => p.ProductId)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
            }
        }

        /// <summary>
        /// 선호하는 스토어
        /// </summary>
        public string? PreferredStore
        {
            get
            {
                var googleCount = Purchases.Count(p => p.Store == "google");
                var oneStoreCount = Purchases.Count(p => p.Store == "onestore");

                if (googleCount > oneStoreCount) return "google";
                if (oneStoreCount > googleCount) return "onestore";
                return null; // 동일한 경우
            }
        }
    }

    /// <summary>
    /// 월별 구매 통계 모델
    /// </summary>
    public class MonthlyPurchaseStats
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int PurchaseCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalPoints { get; set; }
        public int GooglePurchases { get; set; }
        public int OneStorePurchases { get; set; }

        public string MonthName => new DateTime(Year, Month, 1).ToString("yyyy년 MM월");
        public decimal AverageAmount => PurchaseCount > 0 ? TotalAmount / PurchaseCount : 0;
    }

    /// <summary>
    /// 상품별 구매 통계 모델
    /// </summary>
    public class ProductPurchaseStats
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int PurchaseCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalPoints { get; set; }
        public DateTime? LastPurchaseDate { get; set; }

        public decimal AverageAmount => PurchaseCount > 0 ? TotalAmount / PurchaseCount : 0;
        public double AveragePoints => PurchaseCount > 0 ? (double)TotalPoints / PurchaseCount : 0;
    }

    /// <summary>
    /// 일별 구매 통계 모델
    /// </summary>
    public class DailyPurchaseStats
    {
        public DateTime Date { get; set; }
        public int PurchaseCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalPoints { get; set; }
        public int UniqueClients { get; set; }

        public string DateString => Date.ToString("yyyy-MM-dd");
        public string DayOfWeekString => Date.ToString("dddd");
        public decimal AverageAmount => PurchaseCount > 0 ? TotalAmount / PurchaseCount : 0;
    }

    /// <summary>
    /// 구매 상태별 통계 모델
    /// </summary>
    public class PurchaseStatusStats
    {
        public PurchaseStatus Status { get; set; }
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalPoints { get; set; }
        public double Percentage { get; set; }

        public string StatusText => Status switch
        {
            PurchaseStatus.Pending => "대기중",
            PurchaseStatus.Verified => "검증완료",
            PurchaseStatus.Failed => "실패",
            PurchaseStatus.Consumed => "소비완료",
            PurchaseStatus.Refunded => "환불됨",
            _ => "알수없음"
        };

        public string BadgeClass => Status switch
        {
            PurchaseStatus.Verified => "bg-success",
            PurchaseStatus.Consumed => "bg-info",
            PurchaseStatus.Failed => "bg-danger",
            PurchaseStatus.Pending => "bg-warning",
            PurchaseStatus.Refunded => "bg-secondary",
            _ => "bg-dark"
        };
    }

    /// <summary>
    /// 종합 대시보드 뷰모델
    /// </summary>
    public class PurchaseDashboardViewModel
    {
        public PurchaseStatsViewModel Stats { get; set; } = new();
        public List<MonthlyPurchaseStats> MonthlyStats { get; set; } = new();
        public List<ProductPurchaseStats> ProductStats { get; set; } = new();
        public List<DailyPurchaseStats> DailyStats { get; set; } = new();
        public List<PurchaseStatusStats> StatusStats { get; set; } = new();
        public List<InAppPurchase> RecentFailedPurchases { get; set; } = new();
        public List<Client> TopSpendingClients { get; set; } = new();

        /// <summary>
        /// 서비스 상태 정보
        /// </summary>
        public ServiceStatusInfo ServiceStatus { get; set; } = new();
    }

    /// <summary>
    /// 서비스 상태 정보
    /// </summary>
    public class ServiceStatusInfo
    {
        public bool GooglePlayConnected { get; set; }
        public bool OneStoreConnected { get; set; }
        public bool OneStoreConfigured { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }

        public bool AllServicesHealthy => OneStoreConnected && OneStoreConfigured;
        public string OverallStatus => AllServicesHealthy ? "정상" : "문제발생";
    }
}