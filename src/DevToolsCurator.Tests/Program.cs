using DevToolsCurator.Core;
using System.Text.Json;

var tests = new List<(string Name, Func<Task> Test)>
{
    ("catalog parses and Best Dev Stack is curated", CatalogParses),
    ("catalog schema and semantic validation are enforced", CatalogSchemaValidation),
    ("native and legacy catalog boundary is explicit", CatalogCompatibilityBoundary),
    ("version parsing is stable", VersionParsing),
    ("PATH append avoids duplicates", PathAppendAvoidsDuplicates),
    ("PATH repair service appends parent directory once", PathRepairServiceAppendsOnce),
    ("effective PATH includes persisted machine/user PATH", EffectivePathIncludesPersistedPath),
    ("winget table parsing is useful", WingetParsing),
    ("winget install command is safe and exact", WingetCommand),
    ("7-Zip common path detection is not missing", SevenZipCommonPathDetection),
    ("installed outside PATH is NotOnPath", InstalledOutsidePath),
    ("optional heavy Android Studio stays optional until Android goal", AndroidOptionalDefault),
    ("Android goal promotes Android toolchain", AndroidGoalPromotesToolchain),
    ("Linux goal recommends WSL and Docker", LinuxGoalRecommendsCrossPlatformTools),
    ("update target selection only includes outdated tools", UpdateTargetSelection),
    ("tool action visibility is status-specific", ToolActionVisibility),
    ("help popup formatting is clean", HelpPopupFormatting),
    ("central app state refreshes dashboard counts", AppStateRefreshesDashboardCounts),
    ("runtime paths default to AppData unless portable", RuntimePathsDefaultToAppData),
    ("catalog falls back to embedded resource", CatalogEmbeddedFallback),
    ("The Pantry contract self-check passes", DevKitContractSelfCheckPasses),
    ("GUI launchers do not use legacy dashboard", GuiLaunchersUseNativeExe),
    ("The Pantry branding and icon are packaged", ThePantryBranding),
    ("release build script is present", ReleaseBuildScriptExists),
    ("report writer emits required files", ReportWriterEmitsFiles),
    ("dashboard summary computes readiness", DashboardSummary)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        await test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static async Task<ToolCatalog> LoadCatalog()
{
    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    return await new CatalogService().LoadAsync(Path.Combine(root, "tool_catalog.json"));
}

static async Task CatalogParses()
{
    var catalog = await LoadCatalog();
    Assert(catalog.Tools.Count > 30, "catalog should contain a practical tool set");
    var ids = catalog.Tools.Select(x => x.ToolId).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var id in catalog.BestDevStack)
    {
        Assert(ids.Contains(id), $"Best Dev Stack references missing tool {id}");
    }

    Assert(!catalog.BestDevStack.Contains("android-studio"), "Best Dev Stack should exclude Android Studio");
    Assert(!catalog.BestDevStack.Contains("docker-desktop"), "Best Dev Stack should exclude Docker Desktop");
    Assert(!catalog.BestDevStack.Contains("wsl2"), "Best Dev Stack should exclude WSL2");
    Assert(catalog.Tools.First(x => x.ToolId == "7zip").DisplayName == "7-Zip File Archiver", "friendly 7-Zip name expected");
}

static async Task CatalogSchemaValidation()
{
    var catalog = await LoadCatalog();
    var valid = CatalogValidator.Validate(catalog);
    Assert(valid.IsValid, string.Join("; ", valid.Errors));

    var invalid = new ToolCatalog
    {
        SchemaVersion = "99",
        SourceNotes = ["test"],
        BestDevStack = ["missing-tool"],
        Tools =
        [
            new ToolDefinition
            {
                ToolId = "Invalid ID",
                DisplayName = "Invalid",
                Category = "Test",
                Description = "Invalid test fixture.",
                WhyItMatters = "Exercises schema failures.",
                UsedFor = ["testing"],
                InstallMethod = "unknown",
                InstallTier = "Wrong",
                ImportanceScore = 101,
                GoalTags = ["test"]
            }
        ]
    };
    var result = CatalogValidator.Validate(invalid);
    Assert(!result.IsValid, "invalid catalog should fail validation");
    Assert(result.Errors.Count >= 7, "invalid catalog should report all useful failures");

    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    using var schema = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "schemas", "tool-catalog-v2.schema.json")));
    Assert(schema.RootElement.GetProperty("$schema").GetString() == "https://json-schema.org/draft/2020-12/schema", "catalog schema must use JSON Schema 2020-12");
    Assert(schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString() == CatalogValidator.SupportedSchemaVersion, "schema document and runtime validator version must match");
}

