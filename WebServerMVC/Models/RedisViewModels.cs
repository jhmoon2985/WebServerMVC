namespace WebServerMVC.Models
{
    public class RedisDataViewModel
    {
        public List<RedisCacheEntry> CacheEntries { get; set; } = new List<RedisCacheEntry>();
        public RedisServerInfo ServerInfo { get; set; } = new RedisServerInfo();
        public int TotalKeys { get; set; }
        public int ClientKeys { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RedisCacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int Size { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class RedisServerInfo
    {
        public bool IsConnected { get; set; }
        public string Configuration { get; set; } = string.Empty;
    }

    public class RedisKeyDetailViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string FormattedValue { get; set; } = string.Empty;
        public int Size { get; set; }
        public bool IsJson { get; set; }
    }
}
