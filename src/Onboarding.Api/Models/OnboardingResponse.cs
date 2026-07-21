namespace Onboarding.Api.Models;

public sealed class OnboardingResponse
{
    public Guid TransactionId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? HcmEmployeeId { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsRetryable { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }
}