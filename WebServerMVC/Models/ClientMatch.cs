using System;

namespace WebServerMVC.Models
{
    public class ClientMatch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClientId1 { get; set; } = string.Empty;
        public string ClientId2 { get; set; } = string.Empty;
        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; } = null;
        public double Distance { get; set; } = 0;
    }
}