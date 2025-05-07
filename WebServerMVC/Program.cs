using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Serilog;
using System;
using WebServerMVC.Data;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Repositories;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services;
using WebServerMVC.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 로깅 설정
if (builder.Environment.IsProduction())
{
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));
}

// MVC 서비스 추가
builder.Services.AddControllersWithViews();

// 개발 환경에서만 상세 오류 정보 활성화
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddRazorPages();
}

// SignalR 서비스 추가
builder.Services.AddSignalR(options =>
{
    // SignalR 관련 설정
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100KB
});

// CORS 설정
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// DB 서비스 등록
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis 설정
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "ChatServer_";
});

// 클라이언트 설정을 Configuration에서 가져와서 서비스에 등록
builder.Services.Configure<ClientSettings>(
    builder.Configuration.GetSection("ClientSettings"));

builder.Services.Configure<MatchingSettings>(
    builder.Configuration.GetSection("MatchingSettings"));

// 서비스 및 리포지토리 등록
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddSingleton<WaitingQueue>();

// 배경 작업 서비스 등록 (매칭 프로세스를 주기적으로 실행)
builder.Services.AddHostedService<MatchingBackgroundService>();

var app = builder.Build();

// 환경에 따른 오류 페이지 설정
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

// CORS 미들웨어 설정
app.UseCors("CorsPolicy");

app.UseAuthorization();

// 컨트롤러 및 허브 엔드포인트 매핑
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chathub");

app.Run();