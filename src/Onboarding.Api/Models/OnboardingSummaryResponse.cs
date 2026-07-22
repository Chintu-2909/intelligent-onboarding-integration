namespace Onboarding.Api.Models;

public sealed class OnboardingSummaryResponse
{
    public int TotalTransactions { get; set; }

    public int PendingTransactions { get; set; }

    public int ProcessingTransactions { get; set; }

    public int CompletedTransactions { get; set; }

    public int FailedTransactions { get; set; }

    public int RetryableFailures { get; set; }

    public int RetryLimitExceededTransactions { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}