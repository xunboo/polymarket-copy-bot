
namespace Polymarket.CopyBot.Console.Models
{
    public class UserActivity
    {
        [System.ComponentModel.DataAnnotations.Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string OwnerAddress { get; set; } = string.Empty;

        public string? ProxyWallet { get; set; }
        public long? Timestamp { get; set; }
        public string? ConditionId { get; set; }
        public string? Type { get; set; }
        public double? Size { get; set; }
        public double? UsdcSize { get; set; }
        public string? TransactionHash { get; set; }
        public double? Price { get; set; }
        public string? Asset { get; set; }
        public string? Side { get; set; }
        public int? OutcomeIndex { get; set; }
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Icon { get; set; }
        public string? EventSlug { get; set; }
        public string? Outcome { get; set; }
        public string? Name { get; set; }
        public string? Pseudonym { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImage { get; set; }
        public string? ProfileImageOptimized { get; set; }
        
        public bool Bot { get; set; }
        public long BotExecutedTime { get; set; }
        public double? MyBoughtSize { get; set; }
    }
}
