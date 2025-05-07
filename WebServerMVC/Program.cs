using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WebServerMVC.Data;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Repositories;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services;
using WebServerMVC.Services.Interfaces;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 로깅 설정
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 서비스 등록
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// 데이터베이스 컨텍스트 등록
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("WebServerMVC")
    );
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// Redis 캐시 등록
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "WebServerMVC:";
});

// CORS 정책 설정
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", builder =>
    {
        builder.WithOrigins(allowedOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// 싱글톤 서비스 등록
builder.Services.AddSingleton<WaitingQueue>();

// 스코프 서비스 등록
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// 백그라운드 서비스 등록
builder.Services.AddHostedService<MatchingBackgroundService>();

// 앱 설정
builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection("ClientSettings"));
builder.Services.Configure<MatchingSettings>(builder.Configuration.GetSection("MatchingSettings"));

var app = builder.Build();

// 환경 설정
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowedOrigins");
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chathub");

// 데이터베이스 초기화
InitializeDatabase(app);

app.Run();

// 데이터베이스 초기화 메서드
void InitializeDatabase(WebApplication app)
{
    Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<AppDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("데이터베이스 초기화를 시작합니다...");
            await DbInitializer.Initialize(context);
            logger.LogInformation("데이터베이스 초기화가 완료되었습니다.");
        }
        catch (Exception ex)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "데이터베이스 초기화 중 오류가 발생했습니다.");
        }
    }).Wait(); // 동기적으로 실행하여 애플리케이션 시작 전에 초기화 완료
}