using System;
using System.Threading;
using System.Threading.Tasks;
using WebServerMVC.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebServerMVC.Services
{
    public class MatchingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MatchingBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private int _intervalSeconds;

        public MatchingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MatchingBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _intervalSeconds = _configuration.GetValue<int>("MatchingSettings:MatchProcessingIntervalSeconds", 5);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Matching Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Matching process running at: {time}", DateTimeOffset.Now);

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var matchingService = scope.ServiceProvider.GetRequiredService<IMatchingService>();
                        await matchingService.ProcessMatchingQueue();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing matching queue.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Matching Background Service is stopping.");
        }
    }
}