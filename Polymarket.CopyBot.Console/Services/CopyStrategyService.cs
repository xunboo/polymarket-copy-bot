using Polymarket.CopyBot.Console.Configuration;

namespace Polymarket.CopyBot.Console.Services
{
    public class OrderSizeCalculation
    {
        public double TraderOrderSize { get; set; }
        public double BaseAmount { get; set; }
        public double FinalAmount { get; set; }
        public CopyStrategy Strategy { get; set; }
        public bool CappedByMax { get; set; }
        public bool ReducedByBalance { get; set; }
        public bool BelowMinimum { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class CopyStrategyService
    {
        public OrderSizeCalculation CalculateOrderSize(
            CopyStrategyConfig config,
            double traderOrderSize,
            double availableBalance,
            double currentPositionSize = 0)
        {
            double baseAmount = 0;
            string reasoning = "";

            switch (config.Strategy)
            {
                case CopyStrategy.PERCENTAGE:
                    baseAmount = traderOrderSize * (config.CopySize / 100.0);
                    reasoning = $"{config.CopySize}% of trader's ${traderOrderSize:F2} = ${baseAmount:F2}";
                    break;
                case CopyStrategy.FIXED:
                    baseAmount = config.CopySize;
                    reasoning = $"Fixed amount: ${baseAmount:F2}";
                    break;
                case CopyStrategy.ADAPTIVE:
                    double adaptivePercent = CalculateAdaptivePercent(config, traderOrderSize);
                    baseAmount = traderOrderSize * (adaptivePercent / 100.0);
                    reasoning = $"Adaptive {adaptivePercent:F1}% of trader's ${traderOrderSize:F2} = ${baseAmount:F2}";
                    break;
                default:
                    throw new ArgumentException($"Unknown strategy: {config.Strategy}");
            }

            double multiplier = GetTradeMultiplier(config, traderOrderSize);
            double finalAmount = baseAmount * multiplier;

            if (Math.Abs(multiplier - 1.0) > 0.001)
            {
                reasoning += $" -> {multiplier}x multiplier: ${baseAmount:F2} -> ${finalAmount:F2}";
            }

            bool cappedByMax = false;
            bool reducedByBalance = false;
            bool belowMinimum = false;

            if (finalAmount > config.MaxOrderSizeUsd)
            {
                finalAmount = config.MaxOrderSizeUsd;
                cappedByMax = true;
                reasoning += $" -> Capped at max ${config.MaxOrderSizeUsd}";
            }

            if (config.MaxPositionSizeUsd.HasValue)
            {
                double newTotalPosition = currentPositionSize + finalAmount;
                if (newTotalPosition > config.MaxPositionSizeUsd.Value)
                {
                    double allowedAmount = Math.Max(0, config.MaxPositionSizeUsd.Value - currentPositionSize);
                    if (allowedAmount < config.MinOrderSizeUsd)
                    {
                        finalAmount = 0;
                        reasoning += " -> Position limit reached";
                    }
                    else
                    {
                        finalAmount = allowedAmount;
                        reasoning += " -> Reduced to fit position limit";
                    }
                }
            }

            double maxAffordable = availableBalance * 0.99;
            if (finalAmount > maxAffordable)
            {
                finalAmount = maxAffordable;
                reducedByBalance = true;
                reasoning += $" -> Reduced to fit balance (${maxAffordable:F2})";
            }

            if (finalAmount < config.MinOrderSizeUsd)
            {
                belowMinimum = true;
                reasoning += $" -> Below minimum ${config.MinOrderSizeUsd}";
                finalAmount = 0;
            }

            return new OrderSizeCalculation
            {
                TraderOrderSize = traderOrderSize,
                BaseAmount = baseAmount,
                FinalAmount = finalAmount,
                Strategy = config.Strategy,
                CappedByMax = cappedByMax,
                ReducedByBalance = reducedByBalance,
                BelowMinimum = belowMinimum,
                Reasoning = reasoning
            };
        }

        private double CalculateAdaptivePercent(CopyStrategyConfig config, double traderOrderSize)
        {
            double minPercent = config.AdaptiveMinPercent ?? config.CopySize;
            double maxPercent = config.AdaptiveMaxPercent ?? config.CopySize;
            double threshold = config.AdaptiveThreshold ?? 500.0;

            if (traderOrderSize >= threshold)
            {
                double factor = Math.Min(1, traderOrderSize / threshold - 1);
                return Lerp(config.CopySize, minPercent, factor);
            }
            else
            {
                double factor = traderOrderSize / threshold;
                return Lerp(maxPercent, config.CopySize, factor);
            }
        }

        private double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Math.Max(0, Math.Min(1, t));
        }

        private double GetTradeMultiplier(CopyStrategyConfig config, double traderOrderSize)
        {
            if (config.TieredMultipliers != null && config.TieredMultipliers.Any())
            {
                foreach (var tier in config.TieredMultipliers)
                {
                    if (traderOrderSize >= tier.Min)
                    {
                        if (!tier.Max.HasValue || traderOrderSize < tier.Max.Value)
                        {
                            return tier.Multiplier;
                        }
                    }
                }
                return config.TieredMultipliers.Last().Multiplier;
            }

            if (config.TradeMultiplier.HasValue)
            {
                return config.TradeMultiplier.Value;
            }

            return 1.0;
        }
    }
}
