using FluentAssertions;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.ExplorerClients.Models;
using InvestIt.Services.Interfaces;
using InvestIt.Services.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InvestIt.Tests.Services;

public class EthereumMonitorServiceTests
{
    private readonly Mock<EtherscanClient> _mockApiClient;
    private readonly Mock<IWalletRepository> _mockWalletRepo;
    private readonly Mock<ITransactionProcessingService> _mockTransactionProcessor;
    private readonly Mock<IMonitoringStatusRepository> _mockStatusRepo;
    private readonly Mock<IRateLimitService> _mockRateLimitService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<EthereumMonitorService>> _mockLogger;
    private readonly EthereumMonitorService _service;

    public EthereumMonitorServiceTests()
    {
        _mockApiClient = new Mock<EtherscanClient>(MockBehavior.Loose, null, null);
        _mockWalletRepo = new Mock<IWalletRepository>();
        _mockTransactionProcessor = new Mock<ITransactionProcessingService>();
        _mockStatusRepo = new Mock<IMonitoringStatusRepository>();
        _mockRateLimitService = new Mock<IRateLimitService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<EthereumMonitorService>>();

        // Setup default configuration
        _mockConfiguration.Setup(c => c["MonitoringSettings:MaxTransactionsPerPoll"])
            .Returns("100");

        _service = new EthereumMonitorService(
            _mockApiClient.Object,
            _mockWalletRepo.Object,
            _mockTransactionProcessor.Object,
            _mockStatusRepo.Object,
            _mockRateLimitService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void NetworkProperties_ShouldBeCorrect()
    {
        // Assert
        _service.NetworkName.Should().Be("Ethereum");
        _service.NetworkId.Should().Be(1);
    }

    [Fact]
    public async Task MonitorWalletsAsync_NoActiveWallets_SkipsMonitoring()
    {
        // Arrange
        var emptyWalletList = new List<MonitoredWallet>();
        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(emptyWalletList);

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockApiClient.Verify(x => x.GetTransactionsAsync(
            It.IsAny<string>(),
            It.IsAny<int>()),
            Times.Never,
            "Should not call API when no active wallets");

        _mockStatusRepo.Verify(x => x.UpsertAsync(It.Is<MonitoringStatus>(
            s => s.IsHealthy == true)),
            Times.Once,
            "Should update status as healthy");
    }

    [Fact]
    public async Task MonitorWalletsAsync_WithActiveWallet_ProcessesTransactions()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Id = 1,
            Address = "0xTEST",
            IsActive = true,
            BlockchainNetworkId = 1
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(new List<MonitoredWallet> { wallet });

        _mockWalletRepo
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(wallet);

        _mockRateLimitService
            .Setup(x => x.TryAcquireTokenAsync(1))
            .ReturnsAsync(true);

        var mockTransactions = new List<BlockchainTransaction>
        {
            new BlockchainTransaction
            {
                TxHash = "0xABC123",
                FromAddress = "0xOTHER",
                ToAddress = "0xTEST",
                TokenSymbol = "ETH",
                Amount = 1.5m,
                Timestamp = DateTime.UtcNow
            }
        };

        _mockApiClient
            .Setup(x => x.GetTransactionsAsync("0xTEST", 100))
            .ReturnsAsync(mockTransactions);

        _mockTransactionProcessor
            .Setup(x => x.ProcessAndStoreAsync(It.IsAny<BlockchainTransaction>(), 1, "0xTEST"))
            .ReturnsAsync(new Transaction
            {
                Id = 1,
                Type = TransactionType.Buy,
                TxHash = "0xABC123",
                FromAddress = "0xOTHER",
                ToAddress = "0xTEST"
            });

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockApiClient.Verify(x => x.GetTransactionsAsync("0xTEST", 100), Times.Once);
        _mockTransactionProcessor.Verify(x => x.ProcessAndStoreAsync(
            It.Is<BlockchainTransaction>(tx => tx.TxHash == "0xABC123"),
            1,
            "0xTEST"), Times.Once);
        _mockWalletRepo.Verify(x => x.UpdateAsync(It.Is<MonitoredWallet>(
            w => w.LastCheckedAt != null)), Times.Once);
    }

    [Fact]
    public async Task MonitorWalletsAsync_RateLimitExceeded_SkipsWallet()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Id = 1,
            Address = "0xTEST",
            IsActive = true,
            BlockchainNetworkId = 1
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(new List<MonitoredWallet> { wallet });

        _mockRateLimitService
            .Setup(x => x.TryAcquireTokenAsync(1))
            .ReturnsAsync(false); // Rate limit exceeded

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockApiClient.Verify(x => x.GetTransactionsAsync(
            It.IsAny<string>(),
            It.IsAny<int>()),
            Times.Never,
            "Should not call API when rate limited");
    }

