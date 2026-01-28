using System.Text.Json.Serialization;

namespace InvestIt.Services.ExplorerClients.Models;

public class EtherscanApiResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

public class EtherscanTransaction
{
    [JsonPropertyName("blockNumber")]
    public string BlockNumber { get; set; } = string.Empty;

    [JsonPropertyName("timeStamp")]
    public string TimeStamp { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("contractAddress")]
    public string? ContractAddress { get; set; }

    [JsonPropertyName("tokenName")]
    public string? TokenName { get; set; }

    [JsonPropertyName("tokenSymbol")]
    public string? TokenSymbol { get; set; }

    [JsonPropertyName("tokenDecimal")]
    public string? TokenDecimal { get; set; }

    [JsonPropertyName("gas")]
    public string? Gas { get; set; }

    [JsonPropertyName("gasPrice")]
    public string? GasPrice { get; set; }

    [JsonPropertyName("gasUsed")]
    public string? GasUsed { get; set; }
}
