# CLAUDE.md - Crypto Whale Tracker

This file provides context for AI assistants working on this codebase.

## Project Overview

Crypto Whale Tracker (InvestIt) is an ASP.NET Core Razor Pages web application built on **.NET 10** that monitors cryptocurrency wallets across multiple blockchains and detects large ("whale") transactions in real time. It supports **Ethereum**, **Binance Smart Chain (BSC)**, **Polygon**, and **Solana**.

Key capabilities:
- Multi-chain wallet monitoring with configurable polling
- Whale transaction detection (default threshold: $10,000 USD)
- Real-time browser notifications via SignalR
- Background services for continuous monitoring and notification processing
- Rate limiting, retry policies, and circuit breakers for external API resilience

## Repository Structure

```
Crypto_Whale_Tracker/
├── InvestIt/                            # Main web application
│   ├── BackgroundServices/              # Hosted services (monitoring, notifications)
│   ├── Data/                            # EF Core DbContext
│   │   └── Entities/                    # Entity models (Transaction, MonitoredWallet, etc.)
│   ├── Helpers/                         # Utility classes (TimeZoneHelper)
│   ├── Hubs/                            # SignalR hub (TransactionNotificationHub)
│   ├── Middleware/                      # GlobalExceptionMiddleware
│   ├── Migrations/                      # EF Core database migrations
│   ├── Pages/                           # Razor Pages (UI)
│   │   ├── Wallets/                     # Wallet CRUD pages
│   │   ├── Transactions/                # Transaction listing pages
│   │   └── Shared/                      # Layout, partial views
│   ├── Repositories/                    # Data access layer
│   │   ├── Interfaces/                  # Repository contracts
│   │   └── Implementations/             # Repository implementations
│   ├── Services/                        # Business logic
│   │   ├── ExplorerClients/             # Blockchain API clients (Etherscan, Solscan)
│   │   ├── Interfaces/                  # Service contracts
│   │   ├── Monitoring/                  # Per-chain monitor services
│   │   ├── Notifications/               # SignalR notification service
│   │   ├── PriceTracking/               # CoinGecko price client
│   │   ├── Processing/                  # Transaction classification & validation
│   │   ├── RateLimiting/                # Token-bucket rate limiter
│   │   └── WhaleTracking/               # Whale detection services
│   ├── Tests/                           # Manual failure scenario tests
│   ├── wwwroot/                         # Static assets (CSS, JS)
│   ├── Program.cs                       # Application entry point & DI setup
│   ├── appsettings.json                 # Configuration (API keys, thresholds)
│   └── InvestIt.csproj                  # Project file
├── InvestIt.Tests/                      # Automated test project
│   ├── Integration/                     # Integration tests
│   ├── LoadTests/                       # Performance/load tests
│   └── Services/                        # Service unit tests
├── InvestIt.slnx                        # Solution file
└── .gitignore
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core / .NET 10 (Razor Pages) |
| Database | SQL Server (EF Core 10) |
| Real-time | SignalR (WebSocket) |
| Blockchain APIs | Refit HTTP clients (Etherscan V2, Solscan) |
| Resilience | Polly (retry + circuit breaker) |
| Logging | Serilog (console + rolling file) |
| Ethereum/EVM | Nethereum.Web3 |
| Solana | Solnet.Rpc |
| Testing | xUnit, Moq, FluentAssertions |

## Common Commands

### Build & Run

```bash
# Build the solution
dotnet build InvestIt.slnx

# Run the web application
dotnet run --project InvestIt

# Run in development mode
dotnet run --project InvestIt --environment Development
```

### Tests

```bash
# Run all tests
dotnet test InvestIt.Tests/InvestIt.Tests.csproj

# Run with verbosity
dotnet test InvestIt.Tests/InvestIt.Tests.csproj --verbosity normal

# Run specific test class
dotnet test InvestIt.Tests/InvestIt.Tests.csproj --filter "FullyQualifiedName~ClassName"
```

### Database Migrations

```bash
# Add a new migration
dotnet ef migrations add MigrationName --project InvestIt

# Apply migrations
dotnet ef database update --project InvestIt

# Generate SQL script
dotnet ef migrations script --project InvestIt
```

## Architecture

### Layered Design

```
Razor Pages (UI) → Services (Business Logic) → Repositories (Data Access) → EF Core (Database)
                          ↕
               ExplorerClients (External APIs)
                          ↕
               BackgroundServices (Polling)
                          ↕
               SignalR Hub (Real-time Push)
