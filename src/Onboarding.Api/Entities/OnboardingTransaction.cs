using System.ComponentModel.DataAnnotations;

namespace Onboarding.Api.Entities;

public sealed class OnboardingTransaction
{
    [Key]
    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    public DateOnly JoiningDate { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [MaxLength(50)]
    public string? HcmEmployeeId { get; set; }

    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    public bool IsRetryable { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }
}