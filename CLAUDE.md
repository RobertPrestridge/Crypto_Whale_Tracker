# CLAUDE.md - AI Assistant Guide for InvestIt (Crypto Whale Tracker)

## Project Overview

**InvestIt** is an ASP.NET Core Razor Pages application that monitors cryptocurrency wallet transactions across multiple blockchains (Ethereum, BSC, Polygon, Solana) and detects whale transactions. It uses SignalR for real-time notifications and polls blockchain explorer APIs on a configurable interval.

## Tech Stack

- **Framework**: ASP.NET Core (.NET 10) with Razor Pages
- **Database**: SQL Server (LocalDB for development) via Entity Framework Core 10
- **Real-time**: SignalR for live transaction notifications
- **Blockchain APIs**: Etherscan V2 unified API (via Refit) for EVM chains, Solscan for Solana (placeholder)
- **Resilience**: Polly for retry + circuit breaker policies on HTTP clients
- **Logging**: Serilog (Console + File sinks)
- **Price Data**: CoinGecko free API with in-memory caching
- **Testing**: xUnit + Moq + FluentAssertions + EF Core InMemory provider
- **Other**: Nethereum.Web3, Solnet.Rpc (referenced but Solana is placeholder)

## Solution Structure

```
InvestIt.slnx                          # Solution file (currently only references main project)
InvestIt/                              # Main web application
  Program.cs                           # App startup, DI registration, middleware pipeline
  Data/
    ApplicationDbContext.cs             # EF Core DbContext with Fluent API config + seed data
    Entities/                          # Domain entities (Transaction, MonitoredWallet, BlockchainNetwork, etc.)
  Repositories/
    Interfaces/                        # Repository contracts
    Implementations/                   # EF Core repository implementations
  Services/
    Interfaces/                        # Service contracts (IChainMonitorService, IRateLimitService, etc.)
    ExplorerClients/                   # Blockchain API clients (EtherscanClient, BscScanClient, etc.)
      Models/                          # API response DTOs, FlexibleEtherscanResponse with custom JSON converter
    Monitoring/                        # Per-chain monitor services + BlockchainMonitoringService orchestrator
    Processing/                        # TransactionProcessingService (classify, store, queue notifications)
    Notifications/                     # SignalRNotificationService
    PriceTracking/                     # CoinGeckoClient with caching
    RateLimiting/                      # Token bucket rate limiter (in-memory, singleton)
    WhaleTracking/                     # WhaleTransactionMonitorService + GlobalWhaleMonitorService
  BackgroundServices/                  # Hosted services for polling loop + notification processing
  Hubs/                                # SignalR hub (TransactionNotificationHub)
  Helpers/                             # TimeZoneHelper (UTC -> Central Time conversion)
  Middleware/                          # GlobalExceptionMiddleware
  Pages/                               # Razor Pages (Dashboard, Wallet CRUD, Transactions)
  Migrations/                          # EF Core migrations
  wwwroot/                             # Static files (CSS, JS, favicon)
InvestIt.Tests/                        # Test project (not in .slnx - see known issues)
  Services/                            # Unit tests (TransactionProcessingService, EthereumMonitor, RateLimit)
  Integration/                         # Integration tests with InMemory DB
  LoadTests/                           # SignalR concurrency + rate limit load tests
```

## Build & Run

```bash
# Restore and build
dotnet build InvestIt.slnx

# Run the app (uses appsettings.json / appsettings.Development.json)
dotnet run --project InvestIt

# Run tests
dotnet test InvestIt.Tests/InvestIt.Tests.csproj

# Apply EF migrations
dotnet ef database update --project InvestIt
```

## Configuration

- **appsettings.json**: All config including connection strings, blockchain API URLs/keys/chain IDs, monitoring intervals, whale tracking thresholds, Serilog config
- **appsettings.Development.json**: Development overrides (DetailedErrors enabled)
- API keys are empty by default - configure via user secrets or environment variables for production
- `MonitoringSettings:PollingIntervalSeconds` controls the background polling loop (default: 30s)
- `WhaleTracking:ThresholdUsd` sets the whale detection threshold (default: $10,000)

## Architecture Patterns

- **Repository Pattern**: All database access goes through `IXxxRepository` interfaces
- **Background Services**: `BlockchainMonitoringHostedService` polls all chains; `NotificationProcessorHostedService` processes the notification queue
- **DI Scoping**: Background services create scopes to resolve scoped services (DbContext, repositories)
- **Chain Monitor Pattern**: Each blockchain has its own `IChainMonitorService` implementation, orchestrated by `BlockchainMonitoringService`
- **Whale Tracking Dual Strategy**: (1) GlobalWhaleMonitorService scans high-traffic DEX/token contracts, (2) WhaleTransactionMonitorService monitors known exchange wallets
- **Flexible API Response Handling**: `FlexibleResultConverter` custom JSON converter handles Etherscan responses that return either a string error or array result
- **Token Bucket Rate Limiting**: In-memory singleton `RateLimitService` with per-network buckets

