using System;

namespace WebServerMVC.Models
{
    public class ClientMatch
    {
        public string Id { get; set; }
        public string ClientId1 { get; set; }
        public string ClientId2 { get; set; }
        public DateTime MatchedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public double Distance { get; set; }
    }
}