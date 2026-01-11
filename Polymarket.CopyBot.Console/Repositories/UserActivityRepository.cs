using Microsoft.EntityFrameworkCore;
using Polymarket.CopyBot.Console.Data;
using Polymarket.CopyBot.Console.Models;

namespace Polymarket.CopyBot.Console.Repositories
{
    public interface IUserActivityRepository
    {
        Task AddAsync(UserActivity activity);
        Task UpdateAsync(UserActivity activity);
        Task<UserActivity?> GetByIdAsync(string id);
        Task<UserActivity?> GetByTxHashAsync(string address, string txHash);
        Task<List<UserActivity>> GetUnprocessedAsync(string address);
        Task MarkAllHistoricalAsProcessedAsync(string address);
    }

    public class UserActivityRepository : IUserActivityRepository
    {
        private readonly CopyBotDbContext _context;

        public UserActivityRepository(CopyBotDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserActivity activity)
        {
            await _context.UserActivities.AddAsync(activity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserActivity activity)
        {
            _context.UserActivities.Update(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<UserActivity?> GetByIdAsync(string id)
        {
            return await _context.UserActivities.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<UserActivity?> GetByTxHashAsync(string address, string txHash)
        {
            return await _context.UserActivities
                .FirstOrDefaultAsync(x => x.OwnerAddress == address && x.TransactionHash == txHash);
        }

        public async Task<List<UserActivity>> GetUnprocessedAsync(string address)
        {
            return await _context.UserActivities
                .Where(x => x.OwnerAddress == address && 
                            x.Type == "TRADE" && 
                            x.Bot == false && 
                            x.BotExecutedTime == 0)
                .ToListAsync();
        }

        public async Task MarkAllHistoricalAsProcessedAsync(string address)
        {
            var historical = await _context.UserActivities
                .Where(x => x.OwnerAddress == address && x.Bot == false)
                .ToListAsync();

            foreach (var item in historical)
            {
                item.Bot = true;
                item.BotExecutedTime = 999;
            }
            
            if (historical.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
