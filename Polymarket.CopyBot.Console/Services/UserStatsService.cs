using Microsoft.EntityFrameworkCore;
using Polymarket.CopyBot.Console.Data;
using Polymarket.CopyBot.Console.Models;
using Microsoft.Extensions.Logging;

namespace Polymarket.CopyBot.Console.Services
{
    public interface IUserStatsService
    {
        Task<LeaderboardUser> GetUserStatsAsync(string userAddress);
    }

    public class UserStatsService : IUserStatsService
    {
        private readonly CopyBotDbContext _dbContext;
        private readonly IPolymarketDataService _dataService;
        private readonly ILogger<UserStatsService> _logger;

        public UserStatsService(
            CopyBotDbContext dbContext,
            IPolymarketDataService dataService,
            ILogger<UserStatsService> logger)
        {
            _dbContext = dbContext;
            _dataService = dataService;
            _logger = logger;
        }

        public async Task<LeaderboardUser> GetUserStatsAsync(string userAddress)
        {
            var stats = new LeaderboardUser
            {
                ProxyWallet = userAddress
            };

            if (string.IsNullOrWhiteSpace(userAddress))
                return stats;

            userAddress = userAddress.ToLower();

            try
            {
                // 1. Get max timestamp for user to know where to stop
                // Using Timestamp as the primary sync anchor as PositionId validity is questioned.
                var maxTimestamp = await _dbContext.UserClosedPosInfos
                    .Where(p => p.UserAddress == userAddress)
                    .MaxAsync(p => p.Timestamp) ?? 0;
                
                // 2. Fetch from API until we hit the stop condition (Timestamp <= maxTimestamp)
                var newPositions = new List<UserClosedPosInfo>();
                const int pageSize = 50;
                var offset = 0;
                bool stopReached = false;

                // Safety limit
                int maxPages = 50; 

                while (offset < maxPages * pageSize)
                {
                    var page = await _dataService.GetClosedPositions<ClosedPosition>(userAddress, offset, pageSize);
                    
                    if (page == null || page.Count == 0) break;

                    foreach (var apiPos in page)
                    {
                        if (apiPos.Timestamp == null) continue;

                        // Check stop condition
                        if (apiPos.Timestamp < maxTimestamp)
                        {
                            stopReached = true;
                            break; 
                        }
                        
                        // Handle strict equality overlap:
                        // If Timestamp == maxTimestamp, we might have already imported it.
                        // We check if this specific item exists (by EventSlug + Pnl roughly, or just timestamp + slug)
                        // This helps avoid duplicates if multiple trades settled at the exact same second.
                        if (apiPos.Timestamp == maxTimestamp)
                        {
                            bool alreadyExists = await _dbContext.UserClosedPosInfos.AnyAsync(p => 
                                p.UserAddress == userAddress && 
                                p.Timestamp == apiPos.Timestamp &&
                                p.EventSlug == apiPos.EventSlug &&
                                Math.Abs((p.RealizedPnl ?? 0) - (apiPos.RealizedPnl ?? 0)) < 0.001);

                            if (alreadyExists) 
                            {
                                stopReached = true; 
                                break;
                            }
                        }

                        // Map to entity
                        newPositions.Add(new UserClosedPosInfo
                        {
                            UserAddress = userAddress,
                            PositionId = apiPos.PositionId, // Still storing it but not relying on it for unique key
                            Timestamp = apiPos.Timestamp,
                            EventSlug = apiPos.EventSlug,
                            RealizedPnl = apiPos.RealizedPnl,
                            Title = "" 
                        });
                    }

                    if (stopReached || page.Count < pageSize) break;
                    
                    offset += pageSize;
                }

                // 3. Save new positions to DB
                if (newPositions.Count > 0)
                {
                    // Order by timestamp ascending just to be clean, though not strictly required for insert
                    var toInsert = newPositions.OrderBy(p => p.Timestamp).ToList();
                    await _dbContext.UserClosedPosInfos.AddRangeAsync(toInsert);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Synced {Count} new closed positions for {User}", toInsert.Count, userAddress);
                }

                // 4. Calculate stats from DB (All time)
                var dbPositions = await _dbContext.UserClosedPosInfos
                    .Where(p => p.UserAddress == userAddress)
                    .Select(p => p.RealizedPnl)
                    .ToListAsync();

                int wins = 0;
                int total = 0;

                foreach (var pnl in dbPositions)
                {
                    if (pnl == null) continue;
                    total++;
                    if (pnl >= 0) wins++;
                }

                stats.ClosedPositionsCount = total;
                stats.WinsCount = wins;
                stats.WinPercent = total == 0 ? 0 : Math.Round((double)wins / total * 100.0, 2);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing/calculating stats for {User}", userAddress);
            }

            return stats;
        }
    }
}
