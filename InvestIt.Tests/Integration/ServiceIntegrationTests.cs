using FluentAssertions;
using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Implementations;
using InvestIt.Services.ExplorerClients.Models;
using InvestIt.Services.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InvestIt.Tests.Integration;

/// <summary>
/// Integration tests for services working with real repositories and database
/// </summary>
public class ServiceIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionProcessingService _processingService;
    private readonly Mock<ILogger<TransactionProcessingService>> _mockLogger;

    public ServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ServiceIntTest_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        SeedTestData();

        // Real repositories
        var transactionRepo = new TransactionRepository(_context);
        var notificationRepo = new NotificationQueueRepository(_context);

        _mockLogger = new Mock<ILogger<TransactionProcessingService>>();

        _processingService = new TransactionProcessingService(
            transactionRepo,
            notificationRepo,
            _mockLogger.Object
        );
    }

    private void SeedTestData()
    {
        var seedDate = new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc);

        _context.BlockchainNetworks.Add(new BlockchainNetwork
        {
            Id = 1,
            Name = "Ethereum",
            ChainId = "1",
            ApiUrl = "https://api.etherscan.io/api",
            RateLimitPerSecond = 5,
            CreatedAt = seedDate
        });

        _context.MonitoredWallets.Add(new MonitoredWallet
        {
            Id = 1,
            Address = "0xTEST_WALLET",
            Label = "Test Wallet",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    [Fact]
    public async Task ProcessAndStoreAsync_EndToEnd_StoresInDatabase()
    {
        // Arrange
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xABC123",
            FromAddress = "0xOTHER",
            ToAddress = "0xTEST_WALLET", // BUY
            TokenSymbol = "USDT",
            TokenAddress = "0xdac17f958d2ee523a2206206994597c13d831ec7",
            Amount = 1000m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950000,
            GasUsed = 21000m,
            GasPrice = 0.00003m
        };

        // Act
        var result = await _processingService.ProcessAndStoreAsync(
            blockchainTx,
            walletId: 1,
            monitoredWalletAddress: "0xTEST_WALLET"
        );

        // Assert - Service returned correct result
        result.Should().NotBeNull();
        result!.Type.Should().Be(TransactionType.Buy);
        result.Amount.Should().Be(1000m);
        result.TokenSymbol.Should().Be("USDT");

        // Assert - Data persisted to database
        var dbTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TxHash == "0xABC123");

        dbTransaction.Should().NotBeNull();
        dbTransaction!.Type.Should().Be(TransactionType.Buy);
        dbTransaction.Amount.Should().Be(1000m);
        dbTransaction.GasUsed.Should().Be(21000m);

        // Assert - Notification queued
        var notification = await _context.NotificationQueues
            .FirstOrDefaultAsync(n => n.TransactionId == result.Id);

        notification.Should().NotBeNull();
        notification!.Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_DuplicateTransaction_DoesNotInsert()
    {
        // Arrange - Insert first transaction
        var blockchainTx1 = new BlockchainTransaction
        {
            TxHash = "0xDUPLICATE",
            FromAddress = "0xOTHER",
            ToAddress = "0xTEST_WALLET",
            TokenSymbol = "ETH",
            Amount = 1m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950000
        };

        await _processingService.ProcessAndStoreAsync(blockchainTx1, 1, "0xTEST_WALLET");

        // Act - Try to insert duplicate
        var blockchainTx2 = new BlockchainTransaction
        {
            TxHash = "0xDUPLICATE", // Same hash
            FromAddress = "0xOTHER",
            ToAddress = "0xTEST_WALLET",
            TokenSymbol = "ETH",
            Amount = 2m, // Different amount
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950001
        };

        var result = await _processingService.ProcessAndStoreAsync(blockchainTx2, 1, "0xTEST_WALLET");

        // Assert - Duplicate rejected
        result.Should().BeNull();

        // Assert - Only one transaction in database
        var txCount = await _context.Transactions.CountAsync(t => t.TxHash == "0xDUPLICATE");
        txCount.Should().Be(1);

        // Original amount preserved
        var dbTx = await _context.Transactions.FirstAsync(t => t.TxHash == "0xDUPLICATE");
        dbTx.Amount.Should().Be(1m); // Original, not 2m
    }

    [Fact]
    public async Task ProcessAndStoreAsync_MultipleTransactions_AllStored()
    {
        // Arrange
        var transactions = new[]
        {
            new BlockchainTransaction
            {
                TxHash = "0xTX1",
                FromAddress = "0xOTHER",
                ToAddress = "0xTEST_WALLET",
                TokenSymbol = "ETH",
                Amount = 1m,
                Timestamp = DateTime.UtcNow,
                BlockNumber = 1
            },
            new BlockchainTransaction
            {
                TxHash = "0xTX2",
                FromAddress = "0xTEST_WALLET",
                ToAddress = "0xOTHER",
                TokenSymbol = "USDT",
                Amount = 100m,
                Timestamp = DateTime.UtcNow,
                BlockNumber = 2
            },
            new BlockchainTransaction
            {
                TxHash = "0xTX3",
                FromAddress = "0xA",
                ToAddress = "0xB",
                TokenSymbol = "DAI",
                Amount = 50m,
                Timestamp = DateTime.UtcNow,
                BlockNumber = 3
            }
        };

        // Act
        foreach (var tx in transactions)
        {
            await _processingService.ProcessAndStoreAsync(tx, 1, "0xTEST_WALLET");
        }

        // Assert
        var dbTransactions = await _context.Transactions.ToListAsync();
        dbTransactions.Should().HaveCount(3);

        dbTransactions.Should().Contain(t => t.Type == TransactionType.Buy && t.TokenSymbol == "ETH");
        dbTransactions.Should().Contain(t => t.Type == TransactionType.Sell && t.TokenSymbol == "USDT");
        dbTransactions.Should().Contain(t => t.Type == TransactionType.Transfer && t.TokenSymbol == "DAI");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_CaseInsensitiveAddressMatching_WorksCorrectly()
    {
        // Arrange - Mixed case addresses
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xCASE_TEST",
            FromAddress = "0xOTHER",
            ToAddress = "0xtest_wallet", // lowercase
            TokenSymbol = "ETH",
            Amount = 1m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1
        };

        // Act - Compare with uppercase
        var result = await _processingService.ProcessAndStoreAsync(
            blockchainTx,
            1,
            "0xTEST_WALLET" // uppercase
        );

        // Assert - Should still classify as BUY
        result.Should().NotBeNull();
        result!.Type.Should().Be(TransactionType.Buy);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_WithNavigationProperties_LoadsCorrectly()
    {
        // Arrange
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xNAV_TEST",
            FromAddress = "0xOTHER",
            ToAddress = "0xTEST_WALLET",
            TokenSymbol = "ETH",
            Amount = 1m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1
        };

        // Act
        await _processingService.ProcessAndStoreAsync(blockchainTx, 1, "0xTEST_WALLET");

        // Assert - Load with navigation
        var dbTx = await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .FirstAsync(t => t.TxHash == "0xNAV_TEST");

        dbTx.Wallet.Should().NotBeNull();
        dbTx.Wallet.Address.Should().Be("0xTEST_WALLET");
        dbTx.Wallet.BlockchainNetwork.Should().NotBeNull();
        dbTx.Wallet.BlockchainNetwork.Name.Should().Be("Ethereum");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_TransactionTypes_ClassifiedCorrectly()
    {
        // Arrange
        var buyTx = new BlockchainTransaction
        {
            TxHash = "0xBUY",
            FromAddress = "0xDEX",
            ToAddress = "0xTEST_WALLET",
            TokenSymbol = "USDT",
            Amount = 1000m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1
        };

        var sellTx = new BlockchainTransaction
        {
            TxHash = "0xSELL",
            FromAddress = "0xTEST_WALLET",
            ToAddress = "0xDEX",
            TokenSymbol = "ETH",
            Amount = 1m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 2
        };

        var transferTx = new BlockchainTransaction
        {
            TxHash = "0xTRANSFER",
            FromAddress = "0xALICE",
            ToAddress = "0xBOB",
            TokenSymbol = "DAI",
            Amount = 100m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 3
        };

        // Act
        await _processingService.ProcessAndStoreAsync(buyTx, 1, "0xTEST_WALLET");
        await _processingService.ProcessAndStoreAsync(sellTx, 1, "0xTEST_WALLET");
        await _processingService.ProcessAndStoreAsync(transferTx, 1, "0xTEST_WALLET");

        // Assert
        var buyResult = await _context.Transactions.FirstAsync(t => t.TxHash == "0xBUY");
        var sellResult = await _context.Transactions.FirstAsync(t => t.TxHash == "0xSELL");
        var transferResult = await _context.Transactions.FirstAsync(t => t.TxHash == "0xTRANSFER");

        buyResult.Type.Should().Be(TransactionType.Buy);
        sellResult.Type.Should().Be(TransactionType.Sell);
        transferResult.Type.Should().Be(TransactionType.Transfer);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_NotificationQueue_CreatedForEachTransaction()
    {
        // Arrange
        var transactions = new[]
        {
            new BlockchainTransaction { TxHash = "0xN1", FromAddress = "0xA", ToAddress = "0xTEST_WALLET", TokenSymbol = "ETH", Amount = 1m, Timestamp = DateTime.UtcNow, BlockNumber = 1 },
            new BlockchainTransaction { TxHash = "0xN2", FromAddress = "0xA", ToAddress = "0xTEST_WALLET", TokenSymbol = "USDT", Amount = 100m, Timestamp = DateTime.UtcNow, BlockNumber = 2 },
            new BlockchainTransaction { TxHash = "0xN3", FromAddress = "0xA", ToAddress = "0xTEST_WALLET", TokenSymbol = "DAI", Amount = 50m, Timestamp = DateTime.UtcNow, BlockNumber = 3 }
        };

        // Act
        foreach (var tx in transactions)
        {
            await _processingService.ProcessAndStoreAsync(tx, 1, "0xTEST_WALLET");
        }

        // Assert - 3 notifications created
        var notifications = await _context.NotificationQueues.ToListAsync();
        notifications.Should().HaveCount(3);
        notifications.Should().AllSatisfy(n => n.Status.Should().Be(NotificationStatus.Pending));
    }

    [Fact]
    public async Task TransactionRepository_QueryPerformance_WithIndex()
    {
        // Arrange - Insert 100 transactions
        var wallet = await _context.MonitoredWallets.FirstAsync();

        for (int i = 0; i < 100; i++)
        {
            _context.Transactions.Add(new Transaction
            {
                TxHash = $"0xPERF{i:D5}",
                WalletId = wallet.Id,
                FromAddress = "0xFROM",
                ToAddress = "0xTO",
                TokenSymbol = i % 5 == 0 ? "ETH" : "USDT",
                Amount = i,
                Type = i % 2 == 0 ? TransactionType.Buy : TransactionType.Sell,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                BlockNumber = i,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await _context.SaveChangesAsync();

        // Act - Query by wallet (should use index)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await _context.Transactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();
        sw.Stop();

        // Assert
        results.Should().HaveCount(10);
        sw.ElapsedMilliseconds.Should().BeLessThan(100); // Should be very fast with index
    }

    [Fact]
    public async Task ConcurrentTransactionInsertion_NoRaceConditions()
    {
        // Arrange - 10 concurrent transactions
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            var tx = new BlockchainTransaction
            {
                TxHash = $"0xCONC{index}",
                FromAddress = "0xFROM",
                ToAddress = "0xTEST_WALLET",
                TokenSymbol = "ETH",
                Amount = index,
                Timestamp = DateTime.UtcNow,
                BlockNumber = index
            };

            tasks.Add(_processingService.ProcessAndStoreAsync(tx, 1, "0xTEST_WALLET"));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All 10 inserted successfully
        var count = await _context.Transactions.CountAsync();
        count.Should().Be(10);

        // All unique transaction hashes
        var hashes = await _context.Transactions.Select(t => t.TxHash).Distinct().ToListAsync();
        hashes.Should().HaveCount(10);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
