using System.Net.Http.Json;
using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Services;

public sealed class AiFailureExplanationService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IErrorGuidanceCatalogue errorGuidanceCatalogue,
    ILogger<AiFailureExplanationService> logger)
    : IAiFailureExplanationService
{
    private static readonly string[] RequiredHeadings =
    [
        "Known facts:",
        "Probable explanation:",
        "Recommended action:",
        "Retry guidance:"
    ];

    private static readonly string[] ProhibitedPhrases =
    [
        "Human Capital Management",
        "HR Cloud Management",
        "missing required fields",
        "ID number mismatch",
        "HCM standards",
        "company policy",
        "HR guidelines"
    ];

    public async Task<AiFailureExplanationResponse> ExplainAsync(
        OnboardingTransaction transaction,
        CancellationToken cancellationToken)
    {
        var model =
            configuration["Ollama:Model"]
            ?? throw new InvalidOperationException(
                "Ollama model was not configured.");

        var approvedGuidance =
            errorGuidanceCatalogue.GetGuidance(
                transaction);

        var fallbackExplanation =
            BuildDeterministicExplanation(
                transaction,
                approvedGuidance);

        var request =
            new OllamaGenerateRequest
            {
                Model = model,

                Prompt =
                    BuildGroundedPrompt(
                        transaction,
                        approvedGuidance),

                Stream = false,

                Options =
                    new OllamaGenerationOptions
                    {
                        Temperature = 0,
                        MaximumGeneratedTokens = 220
                    }
            };

        var httpClient =
            httpClientFactory.CreateClient(
                "OllamaApi");

        logger.LogInformation(
            "Grounded AI explanation requested for transaction {TransactionId}, status {Status}, error code {ErrorCode}",
            transaction.TransactionId,
            transaction.Status,
            transaction.ErrorCode);

        try
        {
            using var response =
                await httpClient.PostAsJsonAsync(
                    "/api/generate",
                    request,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            var ollamaResponse =
                await response.Content
                    .ReadFromJsonAsync<
                        OllamaGenerateResponse>(
                        cancellationToken:
                            cancellationToken);

            var generatedExplanation =
                ollamaResponse?.Response?.Trim();

            if (!IsAcceptableExplanation(
                    generatedExplanation,
                    approvedGuidance))
            {
                logger.LogWarning(
                    "AI response failed grounding validation for transaction {TransactionId}. Deterministic guidance was returned",
                    transaction.TransactionId);

                return CreateResponse(
                    transaction,
                    fallbackExplanation,
                    model: "approved-error-catalogue");
            }

            logger.LogInformation(
                "Grounded AI explanation generated for transaction {TransactionId} using model {Model}",
                transaction.TransactionId,
                model);

            return CreateResponse(
                transaction,
                generatedExplanation!,
                string.IsNullOrWhiteSpace(
                    ollamaResponse?.Model)
                    ? model
                    : ollamaResponse.Model);
        }
        catch (TaskCanceledException)
            when (!cancellationToken
                .IsCancellationRequested)
        {
            logger.LogWarning(
                "AI generation timed out for transaction {TransactionId}. Deterministic guidance was returned",
                transaction.TransactionId);

            return CreateResponse(
                transaction,
                fallbackExplanation,
                model: "approved-error-catalogue");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "AI service was unavailable for transaction {TransactionId}. Deterministic guidance was returned",
                transaction.TransactionId);

            return CreateResponse(
                transaction,
                fallbackExplanation,
                model: "approved-error-catalogue");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "AI returned an invalid response for transaction {TransactionId}. Deterministic guidance was returned",
                transaction.TransactionId);

            return CreateResponse(
                transaction,
                fallbackExplanation,
                model: "approved-error-catalogue");
        }
    }

    private static AiFailureExplanationResponse
        CreateResponse(
            OnboardingTransaction transaction,
            string explanation,
            string model)
    {
        return new AiFailureExplanationResponse
        {
            TransactionId =
                transaction.TransactionId,

            Status =
                transaction.Status,

            ErrorCode =
                transaction.ErrorCode,

            IsRetryable =
                transaction.IsRetryable,

            RetryCount =
                transaction.RetryCount,

            Explanation =
                explanation,

            Model =
                model,

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    private static string BuildGroundedPrompt(
        OnboardingTransaction transaction,
        ApprovedErrorGuidance guidance)
    {
        return
            """
            Rewrite the approved operational guidance below
            into concise and professional language.

            Mandatory rules:
            - Use only the approved guidance.
            - Do not add causes, fields, policies,
              abbreviations, definitions, or recommendations.
            - Do not expand HCM.
            - Preserve the retry decision exactly.
            - Every required heading must contain text.
            - Keep the response under 160 words.
            - Do not reproduce these instructions.

            Return exactly these four headings:

            Known facts:
            Probable explanation:
            Recommended action:
            Retry guidance:

            APPROVED_GUIDANCE_START

            Recorded status:
            {{STATUS}}

            Recorded error code:
            {{ERROR_CODE}}

            Approved known meaning:
            {{KNOWN_MEANING}}

            Approved probable explanation:
            {{PROBABLE_EXPLANATION}}

            Approved recommended action:
            {{RECOMMENDED_ACTION}}

            Approved retry guidance:
            {{RETRY_GUIDANCE}}

            APPROVED_GUIDANCE_END
            """
            .Replace(
                "{{STATUS}}",
                Sanitize(transaction.Status))
            .Replace(
                "{{ERROR_CODE}}",
                Sanitize(
                    transaction.ErrorCode
                    ?? "Not available"))
            .Replace(
                "{{KNOWN_MEANING}}",
                Sanitize(guidance.KnownMeaning))
            .Replace(
                "{{PROBABLE_EXPLANATION}}",
                Sanitize(
                    guidance.ProbableExplanation))
            .Replace(
                "{{RECOMMENDED_ACTION}}",
                Sanitize(
                    guidance.RecommendedAction))
            .Replace(
                "{{RETRY_GUIDANCE}}",
                Sanitize(
                    guidance.RetryGuidance));
    }

    private static string BuildDeterministicExplanation(
        OnboardingTransaction transaction,
        ApprovedErrorGuidance guidance)
    {
        return
            $"""
            Known facts:
            The transaction has status {transaction.Status}. The recorded error code is {transaction.ErrorCode ?? "not available"}. {guidance.KnownMeaning}

            Probable explanation:
            {guidance.ProbableExplanation}

            Recommended action:
            {guidance.RecommendedAction}

            Retry guidance:
            {guidance.RetryGuidance}
            """;
    }

    private static bool IsAcceptableExplanation(
        string? explanation,
        ApprovedErrorGuidance guidance)
    {
        if (string.IsNullOrWhiteSpace(explanation))
        {
            return false;
        }

        if (explanation.Length > 2000)
        {
            return false;
        }

        foreach (var heading in RequiredHeadings)
        {
            if (!explanation.Contains(
                    heading,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var prohibitedPhrase in
                 ProhibitedPhrases)
        {
            if (explanation.Contains(
                    prohibitedPhrase,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (explanation.Contains(
                "APPROVED_GUIDANCE_START",
                StringComparison.OrdinalIgnoreCase) ||
            explanation.Contains(
                "APPROVED_GUIDANCE_END",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var retryMeaningWasPreserved =
            guidance.RetryGuidance.Contains(
                "Do not retry",
                StringComparison.OrdinalIgnoreCase)
                ? ContainsNoRetryGuidance(explanation)
                : ContainsControlledRetryGuidance(
                    explanation);

        return retryMeaningWasPreserved;
    }

    private static bool ContainsNoRetryGuidance(
        string explanation)
    {
        return
            explanation.Contains(
                "do not retry",
                StringComparison.OrdinalIgnoreCase) ||
            explanation.Contains(
                "should not be retried",
                StringComparison.OrdinalIgnoreCase) ||
            explanation.Contains(
                "not retry",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsControlledRetryGuidance(
        string explanation)
    {
        return
            explanation.Contains(
                "controlled retry",
                StringComparison.OrdinalIgnoreCase) ||
            explanation.Contains(
                "retry may be performed",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string Sanitize(
        string value)
    {
        const int maximumLength = 500;

        var sanitized =
            value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength];
    }
}