using Pantry.Domain;

namespace Pantry.Infrastructure;

public static class PantryDataPaths
{
    public const string ApplicationDataFolderName = "ThePantry";

    public const string PortableStateFolderName = "data";

    public const string DatabaseFileName = "pantry.db";

    public static string DefaultStateDirectory(PantryRunModeDetection? runMode = null)
    {
        if (runMode?.Mode == PantryRunMode.Portable)
        {
            return runMode.StateDirectory;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return StateDirectoryForLocalAppData(localAppData);
    }

    public static string DefaultDatabasePath(PantryRunModeDetection? runMode = null)
    {
        return Path.Combine(DefaultStateDirectory(runMode), DatabaseFileName);
    }

    public static string StateDirectoryForLocalAppData(string localAppDataDirectory)
    {
        return Path.Combine(localAppDataDirectory, ApplicationDataFolderName);
    }

    public static string PortableStateDirectory(string applicationDirectory)
    {
        return Path.Combine(applicationDirectory, PortableStateFolderName);
    }
}
