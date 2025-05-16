using Microsoft.EntityFrameworkCore;
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

// DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "WebServerMVC_";
});

// �̱��� ����
builder.Services.AddSingleton<WaitingQueue>();

// ������ ����
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// ���� ���ε�
builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection("ClientSettings"));
builder.Services.Configure<MatchingSettings>(builder.Configuration.GetSection("MatchingSettings"));

// ��׶��� ����
builder.Services.AddHostedService<MatchingBackgroundService>();

// CORS ����
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins(allowedOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
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
app.UseCors("CorsPolicy");

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