static async Task CatalogCompatibilityBoundary()
{
    var native = await LoadCatalog();
    var nativeIds = native.Tools.Select(x => x.ToolId).ToList();
    Assert(nativeIds.Count == nativeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "native catalog IDs must be unique");

    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    using var legacyDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "tool-catalog.json")));
    var legacyIds = legacyDocument.RootElement.GetProperty("tools")
        .EnumerateArray()
        .Select(x => x.GetProperty("key").GetString() ?? "")
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();
    Assert(legacyIds.Count == legacyIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "legacy catalog IDs must be unique");

    var legacySet = legacyIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var nativeOnly = nativeIds.Where(x => !legacySet.Contains(x)).Order(StringComparer.OrdinalIgnoreCase).ToList();
    var expectedNativeOnly = new[]
    {
        "adb",
        "android-sdk",
        "android-studio",
        "dotnet-format",
        "nodejs-lts",
        "py-launcher",
        "python3",
        "wsl2"
    };
    Assert(nativeOnly.SequenceEqual(expectedNativeOnly, StringComparer.OrdinalIgnoreCase),
        "native-only catalog boundary changed; update both CATALOGS.md and this contract test intentionally");
    Assert(nativeIds.Count(x => legacySet.Contains(x)) >= 40, "catalog overlap unexpectedly dropped below 40 IDs");
}

static Task VersionParsing()
{
    Assert(VersionTools.ExtractVersion("git version 2.51.0.windows.1") == "2.51.0", "git version parsed");
    Assert(VersionTools.ExtractVersion(".NET SDK:\n Version: 10.0.300") == "10.0.300", "dotnet --info version parsed");
    Assert(VersionTools.CompareLoose("1.2.3", "1.2.4") < 0, "loose version comparison detects older");
    Assert(VersionTools.CompareLoose("10.0.300", "9.0.100") > 0, "loose version comparison detects newer");
    return Task.CompletedTask;
}

