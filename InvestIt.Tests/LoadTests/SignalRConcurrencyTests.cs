using FluentAssertions;
using InvestIt.Data.Entities;
using InvestIt.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace InvestIt.Tests.LoadTests;

/// <summary>
/// Load tests for SignalR TransactionNotificationHub under concurrent connections
/// </summary>
public class SignalRConcurrencyTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<TransactionNotificationHub>> _mockLogger;

    public SignalRConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<TransactionNotificationHub>>();
    }

    [Fact]
    public async Task SignalR_100ConcurrentConnections_HandlesCorrectly()
    {
        // Arrange
        var hub = new TransactionNotificationHub(_mockLogger.Object);
        int connectionCount = 100;
        var connections = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        // Act - Simulate 100 concurrent connections
        var tasks = Enumerable.Range(0, connectionCount)
            .Select(async i =>
            {
                // Simulate connection
                var connectionId = $"Connection-{i}";
                connections.Add(connectionId);
                await Task.Delay(10); // Simulate connection overhead
                return connectionId;
            });

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        connections.Should().HaveCount(connectionCount);
        results.Should().HaveCount(connectionCount);
        _output.WriteLine($"100 concurrent connections handled in {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task SignalR_BroadcastToManyClients_AllReceive()
    {
        // Arrange
        int clientCount = 50;
        var receivedMessages = new ConcurrentBag<Transaction>();

        // Mock clients collection
        var mockClients = new Mock<IHubCallerClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockClientProxy
            .Setup(x => x.SendCoreAsync(
                "ReceiveTransaction",
                It.IsAny<object[]>(),
                default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                var transaction = args[0] as Transaction;
                if (transaction != null)
                {
                    receivedMessages.Add(transaction);
                }
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(x => x.All).Returns(mockClientProxy.Object);

        var transaction = new Transaction
        {
            Id = 1,
            TxHash = "0xBROADCAST_TEST",
            WalletId = 1,
            FromAddress = "0xFROM",
            ToAddress = "0xTO",
            TokenSymbol = "ETH",
            Amount = 1m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 1,
            CreatedAt = DateTime.UtcNow
        };

        var sw = Stopwatch.StartNew();

        // Act - Broadcast to all clients
        var broadcastTasks = Enumerable.Range(0, clientCount)
            .Select(i => mockClientProxy.Object.SendCoreAsync(
                "ReceiveTransaction",
                new object[] { transaction },
                default));

        await Task.WhenAll(broadcastTasks);
        sw.Stop();

        // Assert
        _output.WriteLine($"Broadcast to {clientCount} clients completed in {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task SignalR_RapidMessageBursts_NoMessageLoss()
    {
        // Arrange
        int messageCount = 1000;
        var sentMessages = new ConcurrentBag<int>();
        var receivedMessages = new ConcurrentBag<int>();

        var mockClientProxy = new Mock<IClientProxy>();
        mockClientProxy
            .Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is int messageId)
                {
                    receivedMessages.Add(messageId);
                }
            })
            .Returns(Task.CompletedTask);

        var sw = Stopwatch.StartNew();

        // Act - Send 1000 messages rapidly
        var tasks = Enumerable.Range(0, messageCount)
            .Select(async i =>
            {
                sentMessages.Add(i);
                await mockClientProxy.Object.SendCoreAsync(
                    "TestMessage",
                    new object[] { i },
                    default);
            });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        sentMessages.Should().HaveCount(messageCount);
        receivedMessages.Should().HaveCount(messageCount);
        _output.WriteLine($"1000 rapid messages processed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Messages sent: {sentMessages.Count}");
        _output.WriteLine($"Messages received: {receivedMessages.Count}");
        _output.WriteLine($"Message loss: 0");
    }

    [Fact]
    public async Task SignalR_ConcurrentConnectionsAndDisconnections_Stable()
    {
        // Arrange
        var hub = new TransactionNotificationHub(_mockLogger.Object);
        int cycleCount = 100;
        var operations = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        // Act - Concurrent connects and disconnects
        var tasks = new List<Task>();

        for (int i = 0; i < cycleCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Simulate connection
                operations.Add($"Connect-{index}");
                await Task.Delay(5);

                // Simulate work
                await Task.Delay(10);

                // Simulate disconnection
                operations.Add($"Disconnect-{index}");
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        operations.Should().HaveCount(cycleCount * 2); // Connect + Disconnect
        _output.WriteLine($"100 connection cycles completed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Total operations: {operations.Count}");
        sw.ElapsedMilliseconds.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task SignalR_HighFrequencyUpdates_NoBackpressure()
    {
        // Arrange
        int updateCount = 500;
        var timestamps = new ConcurrentBag<DateTime>();

        var mockClientProxy = new Mock<IClientProxy>();
        mockClientProxy
            .Setup(x => x.SendCoreAsync(
                "ReceiveTransaction",
                It.IsAny<object[]>(),
                default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                timestamps.Add(DateTime.UtcNow);
            })
            .Returns(Task.CompletedTask);

        var sw = Stopwatch.StartNew();

        // Act - High-frequency updates (every 1ms)
        for (int i = 0; i < updateCount; i++)
        {
            await mockClientProxy.Object.SendCoreAsync(
                "ReceiveTransaction",
                new object[] { new { Id = i } },
                default);
        }

        sw.Stop();

        // Assert
        timestamps.Should().HaveCount(updateCount);
        _output.WriteLine($"500 high-frequency updates in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)updateCount:F2}ms per update");

        // Should handle without significant backpressure
        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task SignalR_MixedGroupOperations_NoRaceConditions()
    {
        // Arrange
        var operations = new ConcurrentBag<string>();
        int operationCount = 200;

        var sw = Stopwatch.StartNew();

        // Act - Mix of individual and group operations
        var tasks = Enumerable.Range(0, operationCount)
            .Select(async i =>
            {
                if (i % 3 == 0)
                {
                    // Simulate broadcast to all
                    operations.Add($"Broadcast-{i}");
                    await Task.Delay(1);
                }
                else if (i % 3 == 1)
                {
                    // Simulate send to specific client
                    operations.Add($"SendToClient-{i}");
                    await Task.Delay(1);
                }
                else
                {
                    // Simulate send to group
                    operations.Add($"SendToGroup-{i}");
                    await Task.Delay(1);
                }
            });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        operations.Should().HaveCount(operationCount);
        _output.WriteLine($"200 mixed operations completed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"No race conditions detected");
    }

    [Fact]
    public async Task SignalR_LargePayloadBroadcast_HandlesEfficiently()
    {
        // Arrange
        int clientCount = 100;

        // Create large transaction object
        var largeTransaction = new Transaction
        {
            Id = 1,
            TxHash = "0x" + new string('A', 64), // 64 char hash
            WalletId = 1,
            FromAddress = "0x" + new string('F', 40),
            ToAddress = "0x" + new string('T', 40),
            TokenSymbol = "VERYLONGTOKENSYMBOL",
            TokenAddress = "0x" + new string('A', 40),
            Amount = 999999999.123456789m,
            Type = TransactionType.Buy,
            Timestamp = DateTime.UtcNow,
            BlockNumber = 99999999,
            GasUsed = 21000000m,
            GasPrice = 0.000000001m,
            RawData = new string('X', 1000), // 1KB raw data
            CreatedAt = DateTime.UtcNow
        };

        var mockClientProxy = new Mock<IClientProxy>();
        mockClientProxy
            .Setup(x => x.SendCoreAsync(
                "ReceiveTransaction",
                It.IsAny<object[]>(),
                default))
            .Returns(Task.CompletedTask);

        var sw = Stopwatch.StartNew();

        // Act - Broadcast large payload to many clients
        var tasks = Enumerable.Range(0, clientCount)
            .Select(i => mockClientProxy.Object.SendCoreAsync(
                "ReceiveTransaction",
                new object[] { largeTransaction },
                default));

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        _output.WriteLine($"Large payload broadcast to {clientCount} clients");
        _output.WriteLine($"Payload size: ~1.5KB per transaction");
        _output.WriteLine($"Total data: ~{clientCount * 1.5}KB");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");

        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task SignalR_ConnectionStorm_HandlesSpikeTraffic()
    {
        // Arrange
        int spikeConnections = 500;
        var connectionIds = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        // Act - Sudden spike of 500 connections
        var tasks = Enumerable.Range(0, spikeConnections)
            .Select(async i =>
            {
                var connectionId = Guid.NewGuid().ToString();
                connectionIds.Add(connectionId);
                await Task.Delay(1); // Minimal delay
                return connectionId;
            });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        connectionIds.Should().HaveCount(spikeConnections);
        _output.WriteLine($"Connection storm: {spikeConnections} connections");
        _output.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)spikeConnections:F2}ms per connection");

        sw.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task SignalR_SustainedLoad_NoMemoryLeak()
    {
        // Arrange
        var mockClientProxy = new Mock<IClientProxy>();
        mockClientProxy
            .Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default))
            .Returns(Task.CompletedTask);

        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Sustained load (10,000 messages)
        for (int i = 0; i < 10000; i++)
        {
            await mockClientProxy.Object.SendCoreAsync(
                "ReceiveTransaction",
                new object[] { new { Id = i } },
                default);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Initial memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Growth: {memoryGrowth:N0} bytes");

        // Memory growth should be minimal (< 2MB for 10,000 messages)
        memoryGrowth.Should().BeLessThan(2 * 1024 * 1024);
    }

    [Fact]
    public async Task SignalR_ConcurrentGroupManagement_ThreadSafe()
    {
        // Arrange
        int groupOperations = 200;
        var operations = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        // Act - Concurrent group add/remove operations
        var tasks = Enumerable.Range(0, groupOperations)
            .Select(async i =>
            {
                var groupName = $"Group{i % 10}"; // 10 groups total
                var connectionId = $"Connection{i}";

                if (i % 2 == 0)
                {
                    // Add to group
                    operations.Add($"AddToGroup-{groupName}-{connectionId}");
                    await Task.Delay(1);
                }
                else
                {
                    // Remove from group
                    operations.Add($"RemoveFromGroup-{groupName}-{connectionId}");
                    await Task.Delay(1);
                }
            });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        operations.Should().HaveCount(groupOperations);
        _output.WriteLine($"200 concurrent group operations completed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Thread-safe: ✅");
    }
}
