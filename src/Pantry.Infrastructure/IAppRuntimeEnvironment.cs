namespace Pantry.Infrastructure;

public interface IAppRuntimeEnvironment
{
    string ApplicationDirectory { get; }

    string LocalApplicationDataDirectory { get; }

    string ProgramFilesDirectory { get; }

    string ProgramFilesX86Directory { get; }

    bool FileExists(string path);
}
