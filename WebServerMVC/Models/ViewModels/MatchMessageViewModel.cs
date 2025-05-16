using System.Collections.Generic;

namespace WebServerMVC.Models
{
    public class MatchMessageViewModel
    {
        public string MatchId { get; set; }
        public List<TextMessage> Messages { get; set; } = new List<TextMessage>();
        public Dictionary<string, string> ClientNames { get; set; } = new Dictionary<string, string>();
    }
}