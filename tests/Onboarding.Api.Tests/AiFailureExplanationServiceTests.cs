using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Onboarding.Api.Entities;
using Onboarding.Api.Services;

namespace Onboarding.Api.Tests;

[TestFixture]
public sealed class AiFailureExplanationServiceTests
{
    [Test]
    public async Task ExplainAsync_WithValidGroundedResponse_ReturnsAiResponse()
    {
        const string aiExplanation =
            """
            Known facts:
            The transaction failed downstream validation.

            Probable explanation:
            The exact validation cause is not available in the recorded error.

            Recommended action:
            Review the available downstream validation details before correcting the transaction.

            Retry guidance:
            Do not retry until the recorded issue has been reviewed and corrected.
            """;

        var service =
            CreateService(
                CreateSuccessfulResponse(
                    aiExplanation));

        var transaction =
            CreateValidationFailedTransaction();

        var result =
            await service.ExplainAsync(
                transaction,
                CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.TransactionId,
                Is.EqualTo(transaction.TransactionId));

            Assert.That(
                result.Status,
                Is.EqualTo("ValidationFailed"));

            Assert.That(
                result.ErrorCode,
                Is.EqualTo("HCM-VALIDATION-400"));

            Assert.That(
                result.Explanation,
                Does.Contain("Known facts:"));

            Assert.That(
                result.Explanation,
                Does.Contain("Do not retry"));

            Assert.That(
                result.Model,
                Is.EqualTo("phi4-mini"));

            Assert.That(
                result.IsRetryable,
                Is.False);
        });
    }

    [Test]
    public async Task ExplainAsync_WithUnsupportedAiWording_ReturnsApprovedFallback()
    {
        const string unsafeExplanation =
            """
            Known facts:
            The employee transaction failed.

            Probable explanation:
            Human Capital Management rejected the request because of missing required fields.

            Recommended action:
            Check the employee details against HR guidelines.

            Retry guidance:
            Do not retry until the issue is corrected.
            """;

        var service =
            CreateService(
                CreateSuccessfulResponse(
                    unsafeExplanation));

        var transaction =
            CreateValidationFailedTransaction();

        var result =
            await service.ExplainAsync(
                transaction,
                CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Model,
                Is.EqualTo(
                    "approved-error-catalogue"));

            Assert.That(
                result.Explanation,
                Does.Contain(
                    "The exact validation cause is not available in the recorded error."));

            Assert.That(
                result.Explanation,
                Does.Not.Contain(
                    "Human Capital Management"));

            Assert.That(
                result.Explanation,
                Does.Not.Contain(
                    "missing required fields"));

            Assert.That(
                result.Explanation,
                Does.Not.Contain(
                    "HR guidelines"));

            Assert.That(
                result.Explanation,
                Does.Contain("Do not retry"));
        });
    }

    [Test]
    public async Task ExplainAsync_WhenOllamaIsUnavailable_ReturnsApprovedFallback()
    {
        var service =
            CreateService(
                new HttpRequestException(
                    "Ollama is unavailable."));

        var transaction =
            CreateValidationFailedTransaction();

        var result =
            await service.ExplainAsync(
                transaction,
                CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Model,
                Is.EqualTo(
                    "approved-error-catalogue"));

            Assert.That(
                result.TransactionId,
                Is.EqualTo(transaction.TransactionId));

            Assert.That(
                result.Explanation,
                Does.Contain(
                    "The downstream system rejected the transaction during validation."));

            Assert.That(
                result.Explanation,
                Does.Contain(
                    "The exact validation cause is not available in the recorded error."));

            Assert.That(
                result.Explanation,
                Does.Contain("Do not retry"));
        });
    }

    [Test]
    public async Task ExplainAsync_WithRetryableTimeout_PreservesControlledRetryGuidance()
    {
        const string aiExplanation =
            """
            Known facts:
            The downstream system did not respond within the configured timeout.

            Probable explanation:
            The request exceeded the configured response time.

            Recommended action:
            Check downstream availability before another attempt.

            Retry guidance:
            A controlled retry may be performed within the configured retry limit.
            """;

        var service =
            CreateService(
                CreateSuccessfulResponse(
                    aiExplanation));

        var transaction =
            CreateTimeoutTransaction();

        var result =
            await service.ExplainAsync(
                transaction,
                CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Model,
                Is.EqualTo("phi4-mini"));

            Assert.That(
                result.IsRetryable,
                Is.True);

            Assert.That(
                result.RetryCount,
                Is.EqualTo(1));

            Assert.That(
                result.Explanation,
                Does.Contain("controlled retry"));

            Assert.That(
                result.Explanation,
                Does.Contain(
                    "configured retry limit"));
        });
    }

    private static AiFailureExplanationService
        CreateService(
            HttpResponseMessage response)
    {
        var handler =
            new TestHttpMessageHandler(
                _ => Task.FromResult(response));

        return CreateService(handler);
    }

    private static AiFailureExplanationService
        CreateService(
            HttpRequestException exception)
    {
        var handler =
            new TestHttpMessageHandler(
                _ =>
                    Task.FromException<HttpResponseMessage>(
                        exception));

        return CreateService(handler);
    }

    private static AiFailureExplanationService
        CreateService(
            HttpMessageHandler handler)
    {
        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:11434")
            };

        var httpClientFactory =
            new TestHttpClientFactory(httpClient);

        var configurationValues =
            new Dictionary<string, string?>
            {
                ["Ollama:Model"] =
                    "phi4-mini"
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var catalogue =
            new ErrorGuidanceCatalogue();

        return new AiFailureExplanationService(
            httpClientFactory,
            configuration,
            catalogue,
            NullLogger<
                AiFailureExplanationService>.Instance);
    }

    private static HttpResponseMessage
        CreateSuccessfulResponse(
            string explanation)
    {
        var escapedExplanation =
            System.Text.Json.JsonSerializer.Serialize(
                explanation);

        var json =
            $$"""
            {
              "model": "phi4-mini",
              "response": {{escapedExplanation}},
              "done": true,
              "done_reason": "stop"
            }
            """;

        return new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }

    private static OnboardingTransaction
        CreateValidationFailedTransaction()
    {
        return new OnboardingTransaction
        {
            TransactionId =
                Guid.NewGuid(),

            EmployeeNumber =
                "EMP-AI-TEST-1001",

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
                "ValidationFailed",

            ErrorCode =
                "HCM-VALIDATION-400",

            ErrorMessage =
                "The simulated HCM system rejected the employee data.",

            IsRetryable =
                false,

            RetryCount =
                1,

            CreatedAtUtc =
                DateTime.UtcNow
        };
    }

    private static OnboardingTransaction
        CreateTimeoutTransaction()
    {
        return new OnboardingTransaction
        {
            TransactionId =
                Guid.NewGuid(),

            EmployeeNumber =
                "EMP-AI-TEST-1002",

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
                "TimedOut",

            ErrorCode =
                "HCM-TIMEOUT-408",

            ErrorMessage =
                "The simulated HCM API did not respond within the configured timeout.",

            IsRetryable =
                true,

            RetryCount =
                1,

            CreatedAtUtc =
                DateTime.UtcNow
        };
    }

    private sealed class TestHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return httpClient;
        }
    }

    private sealed class TestHttpMessageHandler(
        Func<
            HttpRequestMessage,
            Task<HttpResponseMessage>>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return responseFactory(request);
        }
    }
}