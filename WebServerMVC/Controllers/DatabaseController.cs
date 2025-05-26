using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebServerMVC.Data;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;

namespace WebServerMVC.Controllers
{
    [Authorize]
    public class DatabaseController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IChatClientService _clientService;

        public DatabaseController(AppDbContext context, IChatClientService clientService)
        {
            _context = context;
            _clientService = clientService;
        }

        public async Task<IActionResult> Index()
        {
            var stats = new DatabaseStatsViewModel
            {
                ClientsCount = await _context.Clients.CountAsync(),
                MatchesCount = await _context.Matches.CountAsync(),
                ActiveMatchesCount = await _context.Matches.CountAsync(m => m.EndedAt == null),
                OnlineClientsCount = await _context.Clients.CountAsync(c => !string.IsNullOrEmpty(c.ConnectionId)),
                TotalPoints = await _context.Clients.SumAsync(c => c.Points)
            };

            return View(stats);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteClient(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                TempData["Error"] = "유효하지 않은 클라이언트 ID입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var client = await _context.Clients.FindAsync(clientId);
                if (client != null)
                {
                    // 관련 매치 기록도 삭제
                    var matches = await _context.Matches
                        .Where(m => m.ClientId1 == clientId || m.ClientId2 == clientId)
                        .ToListAsync();

                    _context.Matches.RemoveRange(matches);
                    _context.Clients.Remove(client);
                    
                    await _context.SaveChangesAsync();

                    // Redis 캐시에서도 삭제
                    await _clientService.RemoveClient(clientId);

                    TempData["Success"] = $"클라이언트 {clientId}가 삭제되었습니다.";
                }
                else
                {
                    TempData["Error"] = "클라이언트를 찾을 수 없습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"클라이언트 삭제 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAllClients()
        {
            try
            {
                var allClients = await _context.Clients.ToListAsync();
                var allMatches = await _context.Matches.ToListAsync();

                _context.Matches.RemoveRange(allMatches);
                _context.Clients.RemoveRange(allClients);

                await _context.SaveChangesAsync();

                // Redis 캐시도 모두 삭제
                foreach (var client in allClients)
                {
                    await _clientService.RemoveClient(client.ClientId);
                }

                TempData["Success"] = $"모든 클라이언트 데이터({allClients.Count}개)가 삭제되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"데이터 삭제 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMatch(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                TempData["Error"] = "유효하지 않은 매치 ID입니다.";
                return RedirectToAction("Index");
            }

            try
            {
                var match = await _context.Matches.FindAsync(matchId);
                if (match != null)
                {
                    _context.Matches.Remove(match);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"매치 {matchId}가 삭제되었습니다.";
                }
                else
                {
                    TempData["Error"] = "매치를 찾을 수 없습니다.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"매치 삭제 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAllMatches()
        {
            try
            {
                var allMatches = await _context.Matches.ToListAsync();
                _context.Matches.RemoveRange(allMatches);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"모든 매치 데이터({allMatches.Count}개)가 삭제되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"매치 데이터 삭제 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ClearOfflineClients()
        {
            try
            {
                var offlineClients = await _context.Clients
                    .Where(c => string.IsNullOrEmpty(c.ConnectionId))
                    .ToListAsync();

                var relatedMatches = await _context.Matches
                    .Where(m => offlineClients.Any(c => c.ClientId == m.ClientId1 || c.ClientId == m.ClientId2))
                    .ToListAsync();

                _context.Matches.RemoveRange(relatedMatches);
                _context.Clients.RemoveRange(offlineClients);

                await _context.SaveChangesAsync();

                // Redis 캐시에서도 삭제
                foreach (var client in offlineClients)
                {
                    await _clientService.RemoveClient(client.ClientId);
                }

                TempData["Success"] = $"오프라인 클라이언트 {offlineClients.Count}개가 삭제되었습니다.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"오프라인 클라이언트 삭제 중 오류 발생: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}