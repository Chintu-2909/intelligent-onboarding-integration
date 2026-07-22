using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Services;

public interface IErrorGuidanceCatalogue
{
    ApprovedErrorGuidance GetGuidance(
        OnboardingTransaction transaction);
}