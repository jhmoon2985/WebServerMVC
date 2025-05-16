using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Services
{
    public class MessageService : IMessageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<MessageService> _logger;
        private readonly string _messagesPath;

        public MessageService(IWebHostEnvironment webHostEnvironment, ILogger<MessageService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _messagesPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "messages");

            if (!Directory.Exists(_messagesPath))
            {
                Directory.CreateDirectory(_messagesPath);
            }
        }

        public async Task<TextMessage> SaveTextMessage(string senderId, string matchId, string content)
        {
            try
            {
                var message = new TextMessage
                {
                    SenderId = senderId,
                    MatchId = matchId,
                    Content = content,
                    SentAt = DateTime.UtcNow
                };

                // 매치별 폴더 확인 및 생성
                string matchFolder = Path.Combine(_messagesPath, matchId);
                if (!Directory.Exists(matchFolder))
                {
                    Directory.CreateDirectory(matchFolder);
                }

                // 메시지 JSON 파일로 저장
                string fileName = $"{message.Id}.json";
                string filePath = Path.Combine(matchFolder, fileName);

                string jsonContent = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                await File.WriteAllTextAsync(filePath, jsonContent);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving text message");
                throw;
            }
        }

        public async Task<List<TextMessage>> GetMessagesByMatchId(string matchId)
        {
            string matchFolder = Path.Combine(_messagesPath, matchId);
            if (!Directory.Exists(matchFolder))
            {
                return new List<TextMessage>();
            }

            var messages = new List<TextMessage>();
            var files = Directory.GetFiles(matchFolder, "*.json").OrderBy(f => f);

            foreach (var file in files)
            {
                try
                {
                    string jsonContent = await File.ReadAllTextAsync(file);
                    var message = JsonSerializer.Deserialize<TextMessage>(jsonContent);
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading message file: {file}");
                }
            }

            return messages.OrderBy(m => m.SentAt).ToList();
        }

        public async Task<List<TextMessage>> GetMessagesByClientId(string clientId)
        {
            if (!Directory.Exists(_messagesPath))
            {
                return new List<TextMessage>();
            }

            var messages = new List<TextMessage>();
            var matchFolders = Directory.GetDirectories(_messagesPath);

            foreach (var matchFolder in matchFolders)
            {
                var files = Directory.GetFiles(matchFolder, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string jsonContent = await File.ReadAllTextAsync(file);
                        var message = JsonSerializer.Deserialize<TextMessage>(jsonContent);
                        if (message != null && (message.SenderId == clientId || message.MatchId.Contains(clientId)))
                        {
                            messages.Add(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error reading message file: {file}");
                    }
                }
            }

            return messages.OrderBy(m => m.SentAt).ToList();
        }
    }
}