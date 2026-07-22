using System.ComponentModel.DataAnnotations;

namespace Onboarding.Api.Models;

public sealed class OnboardingQueryParameters
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(100)]
    public string? Search { get; set; }
}