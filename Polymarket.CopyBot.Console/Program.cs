using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http; // Add this using directive
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
            var host = CreateHostBuilder(args).Build();
            
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Polymarket.CopyBot.Console.Data.CopyBotDbContext>();
                db.Database.EnsureCreated();
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Load Configuration
                    var config = AppConfig.LoadFromEnv();
                    services.AddSingleton(config);

                    // Logging
                    services.AddLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                    });

                    services.AddDbContext<Polymarket.CopyBot.Console.Data.CopyBotDbContext>(options =>
                        options.UseSqlite(config.SqliteConnectionString));

                    // Repositories
                    services.AddScoped<IUserActivityRepository, UserActivityRepository>();
                    services.AddScoped<IUserPositionRepository, UserPositionRepository>();

                    // Services
                    services.AddHttpClient<PolymarketDataService>();
                    services.AddSingleton<IPolymarketDataService, PolymarketDataService>();
                    
                    services.AddSingleton<CopyStrategyService>();

                    // Hosted Services
                    services.AddHostedService<TradeMonitorService>();
                    services.AddHostedService<TradeExecutorService>();
                });
    }
}
