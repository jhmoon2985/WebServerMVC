﻿using System;
using System.Threading.Tasks;
using WebServerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using WebServerMVC.Utilities;

namespace WebServerMVC.Hubs
{
    //[Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatClientService _clientService;
        private readonly IMatchingService _matchingService;
        private readonly ILogger<ChatHub> _logger;
        private readonly IMessageService _messageService;


        public ChatHub(
            IChatClientService clientService,
            IMatchingService matchingService,
            IMessageService messageService,
            ILogger<ChatHub> logger)
        {
            _clientService = clientService;
            _matchingService = matchingService;
            _messageService = messageService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            //_logger.LogInformation($"Client connected: {Context.ConnectionId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                // 활성 매칭 종료
                await _matchingService.EndMatch(clientId);

                // 클라이언트 상태 업데이트
                var client = await _clientService.GetClientById(clientId);
                if (client != null)
                {
                    // ConnectionId를 비우고 IsMatched 상태 해제
                    client.ConnectionId = string.Empty;
                    client.IsMatched = false;
                    client.MatchedWithClientId = null;

                    // DB에 업데이트
                    try
                    {
                        // UpdateClient 메서드로 변경하여 모든 상태를 한번에 업데이트
                        //await _clientService.UpdateClient(client);
                        await _clientService.UpdateClientAndClearCache(client);
                        //_logger.LogInformation($"Client {clientId} disconnected and state updated in database.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error updating client state on disconnect: {ex.Message}");
                    }
                }

                // 대기열에서 제거
                _matchingService.RemoveFromWaitingQueue(clientId);
            }

            await base.OnDisconnectedAsync(exception);
            //_logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        }

