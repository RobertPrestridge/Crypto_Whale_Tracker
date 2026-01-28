namespace InvestIt.Services.PriceTracking;

public interface ICoinGeckoClient
{
    Task<decimal?> GetTokenPriceUsdAsync(string tokenSymbol);
    Task<Dictionary<string, decimal>> GetMultipleTokenPricesAsync(IEnumerable<string> tokenSymbols);
}
