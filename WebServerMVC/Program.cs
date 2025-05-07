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

// �α� ����
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ���� ���
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// �����ͺ��̽� ���ؽ�Ʈ ���
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

// Redis ĳ�� ���
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "WebServerMVC:";
});

// CORS ��å ����
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

// �̱��� ���� ���
builder.Services.AddSingleton<WaitingQueue>();

// ������ ���� ���
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// ��׶��� ���� ���
builder.Services.AddHostedService<MatchingBackgroundService>();

// �� ����
builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection("ClientSettings"));
builder.Services.Configure<MatchingSettings>(builder.Configuration.GetSection("MatchingSettings"));

var app = builder.Build();

// ȯ�� ����
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

// �����ͺ��̽� �ʱ�ȭ
InitializeDatabase(app);

app.Run();

// �����ͺ��̽� �ʱ�ȭ �޼���
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

            logger.LogInformation("�����ͺ��̽� �ʱ�ȭ�� �����մϴ�...");
            await DbInitializer.Initialize(context);
            logger.LogInformation("�����ͺ��̽� �ʱ�ȭ�� �Ϸ�Ǿ����ϴ�.");
        }
        catch (Exception ex)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "�����ͺ��̽� �ʱ�ȭ �� ������ �߻��߽��ϴ�.");
        }
    }).Wait(); // ���������� �����Ͽ� ���ø����̼� ���� ���� �ʱ�ȭ �Ϸ�
}