        public async Task Register(object registrationInfo)
        {
            try
            {
                // registrationInfo가 JSON 문자열을 포함하는 경우
                string jsonString = registrationInfo.ToString();

                // 따옴표가 포함된 문자열이면 파싱
                var info = JObject.Parse(jsonString);

                // 클라이언트 ID 추출
                string clientId = info["clientId"]?.ToString();

                // 위치 정보 추출
                double latitude = 37.5642135; // 기본값
                double longitude = 127.0016985; // 기본값

                if (info["latitude"] != null)
                    latitude = info["latitude"].ToObject<double>();

                if (info["longitude"] != null)
                    longitude = info["longitude"].ToObject<double>();

                // 성별 정보 추출
                string gender = "male"; // 기본값

                if (info["gender"] != null)
                    gender = info["gender"].ToString();

                string preferredGender = "any"; // 기본값
                int maxDistance = 10000; // 기본값

                if (info["preferredGender"] != null)
                    preferredGender = info["preferredGender"].ToString();

                if (info["maxDistance"] != null)
                    maxDistance = info["maxDistance"].ToObject<int>();

                // 연결 ID와 클라이언트 ID 매핑
                string connectionId = Context.ConnectionId;

                // 클라이언트 정보 저장 (서비스를 통해)
                clientId = await _clientService.RegisterClient(clientId, connectionId, latitude, longitude, gender, preferredGender, maxDistance);

                // Context.Items에 ClientId 저장 (향후 사용을 위해)
                Context.Items["ClientId"] = clientId;

                // 클라이언트에게 등록 완료 알림
                await Clients.Caller.SendAsync("Registered", new { clientId });

                //_logger.LogInformation($"Client registered: {clientId}, ConnectionId: {connectionId}, Gender: {gender}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Register error: {ex.Message} | StackTrace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("RegisterError", ex.Message);
                throw; // 클라이언트에게 오류 전파
            }
        }
        public async Task UpdateLocation(double latitude, double longitude)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientLocation(clientId, latitude, longitude);
            }
        }

        public async Task UpdateGender(string gender)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientGender(clientId, gender);
            }
        }

        public async Task UpdatePreferences(string preferredGender, int maxDistance)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _clientService.UpdateClientPreferences(clientId, preferredGender, maxDistance);
            }
        }

        public async Task JoinWaitingQueue(double latitude, double longitude, string gender, string preferredGender, int maxDistance)
        {
            var clientId = Context.Items["ClientId"] as string;
            try
            {

                //_logger.LogInformation($"Accessing ClientId from Context.Items: {clientId ?? "null"}");
                if (!string.IsNullOrEmpty(clientId))
                {
                    await _matchingService.AddToWaitingQueue(clientId, Context.ConnectionId, latitude, longitude, gender, preferredGender, maxDistance);

                    // 매칭 프로세스 시작
                    //await _matchingService.ProcessMatchingQueue();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"JoinWaitingQueue error: {ex.Message} | StackTrace: {ex.StackTrace} | clientId: {clientId}");
                await Clients.Caller.SendAsync("JoinWaitingQueueError", ex.Message);
                throw; // 클라이언트에게 오류 전파
            }
        }

        public async Task SendMessage(string message)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                var client = await _clientService.GetClientById(clientId);
                if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
                {
                    var partner = await _clientService.GetClientById(client.MatchedWithClientId);
                    if (partner != null)
                    {
                        // 통일된 그룹 이름 생성 유틸리티 사용 그룹안에 있는 클라이언트 전부에 전송
                        string groupName = ChatUtilities.CreateChatGroupName(client.ClientId, partner.ClientId);
                        // 텍스트 메시지 저장
                        await _messageService.SaveTextMessage(clientId, groupName, message);

                        await Clients.Group(groupName).SendAsync("ReceiveMessage", new
                        {
                            SenderId = clientId,
                            Message = message,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        // Hubs/ChatHub.cs에 추가
        public async Task SendImageMessage(string imageId)
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                var client = await _clientService.GetClientById(clientId);
                if (client != null && client.IsMatched && !string.IsNullOrEmpty(client.MatchedWithClientId))
                {
                    var partner = await _clientService.GetClientById(client.MatchedWithClientId);
                    if (partner != null)
                    {
                        // 이미지 정보 가져오기
                        var imageService = Context.GetHttpContext().RequestServices.GetRequiredService<IImageService>();
                        var imageBytes = await imageService.GetImageBytes(imageId);
                        var thumbnailBytes = await imageService.GetThumbnailBytes(imageId);

                        if (imageBytes != null && thumbnailBytes != null)
                        {
                            // 통일된 그룹 이름 생성 유틸리티 사용
                            string groupName = ChatUtilities.CreateChatGroupName(client.ClientId, partner.ClientId);
                            await Clients.Group(groupName).SendAsync("ReceiveImageMessage", new
                            {
                                SenderId = clientId,
                                ImageId = imageId,
                                ThumbnailUrl = $"/api/image/{imageId}/thumbnail",
                                ImageUrl = $"/api/image/{imageId}",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }

        public async Task EndChat()
        {
            var clientId = Context.Items["ClientId"] as string;
            if (!string.IsNullOrEmpty(clientId))
            {
                await _matchingService.EndMatch(clientId);

                // 다시 대기열에 추가
                var client = await _clientService.GetClientById(clientId);
                if (client != null)
                {
                    await _matchingService.AddToWaitingQueue(clientId, Context.ConnectionId, client.Latitude, client.Longitude, client.Gender, client.PreferredGender, client.MaxDistance);
                    //await _matchingService.ProcessMatchingQueue();
                }
            }
        }
        // ChatHub.cs에 추가
        public async Task GetClientStats()
        {
            var clientService = Context.GetHttpContext().RequestServices.GetRequiredService<IChatClientService>();
            var clients = await clientService.GetAllClients();

            var stats = new
            {
                TotalClients = clients.Count,
                OnlineClients = clients.Count(c => !string.IsNullOrEmpty(c.ConnectionId)),
                MatchedClients = clients.Count(c => c.IsMatched),
                WaitingClients = clients.Count(c => !string.IsNullOrEmpty(c.ConnectionId) && !c.IsMatched)
            };

            await Clients.Caller.SendAsync("ReceiveClientStats", stats);
        }
    }
}