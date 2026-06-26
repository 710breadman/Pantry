using Pantry.Domain;
using Pantry.Infrastructure;
using Pantry.Queue;

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

    [Fact]
    public async Task User_settings_can_be_saved_and_loaded()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var settings = new UserSettingsStore(database);

        try
        {
            await database.InitializeAsync();
            await settings.SaveSelectedProfileIdAsync("repair-toolkit-safe");
            await settings.SavePortableDestinationAsync(@"E:\PantryTools");

            var loaded = await settings.LoadAsync();

            Assert.Equal("repair-toolkit-safe", loaded.SelectedProfileId);
            Assert.Equal(@"E:\PantryTools", loaded.PortableDestination);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task App_selections_are_saved_per_profile()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var selections = new AppSelectionStore(database);

        try
        {
            await database.InitializeAsync();
            await selections.SaveAsync("gaming-setup", "steam", isSelected: true);
            await selections.SaveAsync("gaming-setup", "vlc", isSelected: false);
            await selections.SaveAsync("repair-toolkit-safe", "vlc", isSelected: true);

            var gaming = await selections.LoadAsync("gaming-setup");
            var repair = await selections.LoadAsync("repair-toolkit-safe");

            Assert.True(gaming["steam"]);
            Assert.False(gaming["vlc"]);
            Assert.True(repair["vlc"]);
            Assert.False(gaming.ContainsKey("sysinternals-autoruns"));
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Review_sessions_can_be_saved_and_listed()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var sessions = new ReviewSessionStore(database);

        try
        {
            await database.InitializeAsync();
            await sessions.SaveAsync(new DryRunPlan
            {
                ProfileId = "gaming-setup",
                ProfileName = "Gaming Setup",
                Items =
                [
                    TestPlanItem("steam", "Steam", DryRunIntent.Install),
                    TestPlanItem("7zip", "7-Zip", DryRunIntent.Skip)
                ]
            }, "0.1.0-test");

            var recent = await sessions.ListRecentAsync(5);
            var count = await sessions.CountAsync();

            var session = Assert.Single(recent);
            Assert.Equal("gaming-setup", session.ProfileId);
            Assert.Equal("0.1.0-test", session.CatalogVersion);
            Assert.Equal(2, session.ItemCount);
            Assert.Equal(1, session.InstallCount);
            Assert.Equal(0, session.UpdateCount);
            Assert.Equal(1, session.SkipCount);
            Assert.Equal(1, count);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Review_sessions_can_be_pruned_to_limit()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var sessions = new ReviewSessionStore(database);

        try
        {
            await database.InitializeAsync();
            for (var index = 0; index < 4; index++)
            {
                await sessions.SaveAsync(new DryRunPlan
                {
                    ProfileId = $"profile-{index}",
                    ProfileName = $"Profile {index}",
                    Items = [TestPlanItem("steam", "Steam", DryRunIntent.Install)]
                }, "0.1.0-test");
            }

            var pruned = await sessions.PruneToLimitAsync(2);
            var count = await sessions.CountAsync();
            var recent = await sessions.ListRecentAsync(5);

            Assert.Equal(2, pruned);
            Assert.Equal(2, count);
            Assert.Equal(2, recent.Count);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Queue_sessions_can_be_saved_counted_and_listed()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var queueSessions = new QueueSessionStore(database);

        try
        {
            await database.InitializeAsync();
            await queueSessions.SaveAsync(new QueueSessionPlan
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTimeOffset.UtcNow,
                ProfileId = "gaming-setup",
                ProfileName = "Gaming Setup",
                Jobs =
                [
                    TestQueueJob("steam", "Steam", QueueJobReviewState.ReviewRequired),
                    TestQueueJob("7zip", "7-Zip", QueueJobReviewState.Ready)
                ]
            });

            var count = await queueSessions.CountAsync();
            var recent = await queueSessions.ListRecentAsync(5);

            var session = Assert.Single(recent);
            Assert.Equal(1, count);
            Assert.Equal("gaming-setup", session.ProfileId);
            Assert.Equal(2, session.JobCount);
            Assert.Equal(1, session.ReviewRequiredCount);
        }
        finally
        {
            Directory.Delete(testPath.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Queue_sessions_can_be_pruned_to_limit()
    {
        var testPath = TestDatabasePath();
        var database = new PantryDatabase(testPath.DatabasePath);
        var queueSessions = new QueueSessionStore(database);

        try
        {
            await database.InitializeAsync();
            for (var index = 0; index < 4; index++)
            {
                await queueSessions.SaveAsync(new QueueSessionPlan
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ProfileId = $"profile-{index}",
                    ProfileName = $"Profile {index}",
                    Jobs = [TestQueueJob("steam", "Steam", QueueJobReviewState.ReviewRequired)]
                });
            }

            var pruned = await queueSessions.PruneToLimitAsync(2);
            var sessionCount = await queueSessions.CountAsync();
            var jobCount = await queueSessions.CountJobsAsync();
            var recent = await queueSessions.ListRecentAsync(5);

            Assert.Equal(2, pruned);
            Assert.Equal(2, sessionCount);
            Assert.Equal(2, jobCount);
            Assert.Equal(2, recent.Count);
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

    private static DryRunPlanItem TestPlanItem(string appId, string appName, DryRunIntent intent)
    {
        return new DryRunPlanItem
        {
            AppId = appId,
            AppName = appName,
            Intent = intent,
            PreferredProvider = ProviderType.Winget,
            TrustLevel = TrustLevel.Experimental,
            ScopePreference = MachineScopePreference.Preferred,
            AdministratorRequirement = AdministratorRequirement.Required,
            DetectionState = DetectedAppState.Unknown,
            DetectionConfidence = DetectionConfidence.Unknown,
            DetectionSummary = "Test.",
            Dependencies = [],
            Conflicts = [],
            ConflictSummary = "None",
            PortableDestination = null,
            Reason = "Test."
        };
    }

    private static QueueJobPlan TestQueueJob(
        string appId,
        string appName,
        QueueJobReviewState reviewState)
    {
        return new QueueJobPlan
        {
            Order = appId == "steam" ? 1 : 2,
            AppId = appId,
            AppName = appName,
            Action = QueueJobAction.Install,
            Provider = ProviderType.Winget,
            TrustLevel = TrustLevel.Experimental,
            ScopePreference = MachineScopePreference.Preferred,
            AdministratorRequirement = AdministratorRequirement.Required,
            Dependencies = [],
            Conflicts = [],
            ReviewState = reviewState,
            ReviewReason = "Test."
        };
    }
}
