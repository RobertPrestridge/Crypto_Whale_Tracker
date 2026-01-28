using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvestIt.Services.ExplorerClients.Models;

/// <summary>
/// Handles Etherscan API responses that can have either string or array results
/// </summary>
public class FlexibleEtherscanResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    [JsonConverter(typeof(FlexibleResultConverter))]
    public FlexibleResult Result { get; set; } = new();
}

public class FlexibleResult
{
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public List<EtherscanTransaction>? Transactions { get; set; }
}

public class FlexibleResultConverter : JsonConverter<FlexibleResult>
{
    public override FlexibleResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new FlexibleResult();

        if (reader.TokenType == JsonTokenType.String)
        {
            // Error response - result is a string
            result.IsError = true;
            result.ErrorMessage = reader.GetString();
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Success response - result is an array
            result.IsError = false;
            result.Transactions = JsonSerializer.Deserialize<List<EtherscanTransaction>>(ref reader, options);
        }
        else
        {
            // Unexpected format
            result.IsError = true;
            result.ErrorMessage = "Unexpected response format";
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, FlexibleResult value, JsonSerializerOptions options)
    {
        if (value.IsError)
        {
            writer.WriteStringValue(value.ErrorMessage);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Transactions, options);
        }
    }
}
