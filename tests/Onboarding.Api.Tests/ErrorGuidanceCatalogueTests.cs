using NUnit.Framework;
using Onboarding.Api.Entities;
using Onboarding.Api.Services;

namespace Onboarding.Api.Tests;

[TestFixture]
public sealed class ErrorGuidanceCatalogueTests
{
    private ErrorGuidanceCatalogue _catalogue = null!;

    [SetUp]
    public void SetUp()
    {
        _catalogue =
            new ErrorGuidanceCatalogue();
    }

    [Test]
    public void GetGuidance_WithValidationError_ReturnsApprovedGuidance()
    {
        var transaction =
            CreateFailedTransaction(
                errorCode: "HCM-VALIDATION-400",
                isRetryable: false);

        var guidance =
            _catalogue.GetGuidance(transaction);

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.KnownMeaning,
                Is.EqualTo(
                    "The downstream system rejected the transaction during validation."));

            Assert.That(
                guidance.ProbableExplanation,
                Is.EqualTo(
                    "The exact validation cause is not available in the recorded error."));

            Assert.That(
                guidance.RetryGuidance,
                Does.StartWith("Do not retry"));
        });
    }

    [Test]
    public void GetGuidance_WithTimeoutError_ReturnsControlledRetryGuidance()
    {
        var transaction =
            CreateFailedTransaction(
                errorCode: "HCM-TIMEOUT-408",
                isRetryable: true);

        var guidance =
            _catalogue.GetGuidance(transaction);

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.KnownMeaning,
                Does.Contain(
                    "did not respond within the configured timeout"));

            Assert.That(
                guidance.RetryGuidance,
                Does.Contain("controlled retry"));

            Assert.That(
                guidance.RetryGuidance,
                Does.Contain("configured retry limit"));
        });
    }

    [Test]
    public void GetGuidance_WithConnectionError_ReturnsAvailabilityGuidance()
    {
        var transaction =
            CreateFailedTransaction(
                errorCode: "HCM-CONNECTION-ERROR",
                isRetryable: true);

        var guidance =
            _catalogue.GetGuidance(transaction);

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.KnownMeaning,
                Does.Contain(
                    "could not establish a connection"));

            Assert.That(
                guidance.RecommendedAction,
                Does.Contain(
                    "running and reachable"));

            Assert.That(
                guidance.RetryGuidance,
                Does.Contain("controlled retry"));
        });
    }

    [Test]
    public void GetGuidance_WithUnknownNonRetryableError_ReturnsSafeFallback()
    {
        var transaction =
            CreateFailedTransaction(
                errorCode: "UNKNOWN-ERROR",
                isRetryable: false);

        var guidance =
            _catalogue.GetGuidance(transaction);

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.KnownMeaning,
                Is.EqualTo(
                    "The transaction contains a recorded processing failure."));

            Assert.That(
                guidance.ProbableExplanation,
                Does.Contain(
                    "not available in the approved error catalogue"));

            Assert.That(
                guidance.RetryGuidance,
                Does.StartWith("Do not retry"));
        });
    }

    [Test]
    public void GetGuidance_WithUnknownRetryableError_ReturnsControlledRetryFallback()
    {
        var transaction =
            CreateFailedTransaction(
                errorCode: "UNKNOWN-TEMPORARY-ERROR",
                isRetryable: true);

        var guidance =
            _catalogue.GetGuidance(transaction);

        Assert.That(
            guidance.RetryGuidance,
            Does.Contain("controlled retry"));
    }

    private static OnboardingTransaction
        CreateFailedTransaction(
            string errorCode,
            bool isRetryable)
    {
        return new OnboardingTransaction
        {
            TransactionId =
                Guid.NewGuid(),

            EmployeeNumber =
                "EMP-TEST-AI",

            FirstName =
                "Demo",

            LastName =
                "User",

            Email =
                "demo.user@example.com",

            Department =
                "Engineering",

            Country =
                "India",

            JoiningDate =
                new DateOnly(2026, 8, 17),

            Status =
                "Failed",

            ErrorCode =
                errorCode,

            ErrorMessage =
                "A simulated processing failure occurred.",

            IsRetryable =
                isRetryable,

            RetryCount =
                1,

            CreatedAtUtc =
                DateTime.UtcNow
        };
    }
}