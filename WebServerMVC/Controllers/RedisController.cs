// ====== Controllers/RedisController.cs - ALL ERRORS FIXED ======
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using WebServerMVC.Models;

namespace WebServerMVC.Controllers
{
    [Authorize]
    public class RedisController : Controller
    {
        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisController> _logger;

        public RedisController(IDistributedCache cache, IConnectionMultiplexer redis, ILogger<RedisController> logger)
        {
            _cache = cache;
            _redis = redis;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = new RedisDataViewModel();

            try
            {
                // Redis 연결 상태 확인
                if (!_redis.IsConnected)
                {
                    model.ErrorMessage = "Redis 서버에 연결할 수 없습니다. 서버가 실행 중인지 확인하세요.";
                    return View(model);
                }

                var database = _redis.GetDatabase();
                var endpoints = _redis.GetEndPoints();
                
                if (!endpoints.Any())
                {
                    model.ErrorMessage = "Redis 엔드포인트를 찾을 수 없습니다.";
                    return View(model);
                }

                var server = _redis.GetServer(endpoints.First());

                // Redis 서버 정보
                model.ServerInfo = new RedisServerInfo
                {
                    IsConnected = _redis.IsConnected,
                    Configuration = _redis.Configuration ?? "알 수 없음"
                };

                // 클라이언트 키 패턴으로 검색
                List<RedisKey> clientKeys;
                try
                {
                    clientKeys = server.Keys(pattern: "WebServerMVC_client:*").Take(100).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"클라이언트 키 검색 실패: {ex.Message}");
                    clientKeys = new List<RedisKey>();
                }
                
                foreach (var key in clientKeys)
                {
                    try
                    {
                        var value = await database.StringGetAsync(key);
                        if (value.HasValue)
                        {
                            string stringValue = value.ToString() ?? "";
                            
                            try
                            {
                                // JSON 파싱 시도
                                using var doc = JsonDocument.Parse(stringValue);
                                model.CacheEntries.Add(new RedisCacheEntry
                                {
                                    Key = key.ToString(),
                                    Value = stringValue,
                                    Size = System.Text.Encoding.UTF8.GetByteCount(stringValue),
                                    Type = "Client"
                                });
                            }
                            catch (JsonException)
                            {
                                // JSON이 아닌 경우
                                model.CacheEntries.Add(new RedisCacheEntry
                                {
                                    Key = key.ToString(),
                                    Value = stringValue,
                                    Size = System.Text.Encoding.UTF8.GetByteCount(stringValue),
                                    Type = "Unknown"
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"키 '{key}' 처리 실패: {ex.Message}");
                    }
                }

                // 기타 키들
                List<RedisKey> otherKeys;
                try
                {
                    otherKeys = server.Keys(pattern: "WebServerMVC_*")
                        .Where(k => !k.ToString().Contains("client:"))
                        .Take(50)
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"기타 키 검색 실패: {ex.Message}");
                    otherKeys = new List<RedisKey>();
                }

                foreach (var key in otherKeys)
                {
                    try
                    {
                        var value = await database.StringGetAsync(key);
                        if (value.HasValue)
                        {
                            string stringValue = value.ToString() ?? "";
                            model.CacheEntries.Add(new RedisCacheEntry
                            {
                                Key = key.ToString(),
                                Value = stringValue,
                                Size = System.Text.Encoding.UTF8.GetByteCount(stringValue),
                                Type = "Other"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"키 '{key}' 처리 실패: {ex.Message}");
                    }
                }

                model.TotalKeys = clientKeys.Count + otherKeys.Count;
                model.ClientKeys = clientKeys.Count;
            }
            catch (RedisConnectionException ex)
            {
                model.ErrorMessage = $"Redis 연결 오류: {ex.Message}";
                _logger.LogError(ex, "Redis 연결 오류");
            }
            catch (RedisTimeoutException ex)
            {
                model.ErrorMessage = $"Redis 시간 초과: {ex.Message}";
                _logger.LogError(ex, "Redis 시간 초과");
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"시스템 오류: {ex.Message}";
                _logger.LogError(ex, "Redis Index 처리 중 오류");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                TempData["Error"] = "유효하지 않은 키입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var database = _redis.GetDatabase();
                bool deleted = await database.KeyDeleteAsync(key);

                if (deleted)
                {
                    TempData["Success"] = $"키 '{key}'가 삭제되었습니다.";
                }
                else
                {
                    TempData["Error"] = $"키 '{key}'를 찾을 수 없거나 삭제할 수 없습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"키 삭제 중 오류 발생: {ex.Message}";
                _logger.LogError(ex, $"키 '{key}' 삭제 실패");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAllClientKeys()
        {
            try
            {
                var database = _redis.GetDatabase();
                var endpoints = _redis.GetEndPoints();
                
                if (!endpoints.Any())
                {
                    TempData["Error"] = "Redis 서버에 연결할 수 없습니다.";
                    return RedirectToAction("Index");
                }

                var server = _redis.GetServer(endpoints.First());
                var clientKeys = server.Keys(pattern: "WebServerMVC_client:*").ToArray();
                
                if (clientKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(clientKeys);
                    TempData["Success"] = $"모든 클라이언트 캐시 키({clientKeys.Length}개)가 삭제되었습니다.";
                }
                else
                {
                    TempData["Info"] = "삭제할 클라이언트 키가 없습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"클라이언트 키 삭제 중 오류 발생: {ex.Message}";
                _logger.LogError(ex, "클라이언트 키 일괄 삭제 실패");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAllKeys()
        {
            try
            {
                var database = _redis.GetDatabase();
                var endpoints = _redis.GetEndPoints();
                
                if (!endpoints.Any())
                {
                    TempData["Error"] = "Redis 서버에 연결할 수 없습니다.";
                    return RedirectToAction("Index");
                }

                var server = _redis.GetServer(endpoints.First());
                var allKeys = server.Keys(pattern: "WebServerMVC_*").ToArray();
                
                if (allKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(allKeys);
                    TempData["Success"] = $"모든 애플리케이션 캐시 키({allKeys.Length}개)가 삭제되었습니다.";
                }
                else
                {
                    TempData["Info"] = "삭제할 키가 없습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"키 삭제 중 오류 발생: {ex.Message}";
                _logger.LogError(ex, "모든 키 삭제 실패");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> FlushDatabase()
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                
                if (!endpoints.Any())
                {
                    TempData["Error"] = "Redis 서버에 연결할 수 없습니다.";
                    return RedirectToAction("Index");
                }

                var server = _redis.GetServer(endpoints.First());
                await server.FlushDatabaseAsync();

                TempData["Success"] = "Redis 데이터베이스가 완전히 초기화되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"데이터베이스 초기화 중 오류 발생: {ex.Message}";
                _logger.LogError(ex, "Redis DB 초기화 실패");
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ViewKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return NotFound();
            }

            try
            {
                var database = _redis.GetDatabase();
                var value = await database.StringGetAsync(key);

                if (!value.HasValue)
                {
                    TempData["Error"] = $"키 '{key}'를 찾을 수 없습니다.";
                    return RedirectToAction("Index");
                }

                // RedisValue를 string으로 안전하게 변환
                string stringValue = value.ToString() ?? "";
                
                var model = new RedisKeyDetailViewModel
                {
                    Key = key,
                    Value = stringValue,
                    Size = System.Text.Encoding.UTF8.GetByteCount(stringValue)
                };

                // JSON 형태로 파싱 시도
                try
                {
                    using var doc = JsonDocument.Parse(stringValue);
                    model.FormattedValue = JsonSerializer.Serialize(doc, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    model.IsJson = true;
                }
                catch (JsonException)
                {
                    model.FormattedValue = stringValue;
                    model.IsJson = false;
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"키 조회 중 오류 발생: {ex.Message}";
                _logger.LogError(ex, $"키 '{key}' 조회 실패");
                return RedirectToAction("Index");
            }
        }

        // Redis 연결 상태 확인 API
        [HttpGet]
        public async Task<IActionResult> CheckConnection()
        {
            try
            {
                if (!_redis.IsConnected)
                {
                    return Json(new { connected = false, message = "Redis 서버에 연결되지 않음" });
                }

                var database = _redis.GetDatabase();
                var pingResult = await database.PingAsync();
                
                return Json(new { 
                    connected = true, 
                    message = "Redis 서버 연결 정상",
                    pingTime = pingResult.TotalMilliseconds 
                });
            }
            catch (Exception ex)
            {
                return Json(new { connected = false, message = ex.Message });
            }
        }

        // Redis 서버 정보 API (간소화)
        [HttpGet]
        public async Task<IActionResult> GetServerInfo()
        {
            try
            {
                if (!_redis.IsConnected)
                {
                    return Json(new { error = "Redis 서버에 연결되지 않음" });
                }

                var endpoints = _redis.GetEndPoints();
                if (!endpoints.Any())
                {
                    return Json(new { error = "Redis 엔드포인트를 찾을 수 없음" });
                }

                var server = _redis.GetServer(endpoints.First());
                
                // 기본적인 서버 정보만 반환
                var serverInfo = new
                {
                    isConnected = _redis.IsConnected,
                    endpoint = endpoints.First().ToString(),
                    configuration = _redis.Configuration ?? "알 수 없음",
                    status = "정상"
                };

                return Json(serverInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // 키 검색 API
        [HttpGet]
        public async Task<IActionResult> SearchKeys(string pattern, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    return Json(new { keys = new string[0] });
                }

                if (!_redis.IsConnected)
                {
                    return Json(new { error = "Redis 서버에 연결되지 않음" });
                }

                var endpoints = _redis.GetEndPoints();
                if (!endpoints.Any())
                {
                    return Json(new { error = "Redis 엔드포인트를 찾을 수 없음" });
                }

                var server = _redis.GetServer(endpoints.First());
                var keys = server.Keys(pattern: $"*{pattern}*")
                    .Take(limit)
                    .Select(k => k.ToString())
                    .ToList();

                return Json(new { keys });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}