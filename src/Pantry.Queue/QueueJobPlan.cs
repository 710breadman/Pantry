using Pantry.Domain;

namespace Pantry.Queue;

public sealed record QueueJobPlan
{
    public required int Order { get; init; }

    public required string AppId { get; init; }

    public required string AppName { get; init; }

    public required QueueJobAction Action { get; init; }

    public required ProviderType Provider { get; init; }

    public required TrustLevel TrustLevel { get; init; }

    public required MachineScopePreference ScopePreference { get; init; }

    public required AdministratorRequirement AdministratorRequirement { get; init; }

    public required IReadOnlyList<string> Dependencies { get; init; }

    public required IReadOnlyList<string> Conflicts { get; init; }

    public required QueueJobReviewState ReviewState { get; init; }

    public required string ReviewReason { get; init; }
}
