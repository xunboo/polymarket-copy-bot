using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Polymarket.CopyBot.Console.Models
{
    public class UserPosition
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? ProxyWallet { get; set; }
        public string? Asset { get; set; }
        public string? ConditionId { get; set; }
        public double? Size { get; set; }
        public double? AvgPrice { get; set; }
        public double? InitialValue { get; set; }
        public double? CurrentValue { get; set; }
        public double? CashPnl { get; set; }
        public double? PercentPnl { get; set; }
        public double? TotalBought { get; set; }
        public double? RealizedPnl { get; set; }
        public double? PercentRealizedPnl { get; set; }
        public double? CurPrice { get; set; }
        public bool? Redeemable { get; set; }
        public bool? Mergeable { get; set; }
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Icon { get; set; }
        public string? EventSlug { get; set; }
        public string? Outcome { get; set; }
        public int? OutcomeIndex { get; set; }
        public string? OppositeOutcome { get; set; }
        public string? OppositeAsset { get; set; }
        public string? EndDate { get; set; }
        public bool? NegativeRisk { get; set; }
    }
}
