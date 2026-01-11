using Microsoft.EntityFrameworkCore;
using Polymarket.CopyBot.Console.Data;
using Polymarket.CopyBot.Console.Models;

namespace Polymarket.CopyBot.Console.Repositories
{
    public interface IUserPositionRepository
    {
        Task UpsertAsync(UserPosition position);
        Task<UserPosition?> GetByAssetAsync(string address, string asset);
    }

    public class UserPositionRepository : IUserPositionRepository
    {
        private readonly CopyBotDbContext _context;

        public UserPositionRepository(CopyBotDbContext context)
        {
            _context = context;
        }

        public async Task<UserPosition?> GetByAssetAsync(string address, string asset)
        {
            return await _context.UserPositions
                .FirstOrDefaultAsync(x => x.OwnerAddress == address && x.Asset == asset);
        }

        public async Task UpsertAsync(UserPosition position)
        {
            var existing = await _context.UserPositions
                .FirstOrDefaultAsync(x => x.OwnerAddress == position.OwnerAddress && 
                                          x.Asset == position.Asset && 
                                          x.ConditionId == position.ConditionId);
            
            if (existing != null)
            {
                // Update properties
                existing.Size = position.Size;
                existing.AvgPrice = position.AvgPrice;
                existing.CurrentValue = position.CurrentValue;
                existing.PercentPnl = position.PercentPnl;
                // ... map other fields if needed or just use current values
                
                _context.UserPositions.Update(existing);
            }
            else
            {
                await _context.UserPositions.AddAsync(position);
            }
            
            await _context.SaveChangesAsync();
        }
    }
}