## Key Conventions

- **Time Handling**: All timestamps stored in UTC. Display uses `TimeZoneHelper.ToCentralTime()` for US Central Time
- **Address Comparison**: Always case-insensitive (`ToLowerInvariant()`) for blockchain addresses
- **Transaction Classification**: Receiving = Buy, Sending = Sell, Neither = Transfer
- **Decimal Safety**: All blockchain amounts clamped to `10^15` max to prevent SQL Server `decimal(38,18)` overflow
- **Error Isolation**: Individual chain failures don't affect other chains; individual wallet failures don't affect other wallets
- **Structured Logging**: Uses Serilog with structured log properties (e.g., `{TxHash}`, `{Address}`)
- **Navigation Properties**: EF entities use `= null!` for required navigation properties

## Testing

- **Unit Tests**: Mock repositories/services with Moq, assert with FluentAssertions
- **Integration Tests**: Use EF Core InMemory provider with seeded test data
- **Test Naming**: `MethodName_Scenario_ExpectedResult` convention
- Tests are in `InvestIt.Tests/` project using xUnit

## Known Issues and Areas Needing Improvement

### Critical

1. **`.gitignore` blocked all `.md` files** (line 196) - This prevented README.md or CLAUDE.md from being committed. Fixed in this commit.

2. **`InvestIt.slnx` missing test project** - The solution file only references `InvestIt/InvestIt.csproj` but not `InvestIt.Tests/InvestIt.Tests.csproj`. IDEs won't discover the test project when opening the solution.

3. **`TransactionRepository.GetTransactionCountByDateAsync` ignores its `date` parameter** (`TransactionRepository.cs:154-162`) - The method accepts a `DateTime date` parameter but always queries using `TimeZoneHelper.GetTodayStartUtc()` / `GetTodayEndUtc()`, making the parameter useless.

4. **`TransactionRepository.GetRecentWithWhalePriorityAsync` loads ALL transactions into memory** (`TransactionRepository.cs:124-145`) - Both queries use `.ToListAsync()` without a `.Take()` limit before materialization. As the transaction table grows, this will cause memory exhaustion and degraded performance.

### Significant

5. **Massive code duplication across explorer clients** - `EtherscanClient`, `BscScanClient`, and `PolygonscanClient` have nearly identical implementations of `SafeParseDecimal`, `MapToBlockchainTransaction`, `GetTransactionsAsync`, and `MaxDatabaseDecimal`. These should be refactored into a shared base class.

6. **Massive code duplication across monitor services** - `EthereumMonitorService`, `BscMonitorService`, and `PolygonMonitorService` have nearly identical `MonitorWalletsAsync`, `MonitorSingleWalletAsync`, and `UpdateMonitoringStatusAsync` methods.

7. **Duplicate whale wallet logic** - Both `WhaleTransactionMonitorService` and `GlobalWhaleMonitorService` independently implement identical `EnsureWhaleWalletExistsAsync()` and `CalculateTransactionValueUsdAsync()` methods.

8. **`GlobalWhaleMonitorService._lastProcessedBlock` resets every cycle** (`GlobalWhaleMonitorService.cs:25`) - This is an instance field on a **scoped** service (registered via `AddScoped`). A new instance is created per DI scope (every monitoring cycle), so `_lastProcessedBlock` always starts at 0, defeating the "resume from last block" intent.

### Minor

9. **`FailureScenarios.cs` is in the main project** (`InvestIt/Tests/`) instead of the test project, and the "tests" are fake - they log messages but don't actually test real behavior (no assertions, no real API calls, no real DB interaction).

10. **`RateLimitService.RefreshTokensAsync`** (`RateLimitService.cs:64`) has a redundant `await Task.CompletedTask` after the finally block where all work is already done synchronously.

11. **No wallet address validation** - `Wallets/Add.cshtml.cs` stores any string as a wallet address without validating format (e.g., checking it's a valid hex address for EVM chains).

12. **Solana implementation is a placeholder** - `SolanaClient` returns empty results and `SolanaMonitorService` doesn't actually monitor, despite `Solnet.Rpc` being in dependencies.

13. **`GlobalWhaleMonitorService.GetBlockTransactionsAsync`** uses `new Random()` on each call (`GlobalWhaleMonitorService.cs:184`) instead of a shared instance, and the method doesn't actually fetch block transactions - it queries known contract addresses as a workaround.

14. **`TransactionRepository.AddAsync` uses raw SQL** (`TransactionRepository.cs:83-93`) instead of EF Core's standard `Add`/`SaveChanges` pattern, bypassing change tracking and potentially causing issues with InMemory provider in tests.