static Task PathAppendAvoidsDuplicates()
{
    var original = @"C:\Tools;C:\Windows";
    var updated = PathTools.AddPathEntry(original, @"C:\Tools\");
    Assert(updated.Split(';').Length == 2, "duplicate path entry should not be added");
    updated = PathTools.AddPathEntry(updated, @"C:\NewTool");
    Assert(PathTools.PathContains(updated, @"C:\NewTool"), "new path should be present");
    return Task.CompletedTask;
}

static async Task PathRepairServiceAppendsOnce()
{
    var temp = Directory.CreateTempSubdirectory("devtools-path-repair-test");
    var fakeExe = Path.Combine(temp.FullName, "fake-tool.exe");
    await File.WriteAllTextAsync(fakeExe, "");
    var paths = new FakeEnvironmentPathStore();
    try
    {
        var service = new PathRepairService(environmentPaths: paths);
        var request = new PathRepairRequest
        {
            ToolId = "fake-tool",
            DisplayName = "Fake Tool",
            DetectedExecutablePath = fakeExe
        };
        var first = await service.FixUserPathAsync(request);
        var second = await service.FixUserPathAsync(request);
        var userPath = paths.GetPath(EnvironmentVariableTarget.User);
        var count = PathTools.SplitPath(userPath).Count(x => PathTools.NormalizePath(x) == PathTools.NormalizePath(temp.FullName));
        Assert(first.Success, first.Message);
        Assert(!second.Changed, "second path repair should not append a duplicate");
        Assert(count == 1, $"expected one PATH entry for temp tool, found {count}");
    }
    finally
    {
        temp.Delete(recursive: true);
    }
}

static Task EffectivePathIncludesPersistedPath()
{
    var originalUserPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
    var originalProcessPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process) ?? "";
    var temp = Directory.CreateTempSubdirectory("devtools-effective-path-test");
    var fakeExe = Path.Combine(temp.FullName, "effective-tool.exe");
    File.WriteAllText(fakeExe, "");
    try
    {
        Environment.SetEnvironmentVariable("Path", temp.FullName, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("Path", "", EnvironmentVariableTarget.Process);
        Assert(PathTools.FindOnPath("effective-tool.exe") == fakeExe, "FindOnPath should search persisted user PATH even when process PATH is stale");
        Assert(PathTools.IsOnPath(fakeExe), "IsOnPath should use effective PATH, not only inherited process PATH");
    }
    finally
    {
        Environment.SetEnvironmentVariable("Path", originalUserPath, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("Path", originalProcessPath, EnvironmentVariableTarget.Process);
        temp.Delete(recursive: true);
    }

    return Task.CompletedTask;
}

static Task WingetParsing()
{
    const string sample = """
Name                            Id                          Version    Available Source
-------------------------------------------------------------------------------------
Git                             Git.Git                     2.50.1     2.51.0    winget
Microsoft Visual Studio Code    Microsoft.VisualStudioCode  1.101.0              winget
Microsoft .NET SDK 10.0.300     Microsoft.DotNet.SDK.10     10.0.300             winget
""";
    var rows = WingetCache.ParseTable(sample);
    Assert(rows.Any(x => x.Id == "Git.Git" && x.Available == "2.51.0"), "upgrade row should parse available version");
    Assert(rows.Any(x => x.Id == "Microsoft.VisualStudioCode"), "installed row should parse package id");
    Assert(rows.Any(x => x.Name.Contains(".NET SDK") && x.Id == "Microsoft.DotNet.SDK.10"), ".NET package name should not confuse package ID parsing");
    return Task.CompletedTask;
}

static Task WingetCommand()
{
    var service = new ToolOperationService();
    var command = service.BuildInstallCommand(new ToolDefinition
    {
        ToolId = "git",
        DisplayName = "Git",
        WingetIds = ["Git.Git"]
    });
    Assert(command is not null, "winget command should be built");
    Assert(command!.FileName == "winget.exe", "winget executable expected");
    Assert(command.Arguments.Contains("--id") && command.Arguments.Contains("--exact") && command.Arguments.Contains("--disable-interactivity"), "safe winget arguments expected");
    return Task.CompletedTask;
}

static async Task SevenZipCommonPathDetection()
{
    var temp = Directory.CreateTempSubdirectory("devtools-7zip-test");
    var fake7z = Path.Combine(temp.FullName, "7z.exe");
    await File.WriteAllTextAsync(fake7z, "");
    try
    {
        var tool = FakeTool("7zip", "7-Zip File Archiver", "System Core", ["7z.exe"], [fake7z]);
        var result = await new ToolDetector(winget: new WingetCache()).ScanAsync(tool, RecommendedPlan("7zip"));
        Assert(result.Status == ToolStatus.Installed_NotOnPath || result.Status == ToolStatus.Installed_Current, $"7-Zip should not be missing, got {result.Status}");
        Assert(result.DetectedPath.Length > 0, "a detected 7-Zip path should be shown");
        Assert(result.DetectionHits.Any(x => x.Value.Equals(fake7z, StringComparison.OrdinalIgnoreCase)), "common-path 7-Zip hit should be recorded even if PATH finds a real install first");
    }
    finally
    {
        temp.Delete(recursive: true);
    }
}

static async Task InstalledOutsidePath()
{
    var temp = Directory.CreateTempSubdirectory("devtools-path-test");
    var fakeExe = Path.Combine(temp.FullName, "tool.exe");
    await File.WriteAllTextAsync(fakeExe, "");
    try
    {
        var tool = FakeTool("fake-tool", "Fake Tool", "System Core", ["tool.exe"], [fakeExe]);
        var result = await new ToolDetector(winget: new WingetCache()).ScanAsync(tool, RecommendedPlan("fake-tool"));
        Assert(result.Status == ToolStatus.Installed_NotOnPath, $"installed outside PATH should be NotOnPath, got {result.Status}");
    }
    finally
    {
        temp.Delete(recursive: true);
    }
}

static async Task AndroidOptionalDefault()
{
    var catalog = await LoadCatalog();
    var android = CloneForOfflineDetection(catalog.Tools.First(x => x.ToolId == "android-studio"));
    var planner = new GoalPlanner();
    var plan = planner.BuildPlan(catalog, new WizardSelection { GoalProfileId = "ai_codex_ready" });
    var result = await new ToolDetector(winget: new WingetCache()).ScanAsync(android, new DetectionOptions { Plan = plan });
    Assert(result.Status == ToolStatus.Missing_Optional, $"Android Studio should be optional by default, got {result.Status}");
}

static async Task AndroidGoalPromotesToolchain()
{
    var catalog = await LoadCatalog();
    var plan = new GoalPlanner().BuildPlan(catalog, new WizardSelection { GoalProfileId = "android" });
    Assert(plan.RequiredTools.Contains("android-studio"), "Android Studio should be required for Android goal");
    Assert(plan.RequiredTools.Contains("java-jdk"), "JDK should be required for Android goal");
    Assert(plan.RequiredTools.Contains("android-sdk"), "Android SDK should be required for Android goal");
}

static async Task LinuxGoalRecommendsCrossPlatformTools()
{
    var catalog = await LoadCatalog();
    var plan = new GoalPlanner().BuildPlan(catalog, new WizardSelection { GoalProfileId = "linux_crossplatform" });
    Assert(plan.RecommendedTools.Contains("wsl2"), "Linux goal should recommend WSL2");
    Assert(plan.RecommendedTools.Contains("docker-desktop"), "Linux goal should recommend Docker Desktop");
    Assert(plan.RecommendedTools.Contains("windows-terminal"), "Linux goal should recommend Windows Terminal");
}

static Task UpdateTargetSelection()
{
    var updateTool = new ToolDefinition { ToolId = "git", DisplayName = "Git", WingetIds = ["Git.Git"] };
    var currentTool = new ToolDefinition { ToolId = "python3", DisplayName = "Python 3", WingetIds = ["Python.Python.3.14"] };
    var service = new ToolOperationService();
    var targets = service.GetUpdateTargets([
        new ToolScanResult { ToolId = "git", DisplayName = "Git", Status = ToolStatus.Installed_Outdated, Tool = updateTool },
        new ToolScanResult { ToolId = "python3", DisplayName = "Python 3", Status = ToolStatus.Installed_Current, Tool = currentTool }
    ]);
    Assert(targets.Count == 1 && targets[0].ToolId == "git", "only outdated update-capable tools should be targeted");
    return Task.CompletedTask;
}

static Task ToolActionVisibility()
{
    var tool = new ToolDefinition { ToolId = "7zip", DisplayName = "7-Zip File Archiver" };
    var notOnPath = new ToolScanResult { ToolId = "7zip", DisplayName = "7-Zip File Archiver", Tool = tool, Status = ToolStatus.Installed_NotOnPath, DetectedPath = @"C:\Program Files\7-Zip\7z.exe" };
    var broken = new ToolScanResult { ToolId = "git", DisplayName = "Git", Tool = tool, Status = ToolStatus.Broken };
    var current = new ToolScanResult { ToolId = "node", DisplayName = "Node.js LTS", Tool = tool, Status = ToolStatus.Installed_Current };
    Assert(notOnPath.CanFixPath && !notOnPath.CanInstall && !notOnPath.CanUpdate, "NotOnPath should expose Fix PATH only among install/update/path actions");
    Assert(broken.CanRepair && !broken.CanFixPath, "Broken should expose Repair");
    Assert(!current.CanInstall && !current.CanUpdate && !current.CanRepair && !current.CanFixPath, "Current tools should not expose mutating row actions");
    return Task.CompletedTask;
}

static Task HelpPopupFormatting()
{
    var tool = new ToolDefinition
    {
        ToolId = "windows-terminal",
        DisplayName = "Windows Terminal",
        Category = "System Core",
        Description = "Modern terminal host for shells and command-line tools.",
        WhyItMatters = "Keeps PowerShell, WSL, and command prompts usable in one reliable window.",
        InstallMethod = "winget",
        WingetIds = ["Microsoft.WindowsTerminal"],
        GoalTags = ["windows_cli_scripting", "linux_crossplatform"],
        Detection = new DetectionDefinition
        {
            VersionCommands =
            [
                new VersionCommandDefinition { Executable = "wt.exe", Arguments = ["--version"] }
            ]
        }
    };
    var result = new ToolScanResult
    {
        ToolId = "windows-terminal",
        DisplayName = "Windows Terminal",
        Category = "System Core",
        Status = ToolStatus.Installed_Current,
        Version = "1.24.11321.0",
        DetectedPath = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal\wt.exe",
        DetectionSource = "PATH",
        DetectionSummary = "Detected by executable lookup.",
        Tool = tool
    };

    var model = ToolInfoDialogViewModel.FromTool(result);
    Assert(model.DisplayName == "Windows Terminal", "dialog title should be the friendly tool name only");
    Assert(model.Subtitle == "Installed • Version 1.24.11321.0", $"unexpected subtitle: {model.Subtitle}");
    Assert(model.Version == "1.24.11321.0", "version should be a separate field");
    Assert(!model.AllVisibleText.Contains("help windows terminal 1.24.11321.0", StringComparison.OrdinalIgnoreCase), "raw help/debug text must not be visible");
    Assert(!model.DisplayName.Contains("help", StringComparison.OrdinalIgnoreCase), "action names must not be in the title");
    return Task.CompletedTask;
}

static Task AppStateRefreshesDashboardCounts()
{
    var service = new AppStateService();
    var scan = new ScanService();
    var tool = new ToolScanResult
    {
        ToolId = "nodejs",
        DisplayName = "Node.js LTS",
        Status = ToolStatus.Missing_Recommended,
        IsRecommendedForGoal = true,
        Diagnostic = "Missing"
    };
    var first = new ScanSnapshot
    {
        Tools = [tool],
        Summary = scan.BuildSummary([tool]),
        Issues = ["Node.js LTS: Missing"]
    };
    service.ApplySnapshot(first, new InstallPlan { RecommendedTools = ["nodejs"] });
    Assert(service.LastScan.Summary.MissingCritical == 1, "missing count should be visible after first scan");
    Assert(service.Issues.Count == 1, "issue should be visible after first scan");

    var installed = new ToolScanResult
    {
        ToolId = "nodejs",
        DisplayName = "Node.js LTS",
        Status = ToolStatus.Installed_Current,
        IsRecommendedForGoal = true,
        Version = "22.0.0"
    };
    var second = new ScanSnapshot
    {
        Tools = [installed],
        Summary = scan.BuildSummary([installed]),
        Issues = []
    };
    service.ApplySnapshot(second, new InstallPlan { RecommendedTools = ["nodejs"] });
    Assert(service.LastScan.Summary.MissingCritical == 0, "dashboard missing count should update after fresh scan");
    Assert(service.LastScan.Summary.InstalledCurrent == 1, "installed count should update after fresh scan");
    Assert(service.Issues.Count == 0, "resolved issue should disappear after fresh scan");
    Assert(service.DetectedTools[0].Status == ToolStatus.Installed_Current, "tool row should use the newest result");
    return Task.CompletedTask;
}

static Task RuntimePathsDefaultToAppData()
{
    var temp = Directory.CreateTempSubdirectory("devkit-runtime-paths");
    try
    {
        var paths = DevKitRuntimePaths.Resolve(temp.FullName);
        Assert(!paths.IsPortable, "runtime should default to AppData without a portable marker");
        Assert(paths.ConfigPath.Contains(Path.Combine("AppData", "Roaming", "ThePantry"), StringComparison.OrdinalIgnoreCase), "config should default to The Pantry AppData");

        File.WriteAllText(Path.Combine(temp.FullName, "config.json"), "{}");
        var portable = DevKitRuntimePaths.Resolve(temp.FullName);
        Assert(portable.IsPortable, "config.json beside the EXE should enable portable mode");
        Assert(portable.ConfigPath.StartsWith(temp.FullName, StringComparison.OrdinalIgnoreCase), "portable config should stay beside the EXE");
    }
    finally
    {
        temp.Delete(recursive: true);
    }

    return Task.CompletedTask;
}

static async Task CatalogEmbeddedFallback()
{
    var missingPath = Path.Combine(Path.GetTempPath(), "definitely-missing-tool-catalog-" + Guid.NewGuid() + ".json");
    var service = new CatalogService();
    var missingResult = await service.LoadWithFallbackAsync(missingPath);
    Assert(missingResult.UsedEmbeddedFallback, "missing external catalog should use embedded fallback");
    Assert(missingResult.Catalog.Tools.Count > 30, "embedded fallback catalog should be useful");

    var invalidPath = Path.Combine(Path.GetTempPath(), "invalid-tool-catalog-" + Guid.NewGuid() + ".json");
    try
    {
        await File.WriteAllTextAsync(invalidPath, """{"schema_version":"99","source_notes":[],"best_dev_stack":[],"tools":[{}]}""");
        var invalidResult = await service.LoadWithFallbackAsync(invalidPath);
        Assert(invalidResult.UsedEmbeddedFallback, "schema-invalid external catalog should use embedded fallback");
        Assert(invalidResult.Warnings.Any(warning => warning.Contains("failed schema validation", StringComparison.OrdinalIgnoreCase)), "schema-invalid fallback should explain the validation failure");
    }
    finally
    {
        File.Delete(invalidPath);
    }
}

static async Task DevKitContractSelfCheckPasses()
{
    var catalog = await LoadCatalog();
    var paths = DevKitRuntimePaths.Resolve(AppContext.BaseDirectory);
    var report = DevKitContractSelfCheck.Run(paths, catalog);
    Assert(report.Passed, "contract self-check should pass: " + string.Join("; ", report.Errors));
    Assert(report.PopupTitle == "Windows Terminal", "contract should lock popup title");
    Assert(report.PopupSubtitle == "Installed • Version 1.24.11321.0", "contract should lock popup subtitle");
}

static Task GuiLaunchersUseNativeExe()
{
    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    var setup = File.ReadAllText(Path.Combine(root, "setup-devtools.ps1"));
    var legacy = File.ReadAllText(Path.Combine(root, "gui", "DevToolsDashboard.ps1"));
    Assert(setup.Contains("release\\ThePantry\\ThePantry.exe", StringComparison.OrdinalIgnoreCase), "setup -Gui should launch the release The Pantry EXE first");
    Assert(!setup.Contains("DevToolsDashboard.ps1", StringComparison.OrdinalIgnoreCase), "setup -Gui must not fall back to the legacy dashboard");
    Assert(legacy.Contains("legacy PowerShell dashboard is retired", StringComparison.OrdinalIgnoreCase), "legacy dashboard should be retired");
    Assert(legacy.Contains("Start-Process -FilePath $releaseExe", StringComparison.OrdinalIgnoreCase), "legacy dashboard should redirect to the native EXE");
    return Task.CompletedTask;
}

static Task ThePantryBranding()
{
    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    var icon = Path.Combine(root, "assets", "the-pantry.ico");
    var sourceIcon = Path.Combine(root, "assets", "the-pantry-icon.png");
    var project = File.ReadAllText(Path.Combine(root, "src", "DevToolsCurator.App", "DevToolsCurator.App.csproj"));
    var mainWindow = File.ReadAllText(Path.Combine(root, "src", "DevToolsCurator.App", "MainWindow.xaml"));

    Assert(File.Exists(icon) && new FileInfo(icon).Length > 0, "Windows The Pantry icon should exist");
    Assert(File.Exists(sourceIcon) && new FileInfo(sourceIcon).Length > 0, "source The Pantry icon should exist");
    Assert(project.Contains("<AssemblyName>ThePantry</AssemblyName>", StringComparison.Ordinal), "app assembly should be ThePantry");
    Assert(project.Contains("<ApplicationIcon>..\\..\\assets\\the-pantry.ico</ApplicationIcon>", StringComparison.Ordinal), "app should embed The Pantry Windows icon");
    Assert(mainWindow.Contains("Title=\"The Pantry\"", StringComparison.Ordinal), "main window should use The Pantry title");
    Assert(mainWindow.Contains("Icon=\"/Assets/the-pantry-icon.png\"", StringComparison.Ordinal), "main window should use The Pantry icon");
    return Task.CompletedTask;
}

static Task ReleaseBuildScriptExists()
{
    var root = CatalogService.FindProjectRoot(AppContext.BaseDirectory);
    var script = Path.Combine(root, "build-release.ps1");
    Assert(File.Exists(script), "build-release.ps1 should exist");
    var text = File.ReadAllText(script);
    Assert(text.Contains("PublishSingleFile=true", StringComparison.Ordinal), "release script should publish a single-file EXE");
    Assert(text.Contains("release\\ThePantry", StringComparison.Ordinal), "release script should target release\\ThePantry");
    return Task.CompletedTask;
}

static async Task ReportWriterEmitsFiles()
{
    var temp = Directory.CreateTempSubdirectory("devtools-report-test");
    try
    {
        var snapshot = new ScanSnapshot
        {
            Tools =
            [
                new ToolScanResult { ToolId = "git", DisplayName = "Git", Category = "System Core", Status = ToolStatus.Installed_Current, Version = "2.51.0" },
                new ToolScanResult { ToolId = "gh", DisplayName = "GitHub CLI", Category = "GitHub Workflow", Status = ToolStatus.AuthNeeded, Diagnostic = "Run gh auth login" }
            ],
            Summary = new DashboardSummary { ReadinessScore = 50, TotalTools = 2, InstalledCurrent = 1, AuthNeeded = 1, RecommendedNextAction = "Run gh auth login" },
            Issues = ["GitHub CLI: Run gh auth login"],
            RepairSuggestions = ["Run gh auth login"]
        };
        await new ReportWriter(temp.FullName).WriteAsync(snapshot, new InstallPlan { GoalProfileName = "Test" });
        var reportDir = Path.Combine(temp.FullName, "devtools_setup_report");
        Assert(File.Exists(Path.Combine(reportDir, "summary.md")), "summary.md should exist");
        Assert(File.Exists(Path.Combine(reportDir, "tools.csv")), "tools.csv should exist");
        Assert(File.Exists(Path.Combine(reportDir, "issues.json")), "issues.json should exist");
        Assert(File.Exists(Path.Combine(reportDir, "install_plan.json")), "install_plan.json should exist");
        Assert(File.Exists(Path.Combine(reportDir, "last_scan.json")), "last_scan.json should exist");
    }
    finally
    {
        temp.Delete(recursive: true);
    }
}

static Task DashboardSummary()
{
    var summary = new ScanService().BuildSummary([
        new ToolScanResult { ToolId = "git", DisplayName = "Git", Status = ToolStatus.Installed_Current, IsRecommendedForGoal = true },
        new ToolScanResult { ToolId = "node", DisplayName = "Node", Status = ToolStatus.Missing_Recommended, IsRecommendedForGoal = true },
        new ToolScanResult { ToolId = "android", DisplayName = "Android Studio", Status = ToolStatus.Missing_Optional, IsOptional = true }
    ]);
    Assert(summary.TotalTools == 3, "total count should include all tools");
    Assert(summary.MissingCritical == 1, "missing critical count should include selected recommended tools");
    Assert(summary.ReadinessScore == 50, $"readiness should ignore optional missing tools, got {summary.ReadinessScore}");
    return Task.CompletedTask;
}

static DetectionOptions RecommendedPlan(string toolId)
{
    return new DetectionOptions
    {
        Plan = new InstallPlan { RecommendedTools = [toolId] }
    };
}

static ToolDefinition FakeTool(string id, string name, string category, List<string> executables, List<string> commonPaths)
{
    return new ToolDefinition
    {
        ToolId = id,
        DisplayName = name,
        Category = category,
        Description = "Fake test tool",
        WhyItMatters = "Used by tests",
        InstallTier = "Recommended",
        Detection = new DetectionDefinition
        {
            Executables = executables,
            CommonPaths = commonPaths
        }
    };
}

static ToolDefinition CloneForOfflineDetection(ToolDefinition tool)
{
    return new ToolDefinition
    {
        ToolId = tool.ToolId,
        DisplayName = tool.DisplayName,
        Category = tool.Category,
        Description = tool.Description,
        WhyItMatters = tool.WhyItMatters,
        InstallTier = tool.InstallTier,
        IsHeavy = tool.IsHeavy,
        Detection = new DetectionDefinition()
    };
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FakeEnvironmentPathStore : IEnvironmentPathStore
{
    private readonly Dictionary<EnvironmentVariableTarget, string> _paths = new()
    {
        [EnvironmentVariableTarget.Machine] = @"C:\Windows",
        [EnvironmentVariableTarget.User] = "",
        [EnvironmentVariableTarget.Process] = @"C:\Windows"
    };

    public string GetPath(EnvironmentVariableTarget target) => _paths.GetValueOrDefault(target, "");

    public void SetPath(string value, EnvironmentVariableTarget target) => _paths[target] = value;
}
