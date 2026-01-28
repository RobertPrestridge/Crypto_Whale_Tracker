using FluentAssertions;
using InvestIt.Services.Interfaces;
using InvestIt.Services.RateLimiting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InvestIt.Tests.Services;

public class RateLimitServiceTests
{
    private readonly Mock<ILogger<RateLimitService>> _mockLogger;
    private readonly IRateLimitService _service;

    public RateLimitServiceTests()
    {
        _mockLogger = new Mock<ILogger<RateLimitService>>();
        _service = new RateLimitService(_mockLogger.Object);
    }

    [Fact]
    public async Task TryAcquireTokenAsync_WithinLimit_ReturnsTrue()
    {
        // Arrange
        int networkId = 1; // Ethereum (5 req/sec)

        // Act
        var result = await _service.TryAcquireTokenAsync(networkId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireTokenAsync_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        int networkId = 1; // Ethereum (5 req/sec)

        // Act - Exhaust all 5 tokens
        for (int i = 0; i < 5; i++)
        {
            var acquired = await _service.TryAcquireTokenAsync(networkId);
            acquired.Should().BeTrue($"token {i + 1} should be acquired");
        }

        // Act - Try to acquire 6th token
        var result = await _service.TryAcquireTokenAsync(networkId);

        // Assert
        result.Should().BeFalse("bucket should be empty after 5 acquisitions");
    }

    [Fact]
    public async Task TryAcquireTokenAsync_SolanaLowerLimit_ReturnsCorrectTokens()
    {
        // Arrange
        int networkId = 4; // Solana (3 req/sec)

        // Act - Exhaust all 3 tokens
        var results = new List<bool>();
        for (int i = 0; i < 4; i++)
        {
            results.Add(await _service.TryAcquireTokenAsync(networkId));
        }

        // Assert
        results.Take(3).Should().AllBeEquivalentTo(true);
        results[3].Should().BeFalse("4th token should fail for Solana");
    }

    [Fact]
    public async Task RefreshTokensAsync_RestoresAllBuckets()
    {
        // Arrange
        int networkId = 1; // Ethereum (5 req/sec)

        // Exhaust tokens
        for (int i = 0; i < 5; i++)
        {
            await _service.TryAcquireTokenAsync(networkId);
        }

        // Verify exhausted
        var exhaustedResult = await _service.TryAcquireTokenAsync(networkId);
        exhaustedResult.Should().BeFalse();

        // Act - Refresh
        await _service.RefreshTokensAsync();

        // Assert - Tokens restored
        var refreshedResult = await _service.TryAcquireTokenAsync(networkId);
        refreshedResult.Should().BeTrue("tokens should be restored after refresh");
    }

    [Fact]
    public async Task TryAcquireTokenAsync_UnknownNetwork_ReturnsTrue()
    {
        // Arrange
        int unknownNetworkId = 999;

        // Act
        var result = await _service.TryAcquireTokenAsync(unknownNetworkId);

        // Assert
        result.Should().BeTrue("unknown networks should be allowed by default");
    }

    [Fact]
    public async Task RateLimiting_DifferentNetworks_IndependentBuckets()
    {
        // Arrange
        int ethereumId = 1;
        int bscId = 2;

        // Act - Exhaust Ethereum bucket
        for (int i = 0; i < 5; i++)
        {
            await _service.TryAcquireTokenAsync(ethereumId);
        }

        // Assert - BSC bucket still has tokens
        var bscResult = await _service.TryAcquireTokenAsync(bscId);
        bscResult.Should().BeTrue("BSC bucket should be independent from Ethereum");

        // Assert - Ethereum bucket exhausted
        var ethResult = await _service.TryAcquireTokenAsync(ethereumId);
        ethResult.Should().BeFalse("Ethereum bucket should be exhausted");
    }

    [Fact]
    public async Task RateLimiting_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        int networkId = 1; // Ethereum (5 req/sec)
        var tasks = new List<Task<bool>>();

        // Act - Simulate 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.TryAcquireTokenAsync(networkId));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Exactly 5 should succeed
        var successCount = results.Count(r => r);
        successCount.Should().Be(5, "only 5 tokens should be acquired from bucket of 5");
    }

    [Fact]
    public async Task RefreshTokensAsync_AllNetworks_RefreshesAllBuckets()
    {
        // Arrange - Exhaust multiple network buckets
        await _service.TryAcquireTokenAsync(1); // Ethereum
        await _service.TryAcquireTokenAsync(2); // BSC
        await _service.TryAcquireTokenAsync(3); // Polygon
        await _service.TryAcquireTokenAsync(4); // Solana

        // Act - Single refresh call
        await _service.RefreshTokensAsync();

        // Assert - All buckets refilled
        (await _service.TryAcquireTokenAsync(1)).Should().BeTrue("Ethereum refreshed");
        (await _service.TryAcquireTokenAsync(2)).Should().BeTrue("BSC refreshed");
        (await _service.TryAcquireTokenAsync(3)).Should().BeTrue("Polygon refreshed");
        (await _service.TryAcquireTokenAsync(4)).Should().BeTrue("Solana refreshed");
    }

    [Fact]
    public async Task TryAcquireTokenAsync_MultipleNetworks_CorrectCapacities()
    {
        // Test all networks have correct token capacity

        // Ethereum, BSC, Polygon: 5 tokens each
        var networks = new[] { 1, 2, 3 };
        foreach (var networkId in networks)
        {
            var tokens = new List<bool>();
            for (int i = 0; i < 6; i++)
            {
                tokens.Add(await _service.TryAcquireTokenAsync(networkId));
            }

            tokens.Take(5).Should().AllBeEquivalentTo(true, $"Network {networkId} should allow 5 tokens");
            tokens[5].Should().BeFalse($"Network {networkId} should block 6th token");

            // Refresh for next iteration
            await _service.RefreshTokensAsync();
        }

        // Solana: 3 tokens
        var solanaTokens = new List<bool>();
        for (int i = 0; i < 4; i++)
        {
            solanaTokens.Add(await _service.TryAcquireTokenAsync(4));
        }

        solanaTokens.Take(3).Should().AllBeEquivalentTo(true, "Solana should allow 3 tokens");
        solanaTokens[3].Should().BeFalse("Solana should block 4th token");
    }
}
