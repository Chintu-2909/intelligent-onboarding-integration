namespace Onboarding.Api.Models;

public sealed class AiFailureExplanationResponse
{
    public Guid TransactionId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public bool IsRetryable { get; set; }

    public int RetryCount { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public string Disclaimer { get; set; } =
        "AI-generated operational guidance. Verify the recommendation before taking action.";
}