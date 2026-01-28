namespace InvestIt.Services.Interfaces;

public interface IRateLimitService
{
    Task<bool> TryAcquireTokenAsync(int networkId);
    Task RefreshTokensAsync();
}
