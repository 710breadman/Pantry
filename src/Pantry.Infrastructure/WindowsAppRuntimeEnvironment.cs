namespace Pantry.Infrastructure;

public sealed class WindowsAppRuntimeEnvironment : IAppRuntimeEnvironment
{
    public string ApplicationDirectory => AppContext.BaseDirectory;

    public string LocalApplicationDataDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string ProgramFilesDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    public string ProgramFilesX86Directory =>
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }
}
