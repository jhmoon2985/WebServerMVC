// Models/ImageMessage.cs
using System;

namespace WebServerMVC.Models
{
    public class ImageMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; }
        public string MatchId { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}