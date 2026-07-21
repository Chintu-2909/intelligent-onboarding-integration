namespace Onboarding.Api.Models;

public sealed class HcmCreateEmployeeRequest
{
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public DateOnly JoiningDate { get; set; }
}