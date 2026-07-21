namespace MockHcm.Api.Models;

public sealed class CreateEmployeeResponse
{
    public string HcmEmployeeId { get; set; } = string.Empty;

    public string EmployeeNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}