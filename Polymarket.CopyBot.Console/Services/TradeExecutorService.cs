using Polymarket.ClobClient;
using Polymarket.ClobClient.Models;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;
using Polymarket.CopyBot.Console.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Numerics;

namespace Polymarket.CopyBot.Console.Services
{
    public class TradeExecutorService : BackgroundService
    {
        private readonly AppConfig _config;
        private readonly PolymarketClient _clobClient;
        private readonly PolymarketDataService _dataService;
        private readonly CopyStrategyService _strategyService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TradeExecutorService> _logger;

        private bool _clientReady = false;

        public TradeExecutorService(
            AppConfig config,
            PolymarketDataService dataService,
            CopyStrategyService strategyService,
            IServiceScopeFactory scopeFactory,
            ILogger<TradeExecutorService> logger)
        {
            _config = config;
            _dataService = dataService;
            _strategyService = strategyService;
            _scopeFactory = scopeFactory;
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
                // If keys are invalid, we catch it but don't crash, just can't trade.
                var creds = await _clobClient.DeriveApiKey();
                _clientReady = true;
                _logger.LogInformation("CLOB Client API Keys derived.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to derive API keys. Trading execution will be DISABLED.");
                // Do not throw, keep service alive for gathering data at least
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
                     // Only process if client is ready (keys derived) OR we want to just mark things as failed?
                     // If we don't process, queue grows.
                     // But if keys invalid, we can monitor but not trade.
                     // Let's rely on _clientReady inside execute? 
                     // We still need to process DB updates so we can mark things as skipped/failed maybe?
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
            using (var scope = _scopeFactory.CreateScope())
            {
                var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();
                
                foreach (var address in _config.UserAddresses)
                {
                    var trades = await activityRepo.GetUnprocessedAsync(address);

                    foreach (var trade in trades)
                    {
                        // Mark as processing immediately (BotExecutedTime = 1)
                        trade.BotExecutedTime = 1;
                        await activityRepo.UpdateAsync(trade);

                        if (!_clientReady)
                        {
                            _logger.LogWarning("CLOB Client not ready. Marking trade {Id} as failed/skipped.", trade.Id);
                            // Mark as failed/skipped (999 or error code)
                            trade.Bot = true;
                            trade.BotExecutedTime = 0; // 0 = processed/skipped?
                            await activityRepo.UpdateAsync(trade);
                            continue;
                        }

                        await ExecuteTrade(trade, address, scope);
                    }
                }
            }
        }

        private async Task ExecuteTrade(UserActivity trade, string userAddress, IServiceScope scope)
        {
            _logger.LogInformation("Processing trade from {Address}: {Side} {Asset}", userAddress, trade.Side, trade.Asset);
            
             var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();

            if (_config.PreviewMode)
            {
                _logger.LogInformation("PREVIEW MODE: Skipping execution.");
                await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 0); // 0 means success/processed
                return;
            }

            try 
            {
                if (trade.Side?.ToUpper() == "BUY")
                {
                    await ExecuteBuy(trade, userAddress, scope);
                }
                else if (trade.Side?.ToUpper() == "SELL")
                {
                    await ExecuteSell(trade, userAddress, scope);
                }
                else 
                {
                     _logger.LogWarning("Unknown side: {Side}", trade.Side);
                     await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 999);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute trade");
                // Retry logic is inside Buy/Sell usually, but if exception bubbles up:
                await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 999); 
            }
        }

        private async Task ExecuteBuy(UserActivity trade, string userAddress, IServiceScope scope)
        {
            var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();
            
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
                 await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 0);
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

             await MarkTradeProcessed(activityRepo, userAddress, trade.Id, success ? 999 : 0);
        }

        private async Task ExecuteSell(UserActivity trade, string userAddress, IServiceScope scope)
        {
             var activityRepo = scope.ServiceProvider.GetRequiredService<IUserActivityRepository>();
             
             // Check if we hold position
             // Since we don't have our positions synced to DB (only traders), we need to fetch live.
             var myPositions = await _dataService.GetPositions<UserPosition>(_config.ProxyWallet);
             var myPos = myPositions.FirstOrDefault(x => x.Asset == trade.Asset);

             if (myPos == null || (myPos.Size ?? 0) <= 0)
             {
                 _logger.LogWarning("No position to sell.");
                 await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 0);
                 return;
             }

             // Calculate Sell Amount (Simplified: Sell same %)
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
                    await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 999);
                }
                else 
                {
                    _logger.LogWarning("No Bids");
                    await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 0); // Skip
                }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Sell failed");
                 await MarkTradeProcessed(activityRepo, userAddress, trade.Id, 0); // Skip on error
             }
        }

        private async Task MarkTradeProcessed(IUserActivityRepository activityRepo, string userAddress, string? tradeId, int code)
        {
            if (string.IsNullOrEmpty(tradeId)) return;
            var trade = await activityRepo.GetByIdAsync(tradeId);
            if (trade != null)
            {
                trade.Bot = true;
                trade.BotExecutedTime = code;
                await activityRepo.UpdateAsync(trade);
            }
        }
    }
}
