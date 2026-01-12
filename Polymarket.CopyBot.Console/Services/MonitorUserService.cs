using Microsoft.EntityFrameworkCore;
using Polymarket.CopyBot.Console.Data;
using Polymarket.CopyBot.Console.Models;

namespace Polymarket.CopyBot.Console.Services
{
    public interface IMonitorUserService
    {
        Task<List<MonitorUser>> GetMonitoredUsersAsync();
        Task AddUserAsync(string address, string? name = null);
        Task RemoveUserAsync(string address);
    }

    public class MonitorUserService : IMonitorUserService
    {
        private readonly CopyBotDbContext _context;

        public MonitorUserService(CopyBotDbContext context)
        {
            _context = context;
        }

        public async Task<List<MonitorUser>> GetMonitoredUsersAsync()
        {
            return await _context.MonitorUsers.ToListAsync();
            }

        public async Task AddUserAsync(string address, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            var existing = await _context.MonitorUsers.FindAsync(address);
            if (existing == null)
            {
                _context.MonitorUsers.Add(new MonitorUser
                {
                    Address = address,
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveUserAsync(string address)
        {
            var user = await _context.MonitorUsers.FindAsync(address);
            if (user != null)
            {
                _context.MonitorUsers.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}
