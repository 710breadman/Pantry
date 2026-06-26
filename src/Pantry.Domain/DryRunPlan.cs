namespace Pantry.Domain;

public sealed record DryRunPlan
{
    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public required IReadOnlyList<DryRunPlanItem> Items { get; init; }
}

