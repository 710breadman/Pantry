using Pantry.Domain;
using Pantry.Infrastructure;

namespace Pantry.Tests;

public sealed class PantryDatabaseTests
{
    [Fact]
    public async Task Database_initializes_and_stores_operation_logs()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var logs = new OperationLogStore(database);

        try
        {
            await database.InitializeAsync();
            await logs.AppendAsync("test", "Stored a safe operation log.");

            var recent = await logs.ListRecentAsync(5);

            Assert.Single(recent);
            Assert.Equal("test", recent[0].Category);
            Assert.Equal("Stored a safe operation log.", recent[0].Message);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_results_can_be_saved_and_loaded()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var scans = new ScanResultStore(database);

        try
        {
            await database.InitializeAsync();
            await scans.SaveAsync(
            [
                new AppDetectionResult
                {
                    AppId = "7zip",
                    State = DetectedAppState.UpdateAvailable,
                    Confidence = DetectionConfidence.High,
                    InstalledVersion = "24.09",
                    AvailableVersion = "25.00",
                    Evidence = [],
                    Summary = "Test scan."
                }
            ]);

            var loaded = await scans.LoadAsync();

            Assert.True(loaded.ContainsKey("7zip"));
            Assert.Equal(DetectedAppState.UpdateAvailable, loaded["7zip"].State);
            Assert.Equal("24.09", loaded["7zip"].InstalledVersion);
            Assert.Equal("25.00", loaded["7zip"].AvailableVersion);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    private static TestDatabaseLocation TestDatabasePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pantry-db-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return new TestDatabaseLocation
        {
            DirectoryPath = folder,
            DatabasePath = Path.Combine(folder, "pantry.db")
        };
    }

    private sealed record TestDatabaseLocation
    {
        public required string DirectoryPath { get; init; }

        public required string DatabasePath { get; init; }
    }
}
