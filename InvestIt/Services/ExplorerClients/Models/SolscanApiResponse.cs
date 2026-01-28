using System.Text.Json.Serialization;

namespace InvestIt.Services.ExplorerClients.Models;

public class SolscanApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

public class SolscanTransaction
{
    [JsonPropertyName("txHash")]
    public string TxHash { get; set; } = string.Empty;

    [JsonPropertyName("blockTime")]
    public long BlockTime { get; set; }

    [JsonPropertyName("slot")]
    public long Slot { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("signer")]
    public List<string> Signer { get; set; } = new();

    [JsonPropertyName("parsedInstruction")]
    public List<SolscanInstruction> ParsedInstruction { get; set; } = new();
}

public class SolscanInstruction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("programId")]
    public string ProgramId { get; set; } = string.Empty;

    [JsonPropertyName("program")]
    public string Program { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public SolscanTransferParams? Params { get; set; }
}

public class SolscanTransferParams
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("amount")]
    public long? Amount { get; set; }

    [JsonPropertyName("amount_raw")]
    public string? AmountRaw { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("decimals")]
    public int? Decimals { get; set; }
}

public class SolscanAccountTxResponse
{
    [JsonPropertyName("data")]
    public List<SolscanTransaction> Data { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
