namespace Pantry.Catalog;

public sealed class CatalogLoadException : Exception
{
    public CatalogLoadException(string message)
        : base(message)
    {
    }
}

