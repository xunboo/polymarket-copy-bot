# Polymarket Copy Bot (.NET)

> A high-performance automated copy trading bot for Polymarket, built on **.NET 8**. It mirrors trades from top performers with intelligent position sizing and real-time execution.

![Polymarket CopyBot Dashboard](image.png)

## Overview

The Polymarket Copy Trading Bot automatically replicates trades from successful Polymarket traders to your wallet. It's built as a **.NET Worker Service** to ensure robust background execution and low-latency performance.

### How It Works

1.  **Select Traders** - Choose top performers from the [Polymarket leaderboard](https://polymarket.com/leaderboard).
2.  **Monitor Activity** - The bot polls the Polymarket Data API to detect new positions from selected traders.
3.  **Calculate Size** - Automatically scales trades based on your configured strategy (Percentage, Fixed, or Adaptive).
4.  **Execute Orders** - Places matching orders on the Polymarket CLOB (Central Limit Order Book) using your wallet.
5.  **Track Performance** - Maintains trade history and position tracking in MongoDB.

## Quick Start

### Prerequisites

-   [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   **No Database Server Required** (Uses SQLite)
-   Polygon wallet with **USDC** (for betting) and **POL** (MATIC) (for gas)
-   RPC endpoint ([Infura](https://infura.io) or [Alchemy](https://www.alchemy.com))

### Installation

#### 1. Clone repository
```bash
git clone https://github.com/MahmoudKandee/polymarket-copy-bot-net
cd polymarket-copy-bot-net
```

#### 2. Restore Dependencies
```bash
dotnet restore
```

#### 3. Configure Environment
Create a `.env` file in the root of the `Polymarket.CopyBot.Console` project (or copy from example if available).

```bash
# Your trading wallet
PROXY_WALLET=your_polygon_wallet_address
PRIVATE_KEY=your_private_key_without_0x_prefix

# Setup
# Optional: SQLITE_CONNECTION=Data Source=copybot.db
RPC_URL=https://polygon-mainnet.infura.io/v3/YOUR_PROJECT_ID

# Bot Configuration
CLOB_HTTP_URL=https://clob.polymarket.com/
CLOB_WS_URL=wss://ws-subscriptions-clob.polymarket.com/ws
USDC_CONTRACT_ADDRESS=0x2791Bca1f2de4661ED88A30C99A7a9449Aa84174

# Execution Control
RUN_TRADE_EXECUTOR=true  # Set to false to run in "Monitor Only" mode

# Strategy Settings
TRADE_MULTIPLIER=1.0
COPY_PERCENTAGE=10
MAX_ORDER_SIZE_USD=50
MIN_ORDER_SIZE_USD=5
```

### Build and Run

#### Build the Solution
```bash
dotnet build
```

#### Run the Bot
Navigate to the console project directory and run:

```bash
cd Polymarket.CopyBot.Console
dotnet run
```

### Web Dashboard

Once the bot is running, you can access the **Web Dashboard** at:
[http://localhost:5000](http://localhost:5000)

**Features:**
**Features:**
*   **Watched Users**: View the list of monitored users.
    *   **Add Users**: Directly add users to the monitor list from the Leaderboard.
    *   **Persistent Storage**: Users are saved to the local SQLite database.
*   **Leaderboard**: View the top 100 Polymarket traders.
    *   Filter by **Day**, **Week**, **Month**, or **All Time**.
    *   View P&L, Volume, WinRate and Rank.
    *   **Monitor**: Click the `+` button to start copying a trader immediately.
    *   Easily copy proxy wallet addresses.

## Features

-   **Multi-Trader Support**: Track and copy trades from multiple traders simultaneously.
-   **Leaderboard Support**: Show Offical API data and Other Statistics Rate.
-   **Smart Strategies**:
    -   **Percentage**: Copy a % of your balance.
    -   **Fixed**: Copy a fixed USD amount per trade.
    -   **Adaptive**: Scale based on the trader's conviction.
-   **Safety Limits**: Configurable Max/Min order sizes and position limits.
-   **Real-time Execution**: Fast polling and execution using .NET's high-performance `HttpClient`.
-   **SQLite Integration**: Local, self-contained database for persistent storage (no server required).
-   **Type Safety**: Fully typed C# codebase for reliability and easier maintenance.

## Project Structure

-   `Polymarket.CopyBot.sln`: Solution file.
-   **Polymarket.CopyBot.Console**: The main executable (Worker Service).
    -   `Configuration/`: App settings and strategy config.
    -   `Services/`: Core logic (`TradeMonitor`, `TradeExecutor`, `CopyStrategy`).
    -   `Repositories/`: SQLite data access modules.
-   **Polymarket.ClobClient**: C# Library for interacting with Polymarket's CLOB API (EIP-712 signing, order management).

## License

MIT License.

**Disclaimer:** This software is for educational purposes only. Trading involves risk of loss.
