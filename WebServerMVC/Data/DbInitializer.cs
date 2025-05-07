using WebServerMVC.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WebServerMVC.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // 데이터베이스 자동 생성
            await context.Database.EnsureCreatedAsync();

            // 클라이언트 데이터가 없는 경우에만 샘플 데이터 추가
            if (!context.Clients.Any())
            {
                var clients = new Client[]
                {
                    new Client
                    {
                        ClientId = Guid.NewGuid().ToString(),
                        ConnectionId = string.Empty,
                        ConnectedAt = DateTime.UtcNow.AddMinutes(-30),
                        Gender = "male",
                        Latitude = 37.5642135,
                        Longitude = 127.0016985,
                        IsMatched = false
                    },
                    new Client
                    {
                        ClientId = Guid.NewGuid().ToString(),
                        ConnectionId = string.Empty,
                        ConnectedAt = DateTime.UtcNow.AddMinutes(-25),
                        Gender = "female",
                        Latitude = 37.5642135,
                        Longitude = 127.0016985,
                        IsMatched = false
                    }
                };

                context.Clients.AddRange(clients);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.InnerException?.Message);
                    throw;
                }
            }
        }
    }
}