using System.Text.Json.Serialization;

namespace Polymarket.CopyBot.Console.Models
{
    public class ClosedPosition
    {
        [JsonPropertyName("realizedPnl")]
        public double? RealizedPnl { get; set; }

        [JsonPropertyName("positionId")]
        public string? PositionId { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("eventSlug")]
        public string? EventSlug { get; set; }

        // other fields omitted - only realizedPnl is used for stats
    }
}
