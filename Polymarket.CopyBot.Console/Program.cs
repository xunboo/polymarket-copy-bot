using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http; // Add this using directive
using System.IO;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Repositories;
using Polymarket.CopyBot.Console.Services;
using Microsoft.EntityFrameworkCore;

namespace Polymarket.CopyBot.Console
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // If the persisted log file grows too large, clear it before starting
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "log.txt");
                if (!File.Exists(logPath))
                {
                    // fall back to current directory
                    logPath = Path.Combine(Directory.GetCurrentDirectory(), "log.txt");
                }

                const long maxBytes = 100L * 1024 * 1024; // 100 MB
                if (File.Exists(logPath))
                {
                    var fi = new FileInfo(logPath);
                    if (fi.Length > maxBytes)
                    {
                        // Truncate/clear the file so Serilog can start fresh
                        File.WriteAllText(logPath, string.Empty);
                        Log.Debug($"Cleared '{logPath}' because it exceeded {maxBytes} bytes.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't crash startup because of log maintenance. Just report to console.
                Log.Debug($"Warning: could not enforce log size limit: {ex.Message}");
            }

            var host = CreateHostBuilder(args).Build();
            
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Polymarket.CopyBot.Console.Data.CopyBotDbContext>();
                db.Database.EnsureCreated();
                
                // Manual migration hack for MonitorUsers table since EnsureCreated doesn't update existing DBs
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""MonitorUsers"" (
                        ""Address"" TEXT NOT NULL CONSTRAINT ""PK_MonitorUsers"" PRIMARY KEY,
                        ""Name"" TEXT NULL,
                        ""CreatedAt"" TEXT NOT NULL
                    );");

                // Manual migration for UserClosedPosInfos
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""UserClosedPosInfos"" (
                        ""Id"" TEXT NOT NULL CONSTRAINT ""PK_UserClosedPosInfos"" PRIMARY KEY,
                        ""UserAddress"" TEXT NULL,
                        ""PositionId"" TEXT NULL,
                        ""Timestamp"" INTEGER NULL,
                        ""EventSlug"" TEXT NULL,
                        ""RealizedPnl"" REAL NULL,
                        ""Title"" TEXT NULL
                    );");
                
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_UserClosedPosInfos_UserAddress"" ON ""UserClosedPosInfos"" (""UserAddress"");");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_UserClosedPosInfos_Timestamp"" ON ""UserClosedPosInfos"" (""Timestamp"");");
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, services, configuration) => configuration
                    .MinimumLevel.Information() // Default level for everything (File gets this)
                    .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning) // Console gets Warning+
                    .WriteTo.File("log.txt") // File gets Information+
                )
                .ConfigureServices((hostContext, services) =>
                {
                    // Load Configuration
                    var config = AppConfig.LoadFromEnv();
                    services.AddSingleton(config);

                    // Logging
                    // Logging
                    // Replaced by Serilog in Host.CreateDefaultBuilder chain (see below) or mapped here if using Serilog.Extensions.Logging
                    // But typically UseSerilog is on HostBuilder.
                    // For minimal changes let's use UseSerilog on the builder below but we are inside ConfigureServices here.
                    // Actually, the standard way is:
                    // Host.CreateDefaultBuilder(args).UseSerilog(...)
                    
                    // So we will remove this AddLogging block and add UseSerilog outside ConfigureServices.
                    // So we will remove this AddLogging block and add UseSerilog outside ConfigureServices.

                    services.AddDbContext<Polymarket.CopyBot.Console.Data.CopyBotDbContext>(options =>
                        options.UseSqlite(config.SqliteConnectionString));

                    // Repositories
                    services.AddScoped<IUserActivityRepository, UserActivityRepository>();
                    services.AddScoped<IUserPositionRepository, UserPositionRepository>();
                    services.AddScoped<IMonitorUserService, MonitorUserService>();

                    // Services
                    services.AddHttpClient<PolymarketDataService>();
                    services.AddSingleton<IPolymarketDataService, PolymarketDataService>();
                    services.AddScoped<IUserStatsService, UserStatsService>();
                    
                    services.AddSingleton<CopyStrategyService>();

                    // Hosted Services
                    services.AddHostedService<TradeMonitorService>();
                    
                    if (config.RunTradeExecutor)
                    {
                        services.AddHostedService<TradeExecutorService>();
                    }
                    else
                    {
                        Log.Information("Trade Executor Disabled via Config");
                    }

                    services.AddHostedService<UsersWebService>();
                });
    }
}
