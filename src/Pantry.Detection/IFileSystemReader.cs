namespace Pantry.Detection;

public interface IFileSystemReader
{
    bool FileExists(string path);

    string? TryGetFileVersion(string path);
}

