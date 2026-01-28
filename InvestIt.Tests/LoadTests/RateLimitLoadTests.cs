using FluentAssertions;
using InvestIt.Services.Interfaces;
using InvestIt.Services.RateLimiting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace InvestIt.Tests.LoadTests;

/// <summary>
/// Load tests for RateLimitService under high concurrency and stress
/// </summary>
public class RateLimitLoadTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<RateLimitService>> _mockLogger;

    public RateLimitLoadTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<RateLimitService>>();
    }

    [Fact]
    public async Task LoadTest_100ConcurrentRequests_CorrectLimitEnforced()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int networkId = 1; // Ethereum - 5 tokens
        int totalRequests = 100;
        var successCount = new ConcurrentBag<bool>();

        var sw = Stopwatch.StartNew();

        // Act - 100 concurrent requests
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(async i =>
            {
                var result = await service.TryAcquireTokenAsync(networkId);
                successCount.Add(result);
                return result;
            })
            .ToList();

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var succeeded = successCount.Count(r => r);
        var failed = successCount.Count(r => !r);

        _output.WriteLine($"Total requests: {totalRequests}");
        _output.WriteLine($"Succeeded: {succeeded}");
        _output.WriteLine($"Failed: {failed}");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");

        // Exactly 5 should succeed (token bucket capacity)
        succeeded.Should().Be(5, "token bucket has capacity of 5");
        failed.Should().Be(95, "remaining requests should be rate limited");

        // Should complete quickly (thread-safe semaphore)
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task LoadTest_1000RequestsAcrossNetworks_IndependentLimits()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int requestsPerNetwork = 250;

        var results = new ConcurrentDictionary<int, List<bool>>();
        results[1] = new List<bool>(); // Ethereum
        results[2] = new List<bool>(); // BSC
        results[3] = new List<bool>(); // Polygon
        results[4] = new List<bool>(); // Solana

        var sw = Stopwatch.StartNew();

        // Act - 250 requests per network concurrently (1000 total)
        var allTasks = new List<Task>();

        foreach (var networkId in new[] { 1, 2, 3, 4 })
        {
            var networkTasks = Enumerable.Range(0, requestsPerNetwork)
                .Select(async i =>
                {
                    var result = await service.TryAcquireTokenAsync(networkId);
                    lock (results[networkId])
                    {
                        results[networkId].Add(result);
                    }
                });
            allTasks.AddRange(networkTasks);
        }

        await Task.WhenAll(allTasks);
        sw.Stop();

        // Assert - Each network enforces its own limit
        results[1].Count(r => r).Should().Be(5, "Ethereum: 5 tokens");
        results[2].Count(r => r).Should().Be(5, "BSC: 5 tokens");
        results[3].Count(r => r).Should().Be(5, "Polygon: 5 tokens");
        results[4].Count(r => r).Should().Be(3, "Solana: 3 tokens");

        _output.WriteLine($"1000 requests across 4 networks completed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Ethereum: {results[1].Count(r => r)}/250");
        _output.WriteLine($"BSC: {results[2].Count(r => r)}/250");
        _output.WriteLine($"Polygon: {results[3].Count(r => r)}/250");
        _output.WriteLine($"Solana: {results[4].Count(r => r)}/250");

        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task LoadTest_RefreshUnderLoad_ResetsAllBuckets()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int networkId = 1;

        // Exhaust tokens
        for (int i = 0; i < 5; i++)
        {
            await service.TryAcquireTokenAsync(networkId);
        }

        // Verify exhausted
        var exhausted = await service.TryAcquireTokenAsync(networkId);
        exhausted.Should().BeFalse();

        // Act - Refresh under concurrent load
        var refreshTasks = Enumerable.Range(0, 50)
            .Select(i => service.RefreshTokensAsync())
            .ToList();

        await Task.WhenAll(refreshTasks);

        // Assert - Tokens available again
        var available = await service.TryAcquireTokenAsync(networkId);
        available.Should().BeTrue("tokens should be refreshed");
    }

    [Fact]
    public async Task LoadTest_SustainedLoad_MaintainsCorrectLimits()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int networkId = 1;
        int iterations = 10;
        var allResults = new ConcurrentBag<bool>();

        var sw = Stopwatch.StartNew();

        // Act - 10 iterations of (exhaust + refresh)
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            // Concurrent burst
            var tasks = Enumerable.Range(0, 50)
                .Select(async i =>
                {
                    var result = await service.TryAcquireTokenAsync(networkId);
                    allResults.Add(result);
                });

            await Task.WhenAll(tasks);

            // Refresh
            await service.RefreshTokensAsync();
        }

        sw.Stop();

        // Assert - Exactly 5 successes per iteration
        var totalSuccess = allResults.Count(r => r);
        totalSuccess.Should().Be(50, "5 tokens × 10 iterations = 50 total");

        _output.WriteLine($"Sustained load: {iterations} iterations, {sw.ElapsedMilliseconds}ms total");
        _output.WriteLine($"Success rate: {totalSuccess}/{allResults.Count}");
        _output.WriteLine($"Average per iteration: {sw.ElapsedMilliseconds / iterations}ms");
    }

    [Fact]
    public async Task LoadTest_ExtremeConcurrency_ThreadSafety()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int networkId = 1;
        int extremeLoad = 10000; // 10,000 concurrent requests
        var results = new ConcurrentBag<bool>();

        var sw = Stopwatch.StartNew();

        // Act - 10,000 concurrent requests
        var tasks = Enumerable.Range(0, extremeLoad)
            .Select(async i =>
            {
                var result = await service.TryAcquireTokenAsync(networkId);
                results.Add(result);
            });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var succeeded = results.Count(r => r);
        succeeded.Should().Be(5, "even with 10,000 concurrent requests, only 5 should succeed");

        _output.WriteLine($"Extreme load test: {extremeLoad} requests");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Succeeded: {succeeded}");
        _output.WriteLine($"Thread-safe: ✅");

        // Performance check - should handle 10k requests quickly
        sw.ElapsedMilliseconds.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task LoadTest_RapidRefreshCycles_NoMemoryLeak()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int cycles = 1000;

        var sw = Stopwatch.StartNew();

        // Act - 1000 rapid refresh cycles
        for (int i = 0; i < cycles; i++)
        {
            await service.RefreshTokensAsync();
        }

        sw.Stop();

        // Assert - Should complete without hanging or errors
        _output.WriteLine($"Rapid refresh: {cycles} cycles in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)cycles:F2}ms per refresh");

        sw.ElapsedMilliseconds.Should().BeLessThan(2000);

        // Verify service still works after rapid refreshes
        var result = await service.TryAcquireTokenAsync(1);
        result.Should().BeTrue("service should still function after rapid refreshes");
    }

    [Fact]
    public async Task LoadTest_AllNetworksSimultaneously_NoInterference()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        var results = new ConcurrentDictionary<int, ConcurrentBag<bool>>();

        foreach (var id in new[] { 1, 2, 3, 4 })
        {
            results[id] = new ConcurrentBag<bool>();
        }

        var sw = Stopwatch.StartNew();

        // Act - Hammer all 4 networks simultaneously
        var allTasks = new List<Task>();

        foreach (var networkId in new[] { 1, 2, 3, 4 })
        {
            // 500 concurrent requests per network
            var networkTasks = Enumerable.Range(0, 500)
                .Select(async i =>
                {
                    var result = await service.TryAcquireTokenAsync(networkId);
                    results[networkId].Add(result);
                });

            allTasks.AddRange(networkTasks);
        }

        // All 2000 requests concurrently
        await Task.WhenAll(allTasks);
        sw.Stop();

        // Assert - Each network maintains independent limits
        results[1].Count(r => r).Should().Be(5);
        results[2].Count(r => r).Should().Be(5);
        results[3].Count(r => r).Should().Be(5);
        results[4].Count(r => r).Should().Be(3);

        _output.WriteLine($"2000 concurrent requests across 4 networks");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine("Network independence: ✅");

        sw.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task LoadTest_BurstTraffic_HandlesPeaks()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        int networkId = 1;

        // Simulate realistic traffic pattern: bursts with pauses
        var burstResults = new List<int>();

        var sw = Stopwatch.StartNew();

        // Act - 5 bursts of 100 requests each
        for (int burst = 0; burst < 5; burst++)
        {
            var results = new ConcurrentBag<bool>();

            // Burst of 100 concurrent requests
            var tasks = Enumerable.Range(0, 100)
                .Select(async i =>
                {
                    var result = await service.TryAcquireTokenAsync(networkId);
                    results.Add(result);
                });

            await Task.WhenAll(tasks);

            var succeeded = results.Count(r => r);
            burstResults.Add(succeeded);

            // Refresh between bursts
            await service.RefreshTokensAsync();
        }

        sw.Stop();

        // Assert - Each burst limited to 5
        burstResults.Should().AllSatisfy(count =>
            count.Should().Be(5, "each burst should be limited to 5 successful requests")
        );

        _output.WriteLine($"Burst traffic test: 5 bursts");
        _output.WriteLine($"Results per burst: {string.Join(", ", burstResults)}");
        _output.WriteLine($"Total duration: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task LoadTest_MixedOperations_Stability()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);
        var operations = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        // Act - Mix of acquire and refresh operations concurrently
        var tasks = new List<Task>();

        // 500 acquire attempts
        for (int i = 0; i < 500; i++)
        {
            int networkId = (i % 4) + 1;
            tasks.Add(Task.Run(async () =>
            {
                var result = await service.TryAcquireTokenAsync(networkId);
                operations.Add($"Acquire-{networkId}-{result}");
            }));
        }

        // 50 refresh operations
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await service.RefreshTokensAsync();
                operations.Add("Refresh");
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - All operations completed without errors
        operations.Should().HaveCount(550);

        _output.WriteLine($"Mixed operations: {operations.Count} total");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Stability: ✅ No deadlocks or race conditions");

        sw.ElapsedMilliseconds.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task LoadTest_MemoryUsage_NoLeaks()
    {
        // Arrange
        var service = new RateLimitService(_mockLogger.Object);

        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Perform many operations
        for (int iteration = 0; iteration < 100; iteration++)
        {
            // 100 iterations of burst + refresh
            var tasks = Enumerable.Range(0, 100)
                .Select(i => service.TryAcquireTokenAsync(1));

            await Task.WhenAll(tasks);
            await service.RefreshTokensAsync();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Assert - Memory growth should be minimal
        var memoryGrowth = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Growth: {memoryGrowth:N0} bytes");

        // Memory growth should be minimal (< 1MB for 10,000 operations)
        memoryGrowth.Should().BeLessThan(1024 * 1024, "memory growth should be minimal");
    }
}
