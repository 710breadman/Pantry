using Pantry.Domain;
using Pantry.Infrastructure;

namespace Pantry.Tests;

public sealed class PantryRunModeDetectorTests
{
    [Fact]
    public void Detect_returns_portable_when_marker_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pantry-mode-{Guid.NewGuid():N}");
        var appDirectory = Path.Combine(root, "PantryPortable");
        var localAppData = Path.Combine(root, "LocalAppData");
        var marker = Path.Combine(appDirectory, PantryRunModeDetector.PortableMarkerFileName);
        var environment = new FakeAppRuntimeEnvironment
        {
            ApplicationDirectory = appDirectory,
            LocalApplicationDataDirectory = localAppData,
            ProgramFilesDirectory = Path.Combine(root, "Program Files"),
            ProgramFilesX86Directory = Path.Combine(root, "Program Files (x86)"),
            ExistingFiles = [marker]
        };

        var detection = new PantryRunModeDetector(environment).Detect();

        Assert.Equal(PantryRunMode.Portable, detection.Mode);
        Assert.Equal(Path.Combine(appDirectory, PantryDataPaths.PortableStateFolderName), detection.StateDirectory);
        Assert.Equal(marker, detection.PortableMarkerPath);
    }

    [Fact]
    public void Detect_returns_installed_when_app_is_under_program_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pantry-mode-{Guid.NewGuid():N}");
        var programFiles = Path.Combine(root, "Program Files");
        var localAppData = Path.Combine(root, "LocalAppData");
        var appDirectory = Path.Combine(programFiles, "The Pantry");
        var environment = new FakeAppRuntimeEnvironment
        {
            ApplicationDirectory = appDirectory,
            LocalApplicationDataDirectory = localAppData,
            ProgramFilesDirectory = programFiles,
            ProgramFilesX86Directory = Path.Combine(root, "Program Files (x86)"),
            ExistingFiles = []
        };

        var detection = new PantryRunModeDetector(environment).Detect();

        Assert.Equal(PantryRunMode.Installed, detection.Mode);
        Assert.Equal(Path.Combine(localAppData, PantryDataPaths.ApplicationDataFolderName), detection.StateDirectory);
    }

    [Fact]
    public void Detect_returns_unknown_for_development_or_unrecognized_location()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pantry-mode-{Guid.NewGuid():N}");
        var appDirectory = Path.Combine(root, "source", "bin");
        var localAppData = Path.Combine(root, "LocalAppData");
        var environment = new FakeAppRuntimeEnvironment
        {
            ApplicationDirectory = appDirectory,
            LocalApplicationDataDirectory = localAppData,
            ProgramFilesDirectory = Path.Combine(root, "Program Files"),
            ProgramFilesX86Directory = Path.Combine(root, "Program Files (x86)"),
            ExistingFiles = []
        };

        var detection = new PantryRunModeDetector(environment).Detect();

        Assert.Equal(PantryRunMode.Unknown, detection.Mode);
        Assert.Equal(Path.Combine(localAppData, PantryDataPaths.ApplicationDataFolderName), detection.StateDirectory);
    }

    [Fact]
    public void Portable_marker_wins_over_program_files_location()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pantry-mode-{Guid.NewGuid():N}");
        var programFiles = Path.Combine(root, "Program Files");
        var appDirectory = Path.Combine(programFiles, "The Pantry");
        var marker = Path.Combine(appDirectory, PantryRunModeDetector.PortableMarkerFileName);
        var environment = new FakeAppRuntimeEnvironment
        {
            ApplicationDirectory = appDirectory,
            LocalApplicationDataDirectory = Path.Combine(root, "LocalAppData"),
            ProgramFilesDirectory = programFiles,
            ProgramFilesX86Directory = Path.Combine(root, "Program Files (x86)"),
            ExistingFiles = [marker]
        };

        var detection = new PantryRunModeDetector(environment).Detect();

        Assert.Equal(PantryRunMode.Portable, detection.Mode);
        Assert.Equal(Path.Combine(appDirectory, PantryDataPaths.PortableStateFolderName), detection.StateDirectory);
    }

    private sealed class FakeAppRuntimeEnvironment : IAppRuntimeEnvironment
    {
        public required string ApplicationDirectory { get; init; }

        public required string LocalApplicationDataDirectory { get; init; }

        public required string ProgramFilesDirectory { get; init; }

        public required string ProgramFilesX86Directory { get; init; }

        public required IReadOnlyCollection<string> ExistingFiles { get; init; }

        public bool FileExists(string path)
        {
            return ExistingFiles.Contains(path, StringComparer.OrdinalIgnoreCase);
        }
    }
}
