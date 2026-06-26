namespace Pantry.Domain;

public sealed record DryRunPlanItem
{
    public required string AppId { get; init; }

    public required string AppName { get; init; }

    public required DryRunIntent Intent { get; init; }

    public required ProviderType PreferredProvider { get; init; }

    public required TrustLevel TrustLevel { get; init; }

    public required MachineScopePreference ScopePreference { get; init; }

    public required AdministratorRequirement AdministratorRequirement { get; init; }

    public required DetectedAppState DetectionState { get; init; }

    public required DetectionConfidence DetectionConfidence { get; init; }

    public required string DetectionSummary { get; init; }

    public required IReadOnlyList<string> Dependencies { get; init; }

    public required IReadOnlyList<string> Conflicts { get; init; }

    public required string ConflictSummary { get; init; }

    public string? PortableDestination { get; init; }

    public required string Reason { get; init; }
}
