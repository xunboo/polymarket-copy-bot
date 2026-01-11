using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http; // Add this using directive
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Repositories;
using Polymarket.CopyBot.Console.Services;
using MongoDB.Driver;

namespace Polymarket.CopyBot.Console
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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

                    // MongoDB
                    services.AddSingleton<IMongoClient>(sp => new MongoClient(config.MongoUri));
                    services.AddSingleton<IMongoDatabase>(sp => 
                    {
                        var client = sp.GetRequiredService<IMongoClient>();
                        // Parse DB name from URI or default
                        var url = MongoUrl.Create(config.MongoUri);
                        return client.GetDatabase(url.DatabaseName ?? "polymarket_copytrading");
                    });

                    // Repositories
                    services.AddSingleton<IUserActivityRepository, UserActivityRepository>();
                    services.AddSingleton<IUserPositionRepository, UserPositionRepository>();

                    // Services
                    services.AddHttpClient<PolymarketDataService>();
                    services.AddSingleton<IPolymarketDataService, PolymarketDataService>(); // Forward registration if needed or just use typed
                    
                    services.AddSingleton<CopyStrategyService>();

                    // Hosted Services
                    services.AddHostedService<TradeMonitorService>();
                    services.AddHostedService<TradeExecutorService>();
                });
    }
}
