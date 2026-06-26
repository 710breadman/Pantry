namespace Pantry.UI;

public static class CatalogPathProvider
{
    public static string BundledCatalogRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "catalog", "bundled");
    }
}

