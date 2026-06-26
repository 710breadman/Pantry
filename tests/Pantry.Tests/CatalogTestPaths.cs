namespace Pantry.Tests;

internal static class CatalogTestPaths
{
    public static string BundledCatalogRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "catalog", "bundled");
            if (File.Exists(Path.Combine(candidate, "recipe.schema.json")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find catalog/bundled from the test output directory.");
    }
}

