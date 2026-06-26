namespace Pantry.Domain;

public sealed record DetectionRecipe
{
    public required IReadOnlyList<string> Rules { get; init; }

    public required string MinimumConfidence { get; init; }
}

