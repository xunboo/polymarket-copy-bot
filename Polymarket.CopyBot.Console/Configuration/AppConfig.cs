using System.Globalization;

namespace Polymarket.CopyBot.Console.Configuration
{
    public class AppConfig
    {
        public string[] UserAddresses { get; set; } = Array.Empty<string>();
        public string ProxyWallet { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string ClobHttpUrl { get; set; } = "https://clob.polymarket.com/";
        public string ClobWsUrl { get; set; } = "wss://ws-subscriptions-clob.polymarket.com/ws";
        public int FetchInterval { get; set; } = 1;
        public int TooOldTimestamp { get; set; } = 1;
        public int RetryLimit { get; set; } = 3;
        public int RequestTimeoutMs { get; set; } = 10000;
        public int NetworkRetryLimit { get; set; } = 3;
        
        // Copy Strategy Config
        public CopyStrategyConfig Strategy { get; set; } = new();

        public bool TradeAggregationEnabled { get; set; } = false;
        public int TradeAggregationWindowSeconds { get; set; } = 300;
        public bool PreviewMode { get; set; } = false;
        
        public string MongoUri { get; set; } = string.Empty;
        public string RpcUrl { get; set; } = string.Empty;
        public string UsdcContractAddress { get; set; } = "0x2791Bca1f2de4661ED88A30C99A7a9449Aa84174";

        public static AppConfig LoadFromEnv()
        {
            DotNetEnv.Env.Load();
            
            var config = new AppConfig();
            
            // User Addresses
            var usersStr = GetEnv("USER_ADDRESSES", "");
            if (usersStr.StartsWith("[") && usersStr.EndsWith("]"))
            {
                // Simple JSON array parsing
                config.UserAddresses = usersStr.Trim('[', ']').Split(',')
                    .Select(s => s.Trim().Trim('"').Trim('\''))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
            else
            {
                config.UserAddresses = usersStr.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            config.ProxyWallet = GetEnv("PROXY_WALLET");
            config.PrivateKey = GetEnv("PRIVATE_KEY");
            config.ClobHttpUrl = GetEnv("CLOB_HTTP_URL", config.ClobHttpUrl);
            config.ClobWsUrl = GetEnv("CLOB_WS_URL", config.ClobWsUrl);
            
            config.FetchInterval = int.Parse(GetEnv("FETCH_INTERVAL", "1"));
            config.TooOldTimestamp = int.Parse(GetEnv("TOO_OLD_TIMESTAMP", "1"));
            config.RetryLimit = int.Parse(GetEnv("RETRY_LIMIT", "3"));
            
            config.TradeAggregationEnabled = GetEnv("TRADE_AGGREGATION_ENABLED", "false").ToLower() == "true";
            
            config.MongoUri = GetEnv("MONGO_URI");
            config.RpcUrl = GetEnv("RPC_URL");

            // Load Copy Strategy
            config.Strategy.Strategy = Enum.TryParse<CopyStrategy>(GetEnv("COPY_STRATEGY", "PERCENTAGE"), true, out var strat) 
                ? strat : CopyStrategy.PERCENTAGE;
                
            config.Strategy.CopySize = double.Parse(GetEnv("COPY_SIZE", "10.0"), CultureInfo.InvariantCulture);
            config.Strategy.MaxOrderSizeUsd = double.Parse(GetEnv("MAX_ORDER_SIZE_USD", "100.0"), CultureInfo.InvariantCulture);
            config.Strategy.MinOrderSizeUsd = double.Parse(GetEnv("MIN_ORDER_SIZE_USD", "1.0"), CultureInfo.InvariantCulture);
            
            // ... load other strategy params
            
            return config;
        }

        private static string GetEnv(string key, string defaultValue = "")
        {
             return Environment.GetEnvironmentVariable(key) ?? defaultValue;
        }
    }
}
