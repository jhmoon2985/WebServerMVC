namespace WebServerMVC.Models
{
    public class DatabaseStatsViewModel
    {
        public int ClientsCount { get; set; }
        public int MatchesCount { get; set; }
        public int ActiveMatchesCount { get; set; }
        public int OnlineClientsCount { get; set; }
        public int TotalPoints { get; set; }
    }
}