using MongoDB.Driver;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;
using Polymarket.CopyBot.Console.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polymarket.CopyBot.Console.Services
{
    public class TradeMonitorService : BackgroundService
    {
        private readonly AppConfig _config;
        private readonly IPolymarketDataService _dataService;
        private readonly IUserActivityRepository _activityRepo;
        private readonly IUserPositionRepository _positionRepo;
        private readonly ILogger<TradeMonitorService> _logger;

        public TradeMonitorService(
            AppConfig config, 
            IPolymarketDataService dataService,
            IUserActivityRepository activityRepo,
            IUserPositionRepository positionRepo,
            ILogger<TradeMonitorService> logger)
        {
            _config = config;
            _dataService = dataService;
            _activityRepo = activityRepo;
            _positionRepo = positionRepo;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trade Monitor starting for {Count} users...", _config.UserAddresses.Length);

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
            _logger.LogInformation("Processing historical trades...");
            foreach (var address in _config.UserAddresses)
            {
                var collection = _activityRepo.GetCollection(address);
                var filter = Builders<UserActivity>.Filter.Eq(x => x.Bot, false);
                var update = Builders<UserActivity>.Update
                    .Set(x => x.Bot, true)
                    .Set(x => x.BotExecutedTime, 999);

                var result = await collection.UpdateManyAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation("Marked {Count} historical trades as processed for {Address}", result.ModifiedCount, address);
                }
            }
        }

        private async Task FetchTradeData()
        {
            foreach (var address in _config.UserAddresses)
            {
                try
                {
                    // Fetch Activity
                    var activities = await _dataService.GetActivity<UserActivity>(address);
                    if (activities != null && activities.Count > 0)
                    {
                        var collection = _activityRepo.GetCollection(address);
                        
                        foreach (var activity in activities)
                        {
                            if (activity.Timestamp < _config.TooOldTimestamp) continue;

                            // Check if exists
                            var exists = await collection.Find(x => x.TransactionHash == activity.TransactionHash).AnyAsync();
                            if (exists) continue;

                            activity.Bot = false;
                            activity.BotExecutedTime = 0;
                            // Ensure Id is null so Mongo generates it
                            activity.Id = null;

                            await collection.InsertOneAsync(activity);
                            _logger.LogInformation("New trade detected for {Address}: {Hash}", address, activity.TransactionHash);
                        }
                    }

                    // Fetch Positions
                    var positions = await _dataService.GetPositions<UserPosition>(address);
                    if (positions != null && positions.Count > 0)
                    {
                        var collection = _positionRepo.GetCollection(address);
                        
                        foreach (var pos in positions)
                        {
                            var filter = Builders<UserPosition>.Filter.And(
                                Builders<UserPosition>.Filter.Eq(x => x.Asset, pos.Asset),
                                Builders<UserPosition>.Filter.Eq(x => x.ConditionId, pos.ConditionId)
                            );
                            
                            // Upsert
                            pos.Id = null; // Ensure ID is not set for upsert unless we want to replace
                            // Actually for upsert we usually use ReplaceOne with IsUpsert = true
                            // But we need to handle ID carefully. 
                            // simpler: FindOne. If exists, update fields. If not, insert.
                            // Or ReplaceOne with upsert.
                            
                            // Let's use ReplaceOne with Upsert, but we need to ignore ID in replacement 
                            // or fetch the ID if it exists.
                            
                            var existing = await collection.Find(filter).FirstOrDefaultAsync();
                            if (existing != null)
                            {
                                pos.Id = existing.Id;
                                await collection.ReplaceOneAsync(filter, pos);
                            }
                            else
                            {
                                await collection.InsertOneAsync(pos);
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
