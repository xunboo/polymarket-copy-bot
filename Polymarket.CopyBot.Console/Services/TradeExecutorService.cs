using Polymarket.ClobClient;
using Polymarket.ClobClient.Models;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;
using Polymarket.CopyBot.Console.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Numerics;

namespace Polymarket.CopyBot.Console.Services
{
    public class TradeExecutorService : BackgroundService
    {
        private readonly AppConfig _config;
        private readonly PolymarketClient _clobClient;
        private readonly PolymarketDataService _dataService; // For checking balances/positions
        private readonly CopyStrategyService _strategyService;
        private readonly IUserActivityRepository _activityRepo;
        private readonly IUserPositionRepository _positionRepo;
        private readonly ILogger<TradeExecutorService> _logger;

        public TradeExecutorService(
            AppConfig config,
            PolymarketDataService dataService,
            CopyStrategyService strategyService,
            IUserActivityRepository activityRepo,
            IUserPositionRepository positionRepo,
            ILogger<TradeExecutorService> logger)
        {
            _config = config;
            _dataService = dataService;
            _strategyService = strategyService;
            _activityRepo = activityRepo;
            _positionRepo = positionRepo;
            _logger = logger;

            // Initialize CLOB Client (Polygon Mainnet ChainID 137)
            _clobClient = new PolymarketClient(config.ClobHttpUrl, 137, config.PrivateKey);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing CLOB Client...");
            try 
            {
                // Derive API Keys on startup
                var creds = await _clobClient.DeriveApiKey();
                _logger.LogInformation("CLOB Client API Keys derived.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to derive API keys. Check private key.");
                throw;
            }
            
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trade Executor starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNewTrades();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Trade Executor loop");
                }

                await Task.Delay(300, stoppingToken); // Fast polling
            }
            
