using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Polymarket.CopyBot.Console.Services
{
    public class UsersWebService : BackgroundService
    {
        private readonly ILogger<UsersWebService> _logger;
        private readonly AppConfig _config;
        private readonly IPolymarketDataService _dataService;
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private WebApplication? _app;

        public UsersWebService(
            ILogger<UsersWebService> logger, 
            AppConfig config, 
            IPolymarketDataService dataService,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _config = config;
            _dataService = dataService;
            _scopeFactory = scopeFactory;
            var cacheOptions = new MemoryCacheOptions();
            _cache = new MemoryCache(cacheOptions);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                
                // Configure Kestrel to listen on all interfaces
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(5000);
                });

                _app = builder.Build();

                _app.MapGet("/api/users", async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorUserService>();
                        var users = await monitorService.GetMonitoredUsersAsync();
                        return Results.Json(users.Select(u => u.Address).ToList());
                    }
                });

                _app.MapPost("/api/users", async (AddUserRequest request) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Address)) return Results.BadRequest("Address is required");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorUserService>();
                        await monitorService.AddUserAsync(request.Address, request.Name);
                        return Results.Ok();
                    }
                });

                _app.MapGet("/api/leaderboard", async (HttpContext context) =>
                {
                    string timePeriod = context.Request.Query["timePeriod"].ToString();
                    if (string.IsNullOrEmpty(timePeriod)) timePeriod = "MONTH";

                    // Cache key based on timePeriod
                    string cacheKey = $"leaderboard_{timePeriod.ToUpper()}";

                    if (!_cache.TryGetValue(cacheKey, out List<LeaderboardUser>? users))
                    {
                        try 
                        {
                            users = await _dataService.GetLeaderboardAsync(timePeriod);
                            
                            // Cache for 5 minutes
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                            _cache.Set(cacheKey, users, cacheEntryOptions);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to fetch leaderboard for {TimePeriod}", timePeriod);
                            return Results.Problem("Failed to fetch leaderboard data");
                        }
                    }

                    return Results.Json(users);
                });

                _app.MapGet("/", async (HttpContext context) =>
                {
                    context.Response.ContentType = "text/html";
                    // In a Worker Service, checking output directory for the file
                    var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
                    if (File.Exists(filePath))
                    {
                        var html = await File.ReadAllTextAsync(filePath);
                        await context.Response.WriteAsync(html);
                    }
                    else
                    {
                        await context.Response.WriteAsync("<h1>Error: index.html not found</h1>");
                    }
                });

                _logger.LogInformation("Starting Users Web Service on port 5000...");
                await _app.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Users Web Service");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app != null)
            {
                await _app.StopAsync(cancellationToken);
                await _app.DisposeAsync();
            }
            await base.StopAsync(cancellationToken);
        }
    }

    public class AddUserRequest
    {
        public string Address { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}
