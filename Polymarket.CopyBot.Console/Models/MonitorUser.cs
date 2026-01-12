using System.ComponentModel.DataAnnotations;

namespace Polymarket.CopyBot.Console.Models
{
    public class MonitorUser
    {
        [Key]
        public string Address { get; set; } = string.Empty;
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
