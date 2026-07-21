namespace Onboarding.Api.Models;

public sealed class HcmErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool Retryable { get; set; }
}