```

### Dependency Injection

All dependencies are registered in `Program.cs`:
- **Repositories**: Scoped lifetime (`AddScoped`)
- **Services**: Scoped lifetime
- **RateLimitService**: Singleton (shared state across requests)
- **Background services**: `AddHostedService`
- **HTTP clients**: Configured with Polly policies via `AddRefitClient` / `AddHttpClient`

### Background Services

1. **BlockchainMonitoringHostedService**: Polls all chains every 30 seconds, runs both user-wallet monitoring and whale detection.
2. **NotificationProcessorHostedService**: Processes the notification queue every 5 seconds, sends SignalR messages.

### SignalR Hub

- Endpoint: `/hubs/transactions`
- Client methods: `ReceiveTransaction`, `ReceiveWalletTransaction`
- Server methods: `SubscribeToWallet`, `UnsubscribeFromWallet`, `Ping`, `SendTestNotification`
- Client JS: `wwwroot/js/transaction-hub.js`

### Blockchain Monitor Services

All chain monitors implement `IChainMonitorService`:
- `EthereumMonitorService` (ChainId: 1)
- `BscMonitorService` (ChainId: 56)
- `PolygonMonitorService` (ChainId: 137)
- `SolanaMonitorService` (uses Solscan API)

EVM chains share the Etherscan V2 API (single endpoint with `chainid` parameter).

## Database

### Key Entities

- **BlockchainNetwork**: Supported chains with API configuration
- **MonitoredWallet**: User-tracked wallet addresses (FK → BlockchainNetwork)
- **Transaction**: Recorded transactions with amount as `decimal(38,18)`, txHash is unique
- **NotificationQueue**: Queued SignalR notifications with retry tracking
- **MonitoringStatus**: Per-network health tracking (one per network)
- **ApiRateLimitTracking**: Rate limit token state per network

### Migrations

Migrations are in `InvestIt/Migrations/`. The database is SQL Server (LocalDB in development). Migrations are **not** auto-applied on startup; run `dotnet ef database update` manually.

### Seed Data

SQL seed scripts in `InvestIt/`:
- `SeedWallets.sql` - Initial wallet set (Vitalik, Binance, DEX routers)
- `AddActiveWallets.sql` - Additional active wallets
- `SampleTransactions.sql` - Test transaction data

## Code Conventions

### C# Style

- **Nullable reference types** enabled globally (`#nullable enable`)
- **Implicit usings** enabled
- **PascalCase** for classes, methods, properties, and public members
- **camelCase** for local variables and private fields
- **I-prefix** for interfaces (e.g., `IWalletRepository`)
- **Async suffix** on async methods (e.g., `GetTransactionsAsync`)
- Constructor injection for all dependencies
- Async/await throughout for I/O operations

### Error Handling

- `GlobalExceptionMiddleware` catches unhandled exceptions in production
- Development mode uses `UseDeveloperExceptionPage()`
- Services log errors via Serilog before returning null/empty results
- External API calls wrapped with Polly retry (3 attempts, exponential backoff) and circuit breaker (opens after 5 failures, resets after 1 minute)

### Transaction Processing

- Transactions classified as **Buy** (wallet is recipient), **Sell** (wallet is sender), or **Transfer**
- Amounts clamped to `decimal(38,18)` safe range (max 10^15) to prevent SQL overflow
- Dust transactions (< 0.0001) are filtered out
- Deduplication enforced via unique `TxHash` constraint

### Rate Limiting

- Token-bucket algorithm per blockchain network
- Default rates: Ethereum/BSC/Polygon = 5 req/sec, Solana = 3 req/sec
- Checked before every external API call via `IRateLimitService`

### Logging

- Serilog with structured logging throughout
- Console sink + rolling file sink (`logs/investit-YYYY-MM-DD.txt`)
- Configuration in `appsettings.json` under `Serilog` section
- Log levels: Debug (development), Information (normal), Warning, Error

### Timezone Handling

- All timestamps stored as **UTC** in the database
- Displayed in **US Central Time** on the UI
- `TimeZoneHelper` handles cross-platform timezone IDs (Windows vs. Linux)

## Configuration

### Required Setup

Before running, configure in `appsettings.json`:

1. **Database**: `ConnectionStrings:DefaultConnection` (SQL Server connection string)
2. **API Keys** under `BlockchainApis`:
   - `Ethereum:ApiKey` (Etherscan)
   - `BinanceSmartChain:ApiKey` (BSCScan)
   - `Polygon:ApiKey` (Polygonscan)
   - `Solana:ApiKey` (Solscan, optional)

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `MonitoringSettings:PollingIntervalSeconds` | 30 | How often to poll chains |
| `MonitoringSettings:MaxTransactionsPerPoll` | 100 | Max transactions per API call |
| `MonitoringSettings:MaxRetryAttempts` | 3 | Retry count for failed API calls |
| `WhaleTracking:Enabled` | true | Enable whale detection |
| `WhaleTracking:ThresholdUsd` | 10000 | USD threshold for whale alerts |

### Sensitive Files (gitignored)

- `appsettings.Production.json`
- `appsettings.Local.json`
- `secrets.json`

## Pages & Routes

| Route | Page | Purpose |
|-------|------|---------|
| `/` | Index | Dashboard with stats, recent transactions, network status |
| `/Wallets/List` | Wallet List | View/manage all monitored wallets |
| `/Wallets/Add` | Add Wallet | Add a new wallet to monitor |
| `/Wallets/Details/{id}` | Wallet Details | Transaction history for a specific wallet |
| `/Transactions/Recent` | Recent Transactions | Paginated transaction list with filtering |
| `/Privacy` | Privacy | Privacy policy |
| `/health` | Health Check | EF Core database health endpoint |
| `/hubs/transactions` | SignalR Hub | WebSocket endpoint for real-time updates |

## Testing

- **Framework**: xUnit 2.9.3 with Moq 4.20.72 and FluentAssertions 8.8.0
- **Test DB**: EF Core InMemory provider for isolation
- **Test project**: `InvestIt.Tests/` with subdirectories for Integration, LoadTests, and Services
- **Coverage**: coverlet.collector 6.0.4
- Tests reference the main project via `ProjectReference`

## Important Implementation Notes

- **Etherscan V2 API**: All EVM chains (Ethereum, BSC, Polygon) use a single Etherscan V2 endpoint differentiated by `chainid` parameter, registered once via Refit in `Program.cs`
- **Decimal safety**: `TransactionProcessingService` clamps amounts to avoid SQL Server `decimal(38,18)` overflow. Never store raw unclamped values.
- **Whale detection** has two strategies: (1) monitoring user-added wallets and (2) global scanning of high-volume DEX/exchange contracts (Uniswap, PancakeSwap, QuickSwap routers)
- **SignalR null safety**: `SignalRNotificationService` handles null navigation properties when formatting notifications
- **No authentication**: The application currently has no auth layer; all endpoints are publicly accessible
