
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;
using Polymarket.CopyBot.Console.Services;
using Polymarket.CopyBot.Console.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polymarket.CopyBot.Console.Services
{
    public class TradeMonitorService : BackgroundService
    {
        private readonly AppConfig _config;
        private readonly IPolymarketDataService _dataService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TradeMonitorService> _logger;

        public TradeMonitorService(
            AppConfig config, 
            IPolymarketDataService dataService,
            IServiceScopeFactory scopeFactory,
            ILogger<TradeMonitorService> logger)
        {
            _config = config;
            _dataService = dataService;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trade Monitor starting...");

            // First run: Mark historical trades as processed
            await ProcessHistoricalTrades();

            while (!stoppingToken.IsCancellationRequested)
            {
                await FetchTradeData();
                await Task.Delay(_config.FetchInterval * 1000, stoppingToken);
            }
            
            _logger.LogInformation("Trade Monitor stopped.");
        }

        private async Task ProcessHistoricalTrades()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                 var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();
                 var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorUserService>();
                 
                 var users = await monitorService.GetMonitoredUsersAsync();
                 var addresses = users.Select(u => u.Address).ToList();

                _logger.LogInformation("Processing historical trades for {Count} users...", addresses.Count);
                foreach (var address in addresses)
                {
                    await activityRepo.MarkAllHistoricalAsProcessedAsync(address);
                    _logger.LogInformation("Marked historical trades as processed for {Address}", address);
                }
            }
        }

        private async Task FetchTradeData()
        {
            List<string> addresses;
             using (var scope = _scopeFactory.CreateScope())
            {
                var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorUserService>();
                var users = await monitorService.GetMonitoredUsersAsync();
                addresses = users.Select(u => u.Address).ToList();
            }

            foreach (var address in addresses)
            {
                try
                {
                    // Fetch Activity
                    var activities = await _dataService.GetActivity<UserActivity>(address);
                    
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();
                        var positionRepo = scope.ServiceProvider.GetRequiredService<IUserPositionRepository>();

                        if (activities != null && activities.Count > 0)
                        {
                            foreach (var activity in activities)
                            {
                                if (activity.Timestamp < _config.TooOldTimestamp) continue;

                                // Check if exists
                                var exists = await activityRepo.GetByTxHashAsync(address, activity.TransactionHash ?? "");
                                if (exists != null) continue;

                                activity.Bot = false;
                                activity.BotExecutedTime = 0;
                                activity.OwnerAddress = address; // Set partition key
                                activity.Id = Guid.NewGuid().ToString(); // Generate ID

                                await activityRepo.AddAsync(activity);
                                _logger.LogInformation("New trade detected for {Address}: {Hash}", address, activity.TransactionHash);
                            }
                        }

                        // Fetch Positions
                        var positions = await _dataService.GetPositions<UserPosition>(address);
                        if (positions != null && positions.Count > 0)
                        {
                            foreach (var pos in positions)
                            {
                                pos.OwnerAddress = address; // Set partition key
                                // Id managed by repo upsert logic if new, or preserved
                                if (string.IsNullOrEmpty(pos.Id)) pos.Id = Guid.NewGuid().ToString();

                                await positionRepo.UpsertAsync(pos);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching data for {Address}", address);
                }
            }
        }
    }
}
