using System.Net.Http.Json;
using Polymarket.CopyBot.Console.Configuration;
using Microsoft.Extensions.Logging;

namespace Polymarket.CopyBot.Console.Services
{
    public interface IPolymarketDataService
    {
        Task<List<T>> GetActivity<T>(string userAddress, string type = "TRADE");
        Task<List<T>> GetPositions<T>(string userAddress);
        Task<List<Polymarket.CopyBot.Console.Models.LeaderboardUser>> GetLeaderboardAsync(string timePeriod = "MONTH");
    }

    public class PolymarketDataService : IPolymarketDataService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfig _config;
        private readonly ILogger<PolymarketDataService> _logger;

        public PolymarketDataService(HttpClient httpClient, AppConfig config, ILogger<PolymarketDataService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri("https://data-api.polymarket.com/");
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.RequestTimeoutMs);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<List<T>> GetActivity<T>(string userAddress, string type = "TRADE")
        {
            return await FetchData<List<T>>($"activity?user={userAddress}&type={type}");
        }

        public async Task<List<T>> GetPositions<T>(string userAddress)
        {
            return await FetchData<List<T>>($"positions?user={userAddress}");
        }

        public async Task<List<Polymarket.CopyBot.Console.Models.LeaderboardUser>> GetLeaderboardAsync(string timePeriod = "MONTH")
        {
            var allUsers = new List<Polymarket.CopyBot.Console.Models.LeaderboardUser>();
            
            // Allow default if null/empty
            if (string.IsNullOrWhiteSpace(timePeriod)) timePeriod = "MONTH";

            // Fetch top 100 (limit 50 per page)
            // Page 1: Offset 0
            var page1 = await FetchData<List<Polymarket.CopyBot.Console.Models.LeaderboardUser>>($"v1/leaderboard?limit=50&offset=0&timePeriod={timePeriod}");
            if (page1 != null) allUsers.AddRange(page1);

            // Page 2: Offset 50
            var page2 = await FetchData<List<Polymarket.CopyBot.Console.Models.LeaderboardUser>>($"v1/leaderboard?limit=50&offset=50&timePeriod={timePeriod}");
            if (page2 != null) allUsers.AddRange(page2);

            return allUsers;
        }

        private async Task<TResult> FetchData<TResult>(string endpoint) where TResult : new()
        {
            var retries = _config.NetworkRetryLimit;
            var delayMs = 1000;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();
                    
                    var result = await response.Content.ReadFromJsonAsync<TResult>();
                    return result ?? new TResult();
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == retries)
                    {
                        _logger.LogError(ex, "Network error fetching {Endpoint} after {Retries} attempts", endpoint, retries);
                        throw;
                    }
                    
                    _logger.LogWarning("Network error (attempt {Attempt}/{Retries}), retrying...", attempt, retries);
                    await Task.Delay(delayMs * (int)Math.Pow(2, attempt - 1));
                }
            }
            return new TResult();
        }
    }
}
