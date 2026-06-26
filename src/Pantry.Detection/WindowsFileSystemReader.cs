using System.Diagnostics;

namespace Pantry.Detection;

public sealed class WindowsFileSystemReader : IFileSystemReader
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string? TryGetFileVersion(string path)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(version.FileVersion) ? null : version.FileVersion;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

