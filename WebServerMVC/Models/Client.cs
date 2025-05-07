using System;

namespace WebServerMVC.Models
{
    public class Client
    {
        public string ClientId { get; set; }
        public string ConnectionId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string Gender { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsMatched { get; set; }
        public string MatchedWithClientId { get; set; }
    }
}