using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Services;

public interface IAiFailureExplanationService
{
    Task<AiFailureExplanationResponse> ExplainAsync(
        OnboardingTransaction transaction,
        CancellationToken cancellationToken);
}