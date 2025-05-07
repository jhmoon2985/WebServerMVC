using System;

namespace WebServerMVC.Models
{
    public class Client
    {
        public string ClientId { get; set; } = Guid.NewGuid().ToString();
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public string Gender { get; set; } = string.Empty;
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public bool IsMatched { get; set; } = false;
        public string? MatchedWithClientId { get; set; } = null;
    }
}