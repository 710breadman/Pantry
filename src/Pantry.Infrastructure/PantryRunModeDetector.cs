using Pantry.Domain;

namespace Pantry.Infrastructure;

public sealed class PantryRunModeDetector
{
    public const string PortableMarkerFileName = "pantry.portable";

    private readonly IAppRuntimeEnvironment _environment;

    public PantryRunModeDetector(IAppRuntimeEnvironment environment)
    {
        _environment = environment;
    }

    public PantryRunModeDetection Detect()
    {
        var applicationDirectory = NormalizeDirectory(_environment.ApplicationDirectory);
        var portableMarkerPath = Path.Combine(applicationDirectory, PortableMarkerFileName);

        if (!string.IsNullOrWhiteSpace(applicationDirectory) &&
            _environment.FileExists(portableMarkerPath))
        {
            return new PantryRunModeDetection
            {
                Mode = PantryRunMode.Portable,
                ApplicationDirectory = applicationDirectory,
                StateDirectory = PantryDataPaths.PortableStateDirectory(applicationDirectory),
                PortableMarkerPath = portableMarkerPath,
                Reason = $"Portable marker file found: {PortableMarkerFileName}."
            };
        }

        if (IsUnderDirectory(applicationDirectory, _environment.ProgramFilesDirectory) ||
            IsUnderDirectory(applicationDirectory, _environment.ProgramFilesX86Directory))
        {
            return new PantryRunModeDetection
            {
                Mode = PantryRunMode.Installed,
                ApplicationDirectory = applicationDirectory,
                StateDirectory = PantryDataPaths.StateDirectoryForLocalAppData(_environment.LocalApplicationDataDirectory),
                PortableMarkerPath = portableMarkerPath,
                Reason = "Application is running from a Program Files directory."
            };
        }

        return new PantryRunModeDetection
        {
            Mode = PantryRunMode.Unknown,
            ApplicationDirectory = applicationDirectory,
            StateDirectory = PantryDataPaths.StateDirectoryForLocalAppData(_environment.LocalApplicationDataDirectory),
            PortableMarkerPath = portableMarkerPath,
            Reason = "No portable marker was found and the app is not running from Program Files."
        };
    }

    private static bool IsUnderDirectory(string candidateDirectory, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidateDirectory) || string.IsNullOrWhiteSpace(rootDirectory))
        {
            return false;
        }

        var candidate = EnsureTrailingSeparator(NormalizeDirectory(candidateDirectory));
        var root = EnsureTrailingSeparator(NormalizeDirectory(rootDirectory));
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (NotSupportedException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (PathTooLongException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path) ||
            path.EndsWith(Path.DirectorySeparatorChar) ||
            path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
