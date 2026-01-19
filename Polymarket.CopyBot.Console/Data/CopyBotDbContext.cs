using Microsoft.EntityFrameworkCore;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;

namespace Polymarket.CopyBot.Console.Data
{
    public class CopyBotDbContext : DbContext
    {
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<UserPosition> UserPositions { get; set; }
        public DbSet<MonitorUser> MonitorUsers { get; set; }
        public DbSet<UserClosedPosInfo> UserClosedPosInfos { get; set; }

        // Constructor for DI
        public CopyBotDbContext(DbContextOptions<CopyBotDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Indexing for performance
            modelBuilder.Entity<UserActivity>()
                .HasIndex(u => u.OwnerAddress);
                
            modelBuilder.Entity<UserActivity>()
                .HasIndex(u => u.TransactionHash);

            modelBuilder.Entity<UserPosition>()
                .HasIndex(u => u.OwnerAddress);

            modelBuilder.Entity<UserClosedPosInfo>()
                .HasIndex(u => u.UserAddress);
            
            // Indexing Timestamp for sync logic
            modelBuilder.Entity<UserClosedPosInfo>()
                .HasIndex(u => u.Timestamp);
        }
    }
}
