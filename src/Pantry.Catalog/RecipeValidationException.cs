namespace Pantry.Catalog;

public sealed class RecipeValidationException : Exception
{
    public RecipeValidationException(string message)
        : base(message)
    {
    }
}

