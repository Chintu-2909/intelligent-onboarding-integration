using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Services;

public sealed class ErrorGuidanceCatalogue
    : IErrorGuidanceCatalogue
{
    private static readonly IReadOnlyDictionary<
        string,
        ApprovedErrorGuidance> GuidanceByErrorCode =
        new Dictionary<string, ApprovedErrorGuidance>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["HCM-VALIDATION-400"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system rejected the transaction during validation.",

                    ProbableExplanation =
                        "The exact validation cause is not available in the recorded error.",

                    RecommendedAction =
                        "Review the available downstream validation details and identify the rejected value before correcting the transaction.",

                    RetryGuidance =
                        "Do not retry until the recorded issue has been reviewed and corrected."
                },

            ["HCM-DUPLICATE-409"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system reported that the employee record already exists.",

                    ProbableExplanation =
                        "The transaction conflicts with an existing downstream record.",

                    RecommendedAction =
                        "Verify whether the employee was created previously and determine whether the onboarding transaction should be closed or reconciled.",

                    RetryGuidance =
                        "Do not retry unless the duplicate condition has been resolved."
                },

            ["HCM-UNAVAILABLE-503"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system reported that the service was temporarily unavailable.",

                    ProbableExplanation =
                        "The failure is temporary and does not indicate a confirmed business-data issue.",

                    RecommendedAction =
                        "Confirm that the downstream service is available before performing a controlled retry.",

                    RetryGuidance =
                        "A controlled retry may be performed within the configured retry limit."
                },

            ["HCM-RECOVERABLE-503"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system returned a simulated recoverable temporary failure.",

                    ProbableExplanation =
                        "The downstream service has not yet completed the request successfully.",

                    RecommendedAction =
                        "Wait briefly and perform a controlled retry while monitoring the remaining attempt count.",

                    RetryGuidance =
                        "A controlled retry may be performed within the configured retry limit."
                },

            ["HCM-SERVER-500"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system returned an internal server error.",

                    ProbableExplanation =
                        "The recorded error does not identify the internal downstream cause.",

                    RecommendedAction =
                        "Review downstream service health and technical logs before performing another attempt.",

                    RetryGuidance =
                        "A controlled retry may be performed only when the transaction remains retryable."
                },

            ["HCM-TIMEOUT-408"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system did not respond within the configured timeout.",

                    ProbableExplanation =
                        "The request exceeded the allowed response time. The downstream processing result is not confirmed.",

                    RecommendedAction =
                        "Check downstream availability and confirm that no employee record was created before retrying.",

                    RetryGuidance =
                        "A controlled retry may be performed within the configured retry limit."
                },

            ["HCM-CONNECTION-ERROR"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The Onboarding API could not establish a connection to the downstream system.",

                    ProbableExplanation =
                        "The downstream endpoint was unavailable or unreachable when the request was attempted.",

                    RecommendedAction =
                        "Verify that the downstream service is running and reachable before performing another attempt.",

                    RetryGuidance =
                        "A controlled retry may be performed within the configured retry limit."
                },

            ["HCM-INVALID-RESPONSE"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "The downstream system returned an empty or invalid success response.",

                    ProbableExplanation =
                        "The Onboarding API could not confirm successful employee creation from the returned response.",

                    RecommendedAction =
                        "Review the downstream response and confirm whether the employee record was created.",

                    RetryGuidance =
                        "Do not retry until the downstream outcome has been verified."
                },

            ["ONBOARDING-UNEXPECTED-ERROR"] =
                new ApprovedErrorGuidance
                {
                    KnownMeaning =
                        "An unexpected error occurred during onboarding processing.",

                    ProbableExplanation =
                        "The exact technical cause is not available in the recorded business error.",

                    RecommendedAction =
                        "Review the correlated application logs and resolve the recorded technical error before continuing.",

                    RetryGuidance =
                        "Do not retry unless technical review confirms that another attempt is safe."
                }
        };

    public ApprovedErrorGuidance GetGuidance(
        OnboardingTransaction transaction)
    {
        if (!string.IsNullOrWhiteSpace(
                transaction.ErrorCode) &&
            GuidanceByErrorCode.TryGetValue(
                transaction.ErrorCode,
                out var approvedGuidance))
        {
            return approvedGuidance;
        }

        return CreateFallbackGuidance(transaction);
    }

    private static ApprovedErrorGuidance
        CreateFallbackGuidance(
            OnboardingTransaction transaction)
    {
        return new ApprovedErrorGuidance
        {
            KnownMeaning =
                "The transaction contains a recorded processing failure.",

            ProbableExplanation =
                "The exact cause is not available in the approved error catalogue.",

            RecommendedAction =
                "Review the recorded error and correlated application logs before taking further action.",

            RetryGuidance =
                transaction.IsRetryable
                    ? "A controlled retry may be performed within the configured retry limit."
                    : "Do not retry until the recorded issue has been reviewed and corrected."
        };
    }
}
