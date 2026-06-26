using Pantry.Domain;
using Pantry.Queue;

namespace Pantry.Infrastructure;

public sealed record QueueJobRecord
{
    public required int Order { get; init; }

    public required string AppId { get; init; }

    public required string AppName { get; init; }

    public required QueueJobAction Action { get; init; }

    public required QueueJobStatus Status { get; init; }

    public required ProviderType Provider { get; init; }

    public required TrustLevel TrustLevel { get; init; }

    public required QueueJobReviewState ReviewState { get; init; }

    public required string ReviewReason { get; init; }
}
