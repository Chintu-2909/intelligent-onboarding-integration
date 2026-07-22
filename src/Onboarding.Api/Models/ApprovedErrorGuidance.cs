namespace Onboarding.Api.Models;

public sealed class ApprovedErrorGuidance
{
    public string KnownMeaning { get; init; } =
        string.Empty;

    public string ProbableExplanation { get; init; } =
        string.Empty;

    public string RecommendedAction { get; init; } =
        string.Empty;

    public string RetryGuidance { get; init; } =
        string.Empty;
}