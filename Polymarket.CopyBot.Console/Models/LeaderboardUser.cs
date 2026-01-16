using System.Text.Json.Serialization;

namespace Polymarket.CopyBot.Console.Models
{
    public class LeaderboardUser
    {
        [JsonPropertyName("rank")]
        public string Rank { get; set; } = string.Empty;

        [JsonPropertyName("proxyWallet")]
        public string ProxyWallet { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("vol")]
        public double Volume { get; set; }

        [JsonPropertyName("pnl")]
        public double Pnl { get; set; }

        [JsonPropertyName("profileImage")]
        public string? ProfileImage { get; set; }

        [JsonPropertyName("xUsername")]
        public string? XUsername { get; set; }

        [JsonPropertyName("verifiedBadge")]
        public bool VerifiedBadge { get; set; }

        // Augmented statistics (computed by the app)
        [JsonPropertyName("winPercent")]
        public double WinPercent { get; set; }

        [JsonPropertyName("closedPositionsCount")]
        public int ClosedPositionsCount { get; set; }

        [JsonPropertyName("winsCount")]
        public int WinsCount { get; set; }
    }
}
