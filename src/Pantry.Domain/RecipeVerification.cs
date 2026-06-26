namespace Pantry.Domain;

public sealed record RecipeVerification
{
    public required DateOnly VerifiedOn { get; init; }

    public required string Evidence { get; init; }
}

