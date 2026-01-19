using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Polymarket.CopyBot.Console.Models
{
    [Index(nameof(UserAddress))]
    [Index(nameof(Timestamp))]
    public class UserClosedPosInfo
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string UserAddress { get; set; } = string.Empty;

        // User suspects PositionId might not be valid/reliable, so we keep it nullable and don't rely on it for PK
        [JsonPropertyName("positionId")]
        public string? PositionId { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("eventSlug")]
        public string? EventSlug { get; set; }

        [JsonPropertyName("realizedPnl")]
        public double? RealizedPnl { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
