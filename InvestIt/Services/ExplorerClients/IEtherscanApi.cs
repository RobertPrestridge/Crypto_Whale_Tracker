using InvestIt.Services.ExplorerClients.Models;
using Refit;

namespace InvestIt.Services.ExplorerClients;

/// <summary>
/// Etherscan-compatible API interface supporting both V1 and V2 endpoints.
/// V2 uses unified endpoint with chainid parameter.
/// </summary>
public interface IEtherscanApi
{
    [Get("")]
    Task<FlexibleEtherscanResponse> GetNormalTransactionsAsync(
        [AliasAs("module")] string module,
        [AliasAs("action")] string action,
        [AliasAs("address")] string address,
        [AliasAs("startblock")] int startBlock,
        [AliasAs("endblock")] int endBlock,
        [AliasAs("page")] int page,
        [AliasAs("offset")] int offset,
        [AliasAs("sort")] string sort,
        [AliasAs("apikey")] string? apiKey,
        [AliasAs("chainid")] int? chainId = null);

    [Get("")]
    Task<FlexibleEtherscanResponse> GetTokenTransactionsAsync(
        [AliasAs("module")] string module,
        [AliasAs("action")] string action,
        [AliasAs("address")] string address,
        [AliasAs("startblock")] int startBlock,
        [AliasAs("endblock")] int endBlock,
        [AliasAs("page")] int page,
        [AliasAs("offset")] int offset,
        [AliasAs("sort")] string sort,
        [AliasAs("apikey")] string? apiKey,
        [AliasAs("chainid")] int? chainId = null);
}
