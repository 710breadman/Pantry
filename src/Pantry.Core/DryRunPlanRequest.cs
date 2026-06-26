using Pantry.Domain;

namespace Pantry.Core;

public sealed record DryRunPlanRequest
{
    public required CatalogSnapshot Catalog { get; init; }

    public required Profile Profile { get; init; }

    public IReadOnlyDictionary<string, AppSelectionState> SelectionOverrides { get; init; }
        = new Dictionary<string, AppSelectionState>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, DetectedAppState> DetectedStates { get; init; }
        = new Dictionary<string, DetectedAppState>(StringComparer.OrdinalIgnoreCase);

    public string? PortableDestination { get; init; }
}

