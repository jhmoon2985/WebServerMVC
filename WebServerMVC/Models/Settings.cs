namespace WebServerMVC.Models
{
    public class ClientSettings
    {
        public int MaxInactivityMinutes { get; set; } = 30;
        public int AutoCleanupIntervalMinutes { get; set; } = 5;
        public int LocationExpiryHours { get; set; } = 24;
    }

    public class MatchingSettings
    {
        public int MatchProcessingIntervalSeconds { get; set; } = 5;
        public int MaxWaitingTimeMinutes { get; set; } = 30;
        public bool EnableGeoMatching { get; set; } = true;
        public int MaxDistanceKm { get; set; } = 10000;
    }
}