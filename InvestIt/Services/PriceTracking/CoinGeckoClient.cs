using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InvestIt.Services.PriceTracking;

public class CoinGeckoClient : ICoinGeckoClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CoinGeckoClient> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    // Map common token symbols to CoinGecko IDs
    private readonly Dictionary<string, string> _tokenIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ETH"] = "ethereum",
        ["BTC"] = "bitcoin",
        ["USDT"] = "tether",
        ["USDC"] = "usd-coin",
        ["BNB"] = "binancecoin",
        ["BUSD"] = "binance-usd",
        ["DAI"] = "dai",
        ["WETH"] = "weth",
        ["WBTC"] = "wrapped-bitcoin",
        ["MATIC"] = "matic-network",
        ["WMATIC"] = "wmatic",
        ["SHIB"] = "shiba-inu",
        ["UNI"] = "uniswap",
        ["LINK"] = "chainlink",
        ["CAKE"] = "pancakeswap-token",
        ["APE"] = "apecoin",
        ["SAND"] = "the-sandbox",
        ["MANA"] = "decentraland",
        ["CRV"] = "curve-dao-token",
        ["LDO"] = "lido-dao",
        ["AAVE"] = "aave",
        ["MKR"] = "maker",
        ["SNX"] = "havven",
        ["COMP"] = "compound-governance-token",
        ["1INCH"] = "1inch",
        ["GRT"] = "the-graph",
        ["ENS"] = "ethereum-name-service",
        ["FTM"] = "fantom",
        ["AVAX"] = "avalanche-2",
        ["ATOM"] = "cosmos",
        ["DOT"] = "polkadot",
        ["SOL"] = "solana",
        ["ADA"] = "cardano"
    };

    public CoinGeckoClient(HttpClient httpClient, IMemoryCache cache, ILogger<CoinGeckoClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<decimal?> GetTokenPriceUsdAsync(string tokenSymbol)
    {
        if (string.IsNullOrEmpty(tokenSymbol))
            return null;

        var cacheKey = $"price_{tokenSymbol.ToUpperInvariant()}";

        // Check cache first
        if (_cache.TryGetValue<decimal>(cacheKey, out var cachedPrice))
        {
            _logger.LogDebug("Price for {Token} found in cache: ${Price}", tokenSymbol, cachedPrice);
            return cachedPrice;
        }

        try
        {
            // Get CoinGecko ID for token
            if (!_tokenIdMap.TryGetValue(tokenSymbol, out var coinId))
            {
                _logger.LogWarning("No CoinGecko ID mapping for token {Token}", tokenSymbol);
                return null;
            }

            // Fetch price from CoinGecko
            var response = await _httpClient.GetAsync($"simple/price?ids={coinId}&vs_currencies=usd");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CoinGecko API returned {StatusCode} for {Token}", response.StatusCode, tokenSymbol);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (data.TryGetProperty(coinId, out var coinData) &&
                coinData.TryGetProperty("usd", out var usdPrice))
            {
                var price = usdPrice.GetDecimal();

                // Cache for 5 minutes
                _cache.Set(cacheKey, price, _cacheDuration);

                _logger.LogInformation("Fetched price for {Token}: ${Price}", tokenSymbol, price);
                return price;
            }

            _logger.LogWarning("Could not parse price for {Token}", tokenSymbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price for {Token}", tokenSymbol);
            return null;
        }
    }

    public async Task<Dictionary<string, decimal>> GetMultipleTokenPricesAsync(IEnumerable<string> tokenSymbols)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Get unique token symbols
        var uniqueSymbols = tokenSymbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Filter to only tokens we have CoinGecko IDs for
        var symbolsToFetch = uniqueSymbols
            .Where(s => _tokenIdMap.ContainsKey(s))
            .ToList();

        if (!symbolsToFetch.Any())
            return prices;

        // Check cache first
        var uncachedSymbols = new List<string>();

        foreach (var symbol in symbolsToFetch)
        {
            var cacheKey = $"price_{symbol.ToUpperInvariant()}";
            if (_cache.TryGetValue<decimal>(cacheKey, out var cachedPrice))
            {
                prices[symbol] = cachedPrice;
            }
            else
            {
                uncachedSymbols.Add(symbol);
            }
        }

        // Fetch uncached prices
        if (uncachedSymbols.Any())
        {
            try
            {
                var coinIds = string.Join(",", uncachedSymbols.Select(s => _tokenIdMap[s]));
                var response = await _httpClient.GetAsync($"simple/price?ids={coinIds}&vs_currencies=usd");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    foreach (var symbol in uncachedSymbols)
                    {
                        var coinId = _tokenIdMap[symbol];
                        if (data.TryGetProperty(coinId, out var coinData) &&
                            coinData.TryGetProperty("usd", out var usdPrice))
                        {
                            var price = usdPrice.GetDecimal();
                            prices[symbol] = price;

                            // Cache
                            var cacheKey = $"price_{symbol.ToUpperInvariant()}";
                            _cache.Set(cacheKey, price, _cacheDuration);
                        }
                    }

                    _logger.LogInformation("Fetched {Count} token prices from CoinGecko", uncachedSymbols.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching multiple token prices");
            }
        }

        return prices;
    }
}