            _logger.LogInformation("Trade Executor stopped.");
        }

        private async Task ProcessNewTrades()
        {
            foreach (var address in _config.UserAddresses)
            {
                var collection = _activityRepo.GetCollection(address);
                
                // Find trades not yet processed by bot (Bot: false) and verified valid trade (Type: TRADE)
                // TS: { $and: [{ type: 'TRADE' }, { bot: false }, { botExcutedTime: 0 }] }
                var filter = Builders<UserActivity>.Filter.And(
                    Builders<UserActivity>.Filter.Eq(x => x.Type, "TRADE"),
                    Builders<UserActivity>.Filter.Eq(x => x.Bot, false),
                    Builders<UserActivity>.Filter.Eq(x => x.BotExecutedTime, 0)
                );

                var trades = await collection.Find(filter).ToListAsync();

                foreach (var trade in trades)
                {
                    // Mark as processing immediately (BotExecutedTime = 1)
                    var update = Builders<UserActivity>.Update.Set(x => x.BotExecutedTime, 1);
                    await collection.UpdateOneAsync(x => x.Id == trade.Id, update);

                    await ExecuteTrade(trade, address);
                }
            }
        }

        private async Task ExecuteTrade(UserActivity trade, string userAddress)
        {
            _logger.LogInformation("Processing trade from {Address}: {Side} {Asset}", userAddress, trade.Side, trade.Asset);

            if (_config.PreviewMode)
            {
                _logger.LogInformation("PREVIEW MODE: Skipping execution.");
                await MarkTradeProcessed(userAddress, trade.Id, 0); // 0 means success/processed
                return;
            }

            try 
            {
                if (trade.Side?.ToUpper() == "BUY")
                {
                    await ExecuteBuy(trade, userAddress);
                }
                else if (trade.Side?.ToUpper() == "SELL")
                {
                    await ExecuteSell(trade, userAddress);
                }
                else 
                {
                    // Merge or others? 
                    // TS supports 'merge' condition, logic says trade.side might be 'SELL' but condition 'merge'.
                    // Need to check specific logic. TS 'postOrder' checks `condition`.
                    // trade.ConditionId is not the condition string (buy/sell/merge).
                    // In TS tradeExecutor calls postOrder with 'buy' or 'sell' based on trade.side.
                    // But postOrder checks `condition` arg.
                    // In Executor: postOrder(..., trade.side === 'BUY' ? 'buy' : 'sell', ...)
                    // So we stick to Side.
                    
                     _logger.LogWarning("Unknown side: {Side}", trade.Side);
                     await MarkTradeProcessed(userAddress, trade.Id, 999);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute trade");
                // Retry logic is inside Buy/Sell usually, but if exception bubbles up:
                await MarkTradeProcessed(userAddress, trade.Id, 999); 
            }
        }

        private async Task ExecuteBuy(UserActivity trade, string userAddress)
        {
            // 1. Get Balances (TODO: Use real balance check)
            double myBalance = 1000.0; 
             
            // Get my positions (stubbed)
            double myPositionValue = 0;

             var calc = _strategyService.CalculateOrderSize(
                 _config.Strategy, 
                 trade.UsdcSize ?? 0, 
                 myBalance, 
                 myPositionValue
             );

             _logger.LogInformation("Order Calculation: {Reasoning}", calc.Reasoning);

             if (calc.FinalAmount == 0)
             {
                 _logger.LogWarning("Skipping trade: {Reasoning}", calc.Reasoning);
                 await MarkTradeProcessed(userAddress, trade.Id, 0);
                 return;
             }

             double remaining = calc.FinalAmount;
             int retry = 0;
             bool success = false;

             while (remaining > 0 && retry < _config.RetryLimit)
             {
                 try 
                 {
                    var orderBook = await _clobClient.GetOrderBook(trade.Asset ?? "");
                    if (orderBook.Asks == null || !orderBook.Asks.Any())
                    {
                        _logger.LogWarning("No asks in orderbook.");
                        break;
                    }

                    var bestAsk = orderBook.Asks.OrderBy(x => double.Parse(x.Price)).First();
                    double price = double.Parse(bestAsk.Price);
                    double askWidth = double.Parse(bestAsk.Size);

                    if (price - 0.05 > (trade.Price ?? 0))
                    {
                        _logger.LogWarning("Price slippage too high.");
                        break;
                    }

                    // Max order size in USD
                    double maxOrderSizeUsd = askWidth * price;
                    
                    // Amount we want to spend (USD)
                    double spendAmount = Math.Min(remaining, maxOrderSizeUsd);
                    
                    // Check min
                    if (spendAmount < _config.Strategy.MinOrderSizeUsd)
                    {
                        _logger.LogInformation("Remaining amount below min.");
                        success = true;
                        break;
                    }

                    // UserOrder expects Size in Contracts (Tokens)
                    double sizeInTokens = spendAmount / price;

                    var order = new UserOrder
                    {
                        TokenId = trade.Asset ?? "",
                        Side = Side.Buy,
                        Size = (decimal)sizeInTokens,
                        Price = (decimal)price,
                        // Type = OrderType.Fok // UserOrder doesn't have Type, maybe PostOrder takes arg usually or UserMarketOrder
                        // Checking ClobClient: PostOrder(UserOrder order, OrderType orderType = OrderType.Gtc)
                    };

                    _logger.LogInformation("Posting BUY: {Size} tokens @ {Price} (${Amount})", sizeInTokens, price, spendAmount);
                    
                    var resp = await _clobClient.PostOrder(order, OrderType.Fok);
                    
                    _logger.LogInformation("Order Success: {Response}", resp);
                    remaining -= spendAmount;
                    success = true;
                    retry = 0;
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError(ex, "Order failed (Attempt {Attempt})", retry + 1);
                     retry++;
                     await Task.Delay(1000);
                 }
             }

             await MarkTradeProcessed(userAddress, trade.Id, success ? 999 : 0);
        }

        private async Task ExecuteSell(UserActivity trade, string userAddress)
        {
             // Check if we hold position
             // Since we don't have our positions synced to DB (only traders), we need to fetch live.
             var myPositions = await _dataService.GetPositions<UserPosition>(_config.ProxyWallet);
             var myPos = myPositions.FirstOrDefault(x => x.Asset == trade.Asset);

             if (myPos == null || (myPos.Size ?? 0) <= 0)
             {
                 _logger.LogWarning("No position to sell.");
                 await MarkTradeProcessed(userAddress, trade.Id, 0);
                 return;
             }

             // Calculate Sell Amount (Simplified: Sell same %)
             // For now just sell 100% if trader sells
             // Or stub
             double sellAmount = myPos.Size ?? 0;
             
             // Get Bid Price
             try 
             {
                var ob = await _clobClient.GetOrderBook(trade.Asset ?? "");
                if (ob.Bids != null && ob.Bids.Any())
                {
                    var bestBid = ob.Bids.OrderByDescending(x => double.Parse(x.Price)).First();
                    decimal price = decimal.Parse(bestBid.Price);
                    decimal size = Math.Min((decimal)sellAmount, decimal.Parse(bestBid.Size));
                    
                    var order = new UserOrder
                    {
                        TokenId = trade.Asset ?? "",
                        Side = Side.Sell,
                        Size = size,
                        Price = price
                    };

                    _logger.LogInformation("Posting SELL: {Size} tokens @ {Price}", order.Size, order.Price);
                    await _clobClient.PostOrder(order, OrderType.Fok);
                    await MarkTradeProcessed(userAddress, trade.Id, 999);
                }
                else 
                {
                    _logger.LogWarning("No Bids");
                    await MarkTradeProcessed(userAddress, trade.Id, 0); // Skip
                }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Sell failed");
                 await MarkTradeProcessed(userAddress, trade.Id, 0); // Skip on error
             }
        }

        private async Task MarkTradeProcessed(string userAddress, string? tradeId, int code)
        {
            if (string.IsNullOrEmpty(tradeId)) return;
            var collection = _activityRepo.GetCollection(userAddress);
            var update = Builders<UserActivity>.Update
                .Set(x => x.Bot, true)
                .Set(x => x.BotExecutedTime, code); // using code to store result/retry count
                
            await collection.UpdateOneAsync(x => x.Id == tradeId, update);
        }
    }
}
