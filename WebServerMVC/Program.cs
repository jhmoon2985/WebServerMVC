using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using StackExchange.Redis;
using WebServerMVC.Data;
using WebServerMVC.Hubs;
using WebServerMVC.Models;
using WebServerMVC.Repositories;
using WebServerMVC.Repositories.Interfaces;
using WebServerMVC.Services;
using WebServerMVC.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// HttpClient 추가 (인앱결제 API 호출용)
builder.Services.AddHttpClient();

// Authentication 추가
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "WebServerMVC_";
});

// Redis ConnectionMultiplexer 등록 (Redis 관리용)
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("RedisConnection");
    return ConnectionMultiplexer.Connect(configuration);
});

// �̱��� ����
builder.Services.AddSingleton<WaitingQueue>();

// ������ ����
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IInAppPurchaseRepository, InAppPurchaseRepository>(); // 추가

builder.Services.AddScoped<IChatClientService, ChatClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// 인앱결제 서비스 등록 (Google Mock + OneStore 실제)
builder.Services.AddScoped<MockGooglePlayService>();
builder.Services.AddScoped<OneStoreService>();
builder.Services.AddScoped<IInAppPurchaseService, HybridInAppPurchaseService>();
// 실제 Google Play API 사용 시 주석 해제
// builder.Services.AddScoped<GooglePlayService>(); 
// builder.Services.AddScoped<GooglePlayService>();
// builder.Services.AddScoped<OneStoreService>();
// builder.Services.AddScoped<IInAppPurchaseService, OptimizedInAppPurchaseService>(); // 추가

// HttpClient 설정 (재시도 정책 포함)
builder.Services.AddHttpClient<GooglePlayService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "WebServerMVC/1.0");
});

builder.Services.AddHttpClient<OneStoreService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "WebServerMVC/1.0");
});

// ���� ���ε�
builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection("ClientSettings"));
builder.Services.Configure<MatchingSettings>(builder.Configuration.GetSection("MatchingSettings"));

// ��׶��� ����
builder.Services.AddHostedService<MatchingBackgroundService>();

// CORS ����
/*var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins(allowedOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});*/

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
//app.UseCors("CorsPolicy");
app.UseCors("AllowAll");

// Authentication & Authorization 미들웨어 추가
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chathub");

// ���̱׷��̼� ���� �� �õ� ������ �ʱ�ȭ
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // ���̱׷��̼� ����
        context.Database.Migrate();

        // �õ� ������ �ʱ�ȭ
        await DbInitializer.SeedData(context);

        var logger = services.GetRequiredService<ILogger<Program>>();
        //logger.LogInformation("�����ͺ��̽� ���̱׷��̼� �� �õ� ������ �ʱ�ȭ�� �Ϸ�Ǿ����ϴ�.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "�����ͺ��̽� ���̱׷��̼� �Ǵ� �õ� ������ �ʱ�ȭ �� ������ �߻��߽��ϴ�.");
    }
}

// 이미지 저장 디렉토리 확인 및 생성
var uploadsDir = Path.Combine(app.Environment.WebRootPath, "uploads");
var imagesDir = Path.Combine(uploadsDir, "images");
var thumbnailsDir = Path.Combine(uploadsDir, "thumbnails");
var clientsDir = Path.Combine(uploadsDir, "clients");
var messagesDir = Path.Combine(uploadsDir, "messages"); // 추가

if (!Directory.Exists(uploadsDir))
    Directory.CreateDirectory(uploadsDir);
if (!Directory.Exists(imagesDir))
    Directory.CreateDirectory(imagesDir);
if (!Directory.Exists(thumbnailsDir))
    Directory.CreateDirectory(thumbnailsDir);
if (!Directory.Exists(clientsDir))
    Directory.CreateDirectory(clientsDir);
if (!Directory.Exists(messagesDir))
    Directory.CreateDirectory(messagesDir);

app.Run();