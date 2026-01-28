using InvestIt.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.RateLimiting;

/// <summary>
/// Token bucket algorithm implementation for API rate limiting
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly ILogger<RateLimitService> _logger;
    private readonly Dictionary<int, TokenBucket> _buckets = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RateLimitService(ILogger<RateLimitService> logger)
    {
        _logger = logger;

        // Initialize buckets for known networks with default rates
        // Network IDs from seed data: 1=Ethereum, 2=BSC, 3=Polygon, 4=Solana
        _buckets[1] = new TokenBucket(5); // Ethereum
        _buckets[2] = new TokenBucket(5); // BSC
        _buckets[3] = new TokenBucket(5); // Polygon
        _buckets[4] = new TokenBucket(3); // Solana
    }

    public async Task<bool> TryAcquireTokenAsync(int networkId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_buckets.ContainsKey(networkId))
            {
                _logger.LogWarning("Network {NetworkId} not configured for rate limiting, allowing request", networkId);
                return true;
            }

            var bucket = _buckets[networkId];
            return bucket.TryAcquire();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshTokensAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var bucket in _buckets.Values)
            {
                bucket.Refill();
            }

            _logger.LogDebug("Refreshed rate limit tokens for {BucketCount} networks", _buckets.Count);
        }
        finally
        {
            _lock.Release();
        }

        await Task.CompletedTask;
    }

    private class TokenBucket
    {
        private readonly int _capacity;
        private int _tokens;
        private DateTime _lastRefill;

        public TokenBucket(int tokensPerSecond)
        {
            _capacity = tokensPerSecond;
            _tokens = tokensPerSecond;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryAcquire()
        {
            // Auto-refill if a second has passed
            var now = DateTime.UtcNow;
            if ((now - _lastRefill).TotalSeconds >= 1)
            {
                Refill();
            }

            if (_tokens > 0)
            {
                _tokens--;
                return true;
            }

            return false;
        }

        public void Refill()
        {
            _tokens = _capacity;
            _lastRefill = DateTime.UtcNow;
        }
    }
}
