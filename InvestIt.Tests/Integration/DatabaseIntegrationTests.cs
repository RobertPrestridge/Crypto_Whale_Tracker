using FluentAssertions;
using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Implementations;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InvestIt.Tests.Integration;

/// <summary>
/// Integration tests for database operations with real EF Core context
/// </summary>
public class DatabaseIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IWalletRepository _walletRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IBlockchainNetworkRepository _networkRepo;
    private readonly IMonitoringStatusRepository _statusRepo;
    private readonly INotificationQueueRepository _notificationRepo;

    public DatabaseIntegrationTests()
    {
        // Create unique in-memory database per test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"IntegrationTest_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Initialize repositories with real context
        _walletRepo = new WalletRepository(_context);
        _transactionRepo = new TransactionRepository(_context);
        _networkRepo = new BlockchainNetworkRepository(_context);
        _statusRepo = new MonitoringStatusRepository(_context);
        _notificationRepo = new NotificationQueueRepository(_context);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var seedDate = new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc);

        var networks = new[]
        {
            new BlockchainNetwork
            {
                Id = 1,
                Name = "Ethereum",
                ChainId = "1",
                ApiUrl = "https://api.etherscan.io/api",
                RateLimitPerSecond = 5,
                CreatedAt = seedDate
            },
            new BlockchainNetwork
            {
                Id = 2,
                Name = "BSC",
                ChainId = "56",
                ApiUrl = "https://api.bscscan.com/api",
                RateLimitPerSecond = 5,
                CreatedAt = seedDate
            }
        };

        _context.BlockchainNetworks.AddRange(networks);
        _context.SaveChanges();
    }

    [Fact]
    public async Task WalletRepository_AddAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Address = "0xTEST123",
            Label = "Test Wallet",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var addedWallet = await _walletRepo.AddAsync(wallet);
        var retrievedWallet = await _walletRepo.GetByIdAsync(addedWallet.Id);

        // Assert
        retrievedWallet.Should().NotBeNull();
        retrievedWallet!.Address.Should().Be("0xTEST123");
        retrievedWallet.Label.Should().Be("Test Wallet");
        retrievedWallet.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task WalletRepository_GetByNetwork_ReturnsCorrectWallets()
    {
        // Arrange
        var ethWallet = new MonitoredWallet
        {
            Address = "0xETH1",
            Label = "ETH Wallet",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var bscWallet = new MonitoredWallet
        {
            Address = "0xBSC1",
            Label = "BSC Wallet",
            BlockchainNetworkId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _walletRepo.AddAsync(ethWallet);
        await _walletRepo.AddAsync(bscWallet);

        // Act
        var ethWallets = await _walletRepo.GetByNetworkAsync(1);
        var bscWallets = await _walletRepo.GetByNetworkAsync(2);

        // Assert
        ethWallets.Should().HaveCount(1);
        ethWallets.First().Address.Should().Be("0xETH1");
        bscWallets.Should().HaveCount(1);
        bscWallets.First().Address.Should().Be("0xBSC1");
    }

    [Fact]
    public async Task TransactionRepository_AddAndRetrieve_WorksCorrectly()
    {
        // Arrange - Add wallet first
        var wallet = new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var addedWallet = await _walletRepo.AddAsync(wallet);

        var transaction = new Transaction
        {
            TxHash = "0xABC123",
            WalletId = addedWallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1.5m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950000,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var addedTx = await _transactionRepo.AddAsync(transaction);
        var retrievedTx = await _transactionRepo.GetByIdAsync(addedTx.Id);

        // Assert
        retrievedTx.Should().NotBeNull();
        retrievedTx!.TxHash.Should().Be("0xABC123");
        retrievedTx.Amount.Should().Be(1.5m);
        retrievedTx.Type.Should().Be(TransactionType.Buy);
    }

    [Fact]
    public async Task TransactionRepository_ExistsByHash_WorksCorrectly()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var addedWallet = await _walletRepo.AddAsync(wallet);

        var transaction = new Transaction
        {
            TxHash = "0xUNIQUE",
            WalletId = addedWallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950000,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepo.AddAsync(transaction);

        // Act
        var exists = await _transactionRepo.ExistsByHashAsync("0xUNIQUE");
        var notExists = await _transactionRepo.ExistsByHashAsync("0xNOTFOUND");

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task TransactionRepository_GetRecent_ReturnsLatest()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var addedWallet = await _walletRepo.AddAsync(wallet);

        // Add 5 transactions
        for (int i = 0; i < 5; i++)
        {
            var tx = new Transaction
            {
                TxHash = $"0xTX{i}",
                WalletId = addedWallet.Id,
                FromAddress = "0xFROM",
                ToAddress = "0xTO",
                TokenSymbol = "ETH",
                Amount = i + 1m,
                Type = TransactionType.Buy,
                Timestamp = DateTime.UtcNow.AddMinutes(i),
                BlockNumber = 18950000 + i,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            };
            await _transactionRepo.AddAsync(tx);
        }

        // Act
        var recent = await _transactionRepo.GetRecentAsync(3);

        // Assert
        recent.Should().HaveCount(3);
        // Most recent first (CreatedAt DESC)
        recent.First().TxHash.Should().Be("0xTX4");
        recent.Last().TxHash.Should().Be("0xTX2");
    }

    [Fact]
    public async Task TransactionRepository_GetByWallet_FiltersByWalletId()
    {
        // Arrange
        var wallet1 = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET1",
            Label = "W1",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var wallet2 = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET2",
            Label = "W2",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xW1TX1",
            WalletId = wallet1.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xW2TX1",
            WalletId = wallet2.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 2m,
            Type = TransactionType.Sell,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var wallet1Txs = await _transactionRepo.GetByWalletAsync(wallet1.Id);
        var wallet2Txs = await _transactionRepo.GetByWalletAsync(wallet2.Id);

        // Assert
        wallet1Txs.Should().HaveCount(1);
        wallet1Txs.First().TxHash.Should().Be("0xW1TX1");
        wallet2Txs.Should().HaveCount(1);
        wallet2Txs.First().TxHash.Should().Be("0xW2TX1");
    }

    [Fact]
    public async Task TransactionRepository_GetByTokenSymbol_FiltersByToken()
    {
        // Arrange
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xETH1",
            WalletId = wallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xUSDT1",
            WalletId = wallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "USDT",
            Amount = 100m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var ethTxs = await _transactionRepo.GetByTokenSymbolAsync("ETH");
        var usdtTxs = await _transactionRepo.GetByTokenSymbolAsync("USDT");

        // Assert
        ethTxs.Should().HaveCount(1);
        ethTxs.First().TokenSymbol.Should().Be("ETH");
        usdtTxs.Should().HaveCount(1);
        usdtTxs.First().TokenSymbol.Should().Be("USDT");
    }

    [Fact]
    public async Task TransactionRepository_GetByType_FiltersByTransactionType()
    {
        // Arrange
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xBUY1",
            WalletId = wallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xWALLET",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xSELL1",
            WalletId = wallet.Id,
            FromAddress = "0xWALLET",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Sell,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var buyTxs = await _transactionRepo.GetByTypeAsync(TransactionType.Buy);
        var sellTxs = await _transactionRepo.GetByTypeAsync(TransactionType.Sell);

        // Assert
        buyTxs.Should().HaveCount(1);
        buyTxs.First().Type.Should().Be(TransactionType.Buy);
        sellTxs.Should().HaveCount(1);
        sellTxs.First().Type.Should().Be(TransactionType.Sell);
    }

    [Fact]
    public async Task NotificationQueueRepository_AddAndGetPending_WorksCorrectly()
    {
        // Arrange
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var tx = await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xTX1",
            WalletId = wallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        var notification = new NotificationQueue
        {
            TransactionId = tx.Id,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _notificationRepo.AddAsync(notification);
        var pendingNotifications = await _notificationRepo.GetPendingAsync(10);

        // Assert
        pendingNotifications.Should().HaveCount(1);
        pendingNotifications.First().TransactionId.Should().Be(tx.Id);
        pendingNotifications.First().Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public async Task MonitoringStatusRepository_UpsertAsync_InsertsAndUpdates()
    {
        // Arrange
        var status = new MonitoringStatus
        {
            NetworkId = 1,
            LastPollTime = DateTime.UtcNow,
            IsHealthy = true,
            ErrorCount = 0,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - First insert
        await _statusRepo.UpsertAsync(status);
        var retrieved1 = await _statusRepo.GetByNetworkIdAsync(1);

        // Update
        retrieved1!.ErrorCount = 5;
        retrieved1.IsHealthy = false;
        await _statusRepo.UpsertAsync(retrieved1);
        var retrieved2 = await _statusRepo.GetByNetworkIdAsync(1);

        // Assert
        retrieved2.Should().NotBeNull();
        retrieved2!.ErrorCount.Should().Be(5);
        retrieved2.IsHealthy.Should().BeFalse();
        retrieved2.NetworkId.Should().Be(1);
    }

    [Fact]
    public async Task WalletRepository_Update_ModifiesExistingWallet()
    {
        // Arrange
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Original Label",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        wallet.Label = "Updated Label";
        wallet.LastCheckedAt = DateTime.UtcNow;
        await _walletRepo.UpdateAsync(wallet);

        var updated = await _walletRepo.GetByIdAsync(wallet.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.Label.Should().Be("Updated Label");
        updated.LastCheckedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionRepository_GetCountByWallet_ReturnsCorrectCount()
    {
        // Arrange
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        for (int i = 0; i < 7; i++)
        {
            await _transactionRepo.AddAsync(new Transaction
            {
                TxHash = $"0xTX{i}",
                WalletId = wallet.Id,
                FromAddress = "0xFROM",
                ToAddress = "0xTO",
                TokenSymbol = "ETH",
                Amount = 1m,
                Type = TransactionType.Buy,
                Timestamp = DateTime.UtcNow,
                BlockNumber = i,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Act
        var count = await _transactionRepo.GetCountByWalletAsync(wallet.Id);

        // Assert
        count.Should().Be(7);
    }

    [Fact]
    public async Task DatabaseRelationships_NavigationProperties_WorkCorrectly()
    {
        // Arrange
        var network = await _networkRepo.GetByIdAsync(1);
        var wallet = await _walletRepo.AddAsync(new MonitoredWallet
        {
            Address = "0xWALLET",
            Label = "Test",
            BlockchainNetworkId = network!.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var tx = await _transactionRepo.AddAsync(new Transaction
        {
            TxHash = "0xTX1",
            WalletId = wallet.Id,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        // Act - Load with navigation properties
        var walletWithTx = await _context.MonitoredWallets
            .Include(w => w.Transactions)
            .Include(w => w.BlockchainNetwork)
            .FirstAsync(w => w.Id == wallet.Id);

        // Assert
        walletWithTx.Transactions.Should().HaveCount(1);
        walletWithTx.Transactions.First().TxHash.Should().Be("0xTX1");
        walletWithTx.BlockchainNetwork.Name.Should().Be("Ethereum");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
