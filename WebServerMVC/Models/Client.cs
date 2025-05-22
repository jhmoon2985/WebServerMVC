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
        // 매칭 선호도 필드 추가
        public string PreferredGender { get; set; } = "any"; // "male", "female", "any"
        public int MaxDistance { get; set; } = 10000; // 킬로미터 단위
        // 포인트 시스템 추가
        public int Points { get; set; } = 0;
        public DateTime? PreferenceActiveUntil { get; set; } = null;

        // 선호도 설정이 활성화 상태인지 확인
        public bool IsPreferenceActive => PreferenceActiveUntil.HasValue && PreferenceActiveUntil.Value > DateTime.UtcNow;
    }
}