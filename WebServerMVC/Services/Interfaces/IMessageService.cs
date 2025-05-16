using System.Collections.Generic;
using System.Threading.Tasks;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IMessageService
    {
        Task<TextMessage> SaveTextMessage(string senderId, string matchId, string content);
        Task<List<TextMessage>> GetMessagesByMatchId(string matchId);
        Task<List<TextMessage>> GetMessagesByClientId(string clientId);
    }
}