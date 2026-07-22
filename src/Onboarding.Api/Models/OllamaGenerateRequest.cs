using System.Text.Json.Serialization;

namespace Onboarding.Api.Models;

public sealed class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    public OllamaGenerationOptions Options { get; set; } =
        new();
}

public sealed class OllamaGenerationOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.1;

    [JsonPropertyName("num_predict")]
    public int MaximumGeneratedTokens { get; set; } = 250;
}