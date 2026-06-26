namespace Pantry.Infrastructure;

public static class PantryDataPaths
{
    public static string DefaultStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "ThePantry");
    }

    public static string DefaultDatabasePath()
    {
        return Path.Combine(DefaultStateDirectory(), "pantry.db");
    }
}

