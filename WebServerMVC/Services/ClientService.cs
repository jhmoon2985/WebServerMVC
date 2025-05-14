using System;
using System.Text.Json;
using System.Threading.Tasks;
using WebServerMVC.Models;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace WebServerMVC.Services
{
    public class ClientService : IClientService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IDistributedCache _cache;

        public ClientService(IClientRepository clientRepository, IDistributedCache cache)
        {
            _clientRepository = clientRepository;
            _cache = cache;
        }

        public async Task<string> RegisterClient(string clientId, string connectionId, double latitude, double longitude, string gender, string preferredGender, int maxDistance)
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
    }
}