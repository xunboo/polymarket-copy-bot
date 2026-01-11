namespace Polymarket.CopyBot.Console.Configuration
{
    public enum CopyStrategy
    {
        PERCENTAGE,
        FIXED,
        ADAPTIVE
    }

    public class MultiplierTier
    {
        public double Min { get; set; }
        public double? Max { get; set; } // Null for infinite
        public double Multiplier { get; set; }
    }

    public class CopyStrategyConfig
    {
        public CopyStrategy Strategy { get; set; } = CopyStrategy.PERCENTAGE;
        public double CopySize { get; set; } = 10.0;
        
        // Adaptive
        public double? AdaptiveMinPercent { get; set; }
        public double? AdaptiveMaxPercent { get; set; }
        public double? AdaptiveThreshold { get; set; }

        public List<MultiplierTier> TieredMultipliers { get; set; } = new();
        public double? TradeMultiplier { get; set; }

        public double MaxOrderSizeUsd { get; set; } = 100.0;
        public double MinOrderSizeUsd { get; set; } = 1.0;
        public double? MaxPositionSizeUsd { get; set; }
        public double? MaxDailyVolumeUsd { get; set; }
    }
}
