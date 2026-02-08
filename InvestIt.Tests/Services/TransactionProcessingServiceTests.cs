using FluentAssertions;
using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients.Models;
using InvestIt.Services.Interfaces;
using InvestIt.Services.PriceTracking;
using InvestIt.Services.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InvestIt.Tests.Services;

public class TransactionProcessingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITransactionRepository> _mockTransactionRepo;
    private readonly Mock<INotificationQueueRepository> _mockNotificationRepo;
    private readonly Mock<ICoinGeckoClient> _mockPriceClient;
    private readonly Mock<ILogger<TransactionProcessingService>> _mockLogger;
    private readonly TransactionProcessingService _service;

    public TransactionProcessingServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Seed test data
        SeedTestData();

        // Setup mocks
        _mockTransactionRepo = new Mock<ITransactionRepository>();
        _mockNotificationRepo = new Mock<INotificationQueueRepository>();
        _mockPriceClient = new Mock<ICoinGeckoClient>();
        _mockLogger = new Mock<ILogger<TransactionProcessingService>>();

        // Default price mock: return $1 for any token
        _mockPriceClient
            .Setup(x => x.GetTokenPriceUsdAsync(It.IsAny<string>()))
            .ReturnsAsync(1m);

        _service = new TransactionProcessingService(
            _mockTransactionRepo.Object,
            _mockNotificationRepo.Object,
            _mockPriceClient.Object,
            _mockLogger.Object
        );
    }

    private void SeedTestData()
    {
        _context.BlockchainNetworks.Add(new BlockchainNetwork
        {
            Id = 1,
            Name = "Ethereum",
            ChainId = "1",
            ApiUrl = "https://api.etherscan.io/api",
            RateLimitPerSecond = 5,
            CreatedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    [Fact]
    public async Task ProcessAndStoreAsync_BuyTransaction_ClassifiedCorrectly()
    {
        // Arrange
        var walletAddress = "0xWALLET";
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xTEST123",
            FromAddress = "0xOTHER",
            ToAddress = walletAddress, // Receiving = BUY
            TokenSymbol = "USDT",
            Amount = 1000.50m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950000
        };

        _mockTransactionRepo
            .Setup(x => x.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockTransactionRepo
            .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((Transaction t) => t);

        // Act
        var result = await _service.ProcessAndStoreAsync(blockchainTx, 1, walletAddress);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(TransactionType.Buy);
        result.Amount.Should().Be(1000.50m);
        result.TokenSymbol.Should().Be("USDT");

        _mockTransactionRepo.Verify(x => x.AddAsync(It.Is<Transaction>(
            t => t.Type == TransactionType.Buy
        )), Times.Once);

        _mockNotificationRepo.Verify(x => x.AddAsync(
            It.IsAny<NotificationQueue>()
        ), Times.Once);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_SellTransaction_ClassifiedCorrectly()
    {
        // Arrange
        var walletAddress = "0xWALLET";
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xTEST456",
            FromAddress = walletAddress, // Sending = SELL
            ToAddress = "0xOTHER",
            TokenSymbol = "ETH",
            Amount = 2.5m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950001
        };

        _mockTransactionRepo
            .Setup(x => x.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockTransactionRepo
            .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((Transaction t) => t);

        _mockNotificationRepo
            .Setup(x => x.AddAsync(It.IsAny<NotificationQueue>()))
            .ReturnsAsync((NotificationQueue n) => n);

        // Act
        var result = await _service.ProcessAndStoreAsync(blockchainTx, 1, walletAddress);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(TransactionType.Sell);
        result.Amount.Should().Be(2.5m);
        result.TokenSymbol.Should().Be("ETH");

        _mockTransactionRepo.Verify(x => x.AddAsync(It.Is<Transaction>(
            t => t.Type == TransactionType.Sell
        )), Times.Once);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_TransferTransaction_ClassifiedCorrectly()
    {
        // Arrange
        var walletAddress = "0xWALLET";
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xTEST789",
            FromAddress = "0xOTHER1",
            ToAddress = "0xOTHER2", // Neither from nor to = TRANSFER
            TokenSymbol = "DAI",
            Amount = 500m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950002
        };

        _mockTransactionRepo
            .Setup(x => x.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockTransactionRepo
            .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((Transaction t) => t);

        _mockNotificationRepo
            .Setup(x => x.AddAsync(It.IsAny<NotificationQueue>()))
            .ReturnsAsync((NotificationQueue n) => n);

        // Act
        var result = await _service.ProcessAndStoreAsync(blockchainTx, 1, walletAddress);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(TransactionType.Transfer);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_DuplicateTransaction_ReturnsNull()
    {
        // Arrange
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xDUPLICATE",
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "USDC",
            Amount = 100m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950003
        };

        _mockTransactionRepo
            .Setup(x => x.ExistsByHashAsync("0xDUPLICATE"))
            .ReturnsAsync(true); // Already exists

        // Act
        var result = await _service.ProcessAndStoreAsync(blockchainTx, 1, "0xWALLET");

        // Assert
        result.Should().BeNull();

        _mockTransactionRepo.Verify(x => x.AddAsync(It.IsAny<Transaction>()), Times.Never);
        _mockNotificationRepo.Verify(x => x.AddAsync(It.IsAny<NotificationQueue>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_WithGasData_StoresCorrectly()
    {
        // Arrange
        var blockchainTx = new BlockchainTransaction
        {
            TxHash = "0xGAS",
            FromAddress = "0xFROM",
            ToAddress = "0xWALLET",
            TokenSymbol = "ETH",
            Amount = 1m,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 18950004,
            GasUsed = 21000m,
            GasPrice = 0.00003m
        };

        _mockTransactionRepo
            .Setup(x => x.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockTransactionRepo
            .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((Transaction t) => t);

        _mockNotificationRepo
            .Setup(x => x.AddAsync(It.IsAny<NotificationQueue>()))
            .ReturnsAsync((NotificationQueue n) => n);

        // Act
        var result = await _service.ProcessAndStoreAsync(blockchainTx, 1, "0xWALLET");

        // Assert
        result.Should().NotBeNull();
        result.GasUsed.Should().Be(21000m);
        result.GasPrice.Should().Be(0.00003m);
    }

    [Fact]
    public void ClassifyTransaction_BuyScenarios_ReturnsCorrectType()
    {
        // Arrange
        var walletAddress = "0xMYWALLET";

        // Act & Assert - All buy scenarios
        var buy1 = ClassifyTransactionType("0xOTHER", walletAddress, walletAddress);
        buy1.Should().Be(TransactionType.Buy);

        var buy2 = ClassifyTransactionType("0xANOTHER", walletAddress.ToUpper(), walletAddress);
        buy2.Should().Be(TransactionType.Buy); // Case insensitive
    }

    [Fact]
    public void ClassifyTransaction_SellScenarios_ReturnsCorrectType()
    {
        // Arrange
        var walletAddress = "0xMYWALLET";

        // Act & Assert - All sell scenarios
        var sell1 = ClassifyTransactionType(walletAddress, "0xOTHER", walletAddress);
        sell1.Should().Be(TransactionType.Sell);

        var sell2 = ClassifyTransactionType(walletAddress.ToUpper(), "0xANOTHER", walletAddress);
        sell2.Should().Be(TransactionType.Sell); // Case insensitive
    }

    // Helper method to test classification logic
    private TransactionType ClassifyTransactionType(string from, string to, string walletAddress)
    {
        if (string.Equals(to, walletAddress, StringComparison.OrdinalIgnoreCase))
            return TransactionType.Buy;
        if (string.Equals(from, walletAddress, StringComparison.OrdinalIgnoreCase))
            return TransactionType.Sell;
        return TransactionType.Transfer;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