    [Fact]
    public async Task MonitorWalletsAsync_InactiveWallet_Skipped()
    {
        // Arrange
        var inactiveWallet = new MonitoredWallet
        {
            Id = 1,
            Address = "0xINACTIVE",
            IsActive = false, // Inactive
            BlockchainNetworkId = 1
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(new List<MonitoredWallet> { inactiveWallet });

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockApiClient.Verify(x => x.GetTransactionsAsync(
            It.IsAny<string>(),
            It.IsAny<int>()),
            Times.Never,
            "Inactive wallets should not be monitored");
    }

    [Fact]
    public async Task MonitorWalletsAsync_MultipleWallets_ProcessesAll()
    {
        // Arrange
        var wallets = new List<MonitoredWallet>
        {
            new MonitoredWallet { Id = 1, Address = "0xWALLET1", IsActive = true, BlockchainNetworkId = 1 },
            new MonitoredWallet { Id = 2, Address = "0xWALLET2", IsActive = true, BlockchainNetworkId = 1 },
            new MonitoredWallet { Id = 3, Address = "0xWALLET3", IsActive = true, BlockchainNetworkId = 1 }
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(wallets);

        foreach (var wallet in wallets)
        {
            _mockWalletRepo
                .Setup(x => x.GetByIdAsync(wallet.Id))
                .ReturnsAsync(wallet);
        }

        _mockRateLimitService
            .Setup(x => x.TryAcquireTokenAsync(1))
            .ReturnsAsync(true);

        _mockApiClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<string>(), 100))
            .ReturnsAsync(new List<BlockchainTransaction>());

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockApiClient.Verify(x => x.GetTransactionsAsync(It.IsAny<string>(), 100), Times.Exactly(3));
        _mockWalletRepo.Verify(x => x.UpdateAsync(It.IsAny<MonitoredWallet>()), Times.Exactly(3));
    }

    [Fact]
    public async Task MonitorWalletsAsync_ApiException_UpdatesStatusUnhealthy()
    {
        // Arrange
        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ThrowsAsync(new Exception("Database error"));

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockStatusRepo.Verify(x => x.UpsertAsync(It.Is<MonitoringStatus>(
            s => s.IsHealthy == false &&
                 s.LastErrorMessage == "Database error")),
            Times.Once);
    }

    [Fact]
    public async Task MonitorWalletsAsync_UpdatesMonitoringStatus()
    {
        // Arrange
        var existingStatus = new MonitoringStatus
        {
            NetworkId = 1,
            IsHealthy = true,
            ErrorCount = 0,
            LastPollTime = DateTime.UtcNow.AddMinutes(-1)
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(new List<MonitoredWallet>());

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync(existingStatus);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockStatusRepo.Verify(x => x.UpsertAsync(It.Is<MonitoringStatus>(
            s => s.NetworkId == 1 &&
                 s.IsHealthy == true &&
                 s.LastPollTime > existingStatus.LastPollTime)),
            Times.Once);
    }

    [Fact]
    public async Task MonitorWalletsAsync_NoNewTransactions_StillUpdatesTimestamp()
    {
        // Arrange
        var wallet = new MonitoredWallet
        {
            Id = 1,
            Address = "0xTEST",
            IsActive = true,
            BlockchainNetworkId = 1,
            LastCheckedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockWalletRepo
            .Setup(x => x.GetByNetworkAsync(1))
            .ReturnsAsync(new List<MonitoredWallet> { wallet });

        _mockWalletRepo
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(wallet);

        _mockRateLimitService
            .Setup(x => x.TryAcquireTokenAsync(1))
            .ReturnsAsync(true);

        _mockApiClient
            .Setup(x => x.GetTransactionsAsync("0xTEST", 100))
            .ReturnsAsync(new List<BlockchainTransaction>()); // No transactions

        _mockStatusRepo
            .Setup(x => x.GetByNetworkIdAsync(1))
            .ReturnsAsync((MonitoringStatus?)null);

        // Act
        await _service.MonitorWalletsAsync();

        // Assert
        _mockWalletRepo.Verify(x => x.UpdateAsync(It.Is<MonitoredWallet>(
            w => w.LastCheckedAt > wallet.LastCheckedAt)),
            Times.Once,
            "LastCheckedAt should be updated even with no new transactions");
    }
}
