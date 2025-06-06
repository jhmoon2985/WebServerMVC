﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace WebServerMVC.Services
{
    public class ChatClientService : IChatClientService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IDistributedCache _cache;

        public ChatClientService(IClientRepository clientRepository, IDistributedCache cache)
        {
            _clientRepository = clientRepository;
            _cache = cache;
        }

        public async Task<string> RegisterClient(string clientId, string connectionId, double latitude, double longitude, string gender, string preferredGender, int maxDistance, int points = 0, DateTime? preferenceActiveUntil = null)
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                var client = await GetClientById(clientId);
                if (client != null)
                {
                    client.ConnectionId = connectionId;
                    client.ConnectedAt = DateTime.UtcNow;
                    client.Latitude = latitude;
                    client.Longitude = longitude;
                    client.Gender = gender;
                    client.PreferredGender = preferredGender;
                    client.MaxDistance = maxDistance;

                    // 포인트 정보 서버에 저장
                    if (points > 0)
                    {
                        client.Points = points;
                    }

                    // 선호도 활성화 시간 저장
                    if (preferenceActiveUntil.HasValue)
                    {
                        client.PreferenceActiveUntil = preferenceActiveUntil;
                    }

                    await _clientRepository.UpdateClient(client);

                    // 캐시도 업데이트 - 기존 클라이언트 정보가 변경된 경우
                    await _cache.SetStringAsync($"client:{client.ClientId}",
                        JsonSerializer.Serialize(client)/*,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                        }*/);
                    return clientId;
                }
            }

            var newClient = new Client
            {
                ClientId = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                ConnectedAt = DateTime.UtcNow,
                Latitude = latitude,
                Longitude = longitude,
                Gender = gender,
                IsMatched = false,
                PreferredGender = "any", // 기본값
                MaxDistance = 10000 // 기본값 (km)
            };

            await _clientRepository.AddClient(newClient);

            // Redis에 기본 정보 캐싱
            await _cache.SetStringAsync($"client:{newClient.ClientId}",
                JsonSerializer.Serialize(newClient)/*,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                }*/);

            return newClient.ClientId;
        }

        public async Task<Client> GetClientById(string clientId)
        {
            // 먼저 캐시에서 확인
            var cachedClient = await _cache.GetStringAsync($"client:{clientId}");
            if (!string.IsNullOrEmpty(cachedClient))
            {
                return JsonSerializer.Deserialize<Client>(cachedClient);
            }

            // DB에서 조회
            return await _clientRepository.GetClientById(clientId);
        }
        public async Task UpdateClientAll(string clientId, double latitude, double longitude, string gender, bool IsMatched, string MatchedWithClientId)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.Latitude = latitude;
                client.Longitude = longitude;
                client.Gender = gender;
                client.IsMatched = IsMatched;
                client.MatchedWithClientId = MatchedWithClientId;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client)/*,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }*/);
            }
        }
        public async Task UpdateClientLocation(string clientId, double latitude, double longitude)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.Latitude = latitude;
                client.Longitude = longitude;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client)/*,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }*/);
            }
        }

        public async Task UpdateClientGender(string clientId, string gender)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.Gender = gender;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client)/*,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }*/);
            }
        }
        public async Task UpdateClientMatchClientId(string clientId, string MatchedWithClientId)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.MatchedWithClientId = MatchedWithClientId;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client)/*,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }*/);
            }
        }
        public async Task UpdateClientPreferences(string clientId, string preferredGender, int maxDistance)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.PreferredGender = preferredGender;
                client.MaxDistance = maxDistance;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client)/*,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }*/);
            }
        }
        // 포인트 관련 메서드 추가
        public async Task<int> AddPoints(string clientId, int amount)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.Points += amount;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client));

                return client.Points;
            }

            return 0;
        }

        // 포인트 차감 메서드
        public async Task<bool> SubtractPoints(string clientId, int amount)
        {
            var client = await GetClientById(clientId);
            if (client != null && client.Points >= amount)
            {
                client.Points -= amount;
                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client));

                return true;
            }

            return false;
        }

        // 선호도 활성화 메서드
        public async Task<bool> ActivatePreference(string clientId, string preferredGender, int maxDistance)
        {
            var client = await GetClientById(clientId);
            if (client != null && client.Points >= 1000)
            {
                // 포인트 차감
                client.Points -= 1000;

                // 선호도 설정
                client.PreferredGender = preferredGender;
                client.MaxDistance = maxDistance;

                // 활성화 시간 설정 (10분)
                client.PreferenceActiveUntil = DateTime.UtcNow.AddMinutes(10);

                await _clientRepository.UpdateClient(client);

                // 캐시 업데이트
                await _cache.SetStringAsync($"client:{clientId}",
                    JsonSerializer.Serialize(client));

                return true;
            }

            return false;
        }
        public async Task RemoveClient(string clientId)
        {
            await _clientRepository.DeleteClient(clientId);
            await _cache.RemoveAsync($"client:{clientId}");
        }
        // ClientService.cs에 다음 메서드 구현 추가
        public async Task<List<Client>> GetAllClients()
        {
            // 모든 클라이언트를 리포지토리에서 가져옴
            return await _clientRepository.GetAllClients();
        }
        public async Task UpdateClient(Client client)
        {
            // DB 업데이트
            await _clientRepository.UpdateClient(client);

            // Redis 캐시 업데이트
            await _cache.SetStringAsync($"client:{client.ClientId}",
                JsonSerializer.Serialize(client)/*,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                }*/);
        }
        public async Task UpdateClientAndClearCache(Client client)
        {
            // DB만 업데이트
            await _clientRepository.UpdateClient(client);

            // Redis 캐시에서 삭제
            await _cache.RemoveAsync($"client:{client.ClientId}");
        }
        // ClientService.cs에 추가
        public async Task ClearConnectionId(string clientId)
        {
            var client = await GetClientById(clientId);
            if (client != null)
            {
                client.ConnectionId = string.Empty;
                client.IsMatched = false;
                client.MatchedWithClientId = null;

                await _clientRepository.UpdateClient(client);

                // 캐시에서 제거 (다음 조회 시 DB에서 최신 정보를 가져옴)
                await _cache.RemoveAsync($"client:{clientId}");
            }
        }

        // 모든 오프라인 클라이언트의 ConnectionId 초기화
        public async Task ClearAllOfflineConnections()
        {
            var allClients = await GetAllClients();

            foreach (var client in allClients.Where(c => !string.IsNullOrEmpty(c.ConnectionId)))
            {
                // SignalR 연결 상태 확인 로직이 필요하면 여기에 추가
                client.ConnectionId = string.Empty;
                client.IsMatched = false;
                client.MatchedWithClientId = null;

                await _clientRepository.UpdateClient(client);
                await _cache.RemoveAsync($"client:{client.ClientId}");
            }
        }
    }
}