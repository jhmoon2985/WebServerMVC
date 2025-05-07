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

// �α� ����
if (builder.Environment.IsProduction())
{
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));
}

// MVC ���� �߰�
builder.Services.AddControllersWithViews();

// ���� ȯ�濡���� �� ���� ���� Ȱ��ȭ
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddRazorPages();
}

// SignalR ���� �߰�
builder.Services.AddSignalR(options =>
{
    // SignalR ���� ����
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100KB
});

// CORS ����
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

// DB ���� ���
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis ����
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "ChatServer_";
});

// Ŭ���̾�Ʈ ������ Configuration���� �����ͼ� ���񽺿� ���
builder.Services.Configure<ClientSettings>(
    builder.Configuration.GetSection("ClientSettings"));

builder.Services.Configure<MatchingSettings>(
    builder.Configuration.GetSection("MatchingSettings"));

// ���� �� �������丮 ���
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddSingleton<WaitingQueue>();

// ��� �۾� ���� ��� (��Ī ���μ����� �ֱ������� ����)
builder.Services.AddHostedService<MatchingBackgroundService>();

var app = builder.Build();

// ȯ�濡 ���� ���� ������ ����
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

// CORS �̵���� ����
app.UseCors("CorsPolicy");

app.UseAuthorization();

// ��Ʈ�ѷ� �� ��� ��������Ʈ ����
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chathub");

app.Run();