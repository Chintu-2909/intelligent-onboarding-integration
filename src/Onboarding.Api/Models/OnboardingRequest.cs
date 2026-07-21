using System.ComponentModel.DataAnnotations;

namespace Onboarding.Api.Models;

public sealed class OnboardingRequest
{
    [Required]
    [StringLength(20)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Department { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [Required]
    public DateOnly JoiningDate { get; set; }
}