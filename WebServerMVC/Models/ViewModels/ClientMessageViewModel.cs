using System.Collections.Generic;

namespace WebServerMVC.Models
{
    public class ClientMessageViewModel
    {
        public Client Client { get; set; }
        public List<TextMessage> Messages { get; set; } = new List<TextMessage>();
    }
}