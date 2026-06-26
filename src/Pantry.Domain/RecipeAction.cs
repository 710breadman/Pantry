namespace Pantry.Domain;

public sealed record RecipeAction
{
    public required string Method { get; init; }

    public bool Supported { get; init; }
}

