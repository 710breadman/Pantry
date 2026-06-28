namespace DevToolsCurator.Core;

public sealed class GoalPlanner
{
    public IReadOnlyList<GoalProfile> Profiles { get; } =
    [
        new()
        {
            Id = "windows_desktop",
            Name = "Windows Desktop Apps",
            Description = "WPF, WinUI, .NET desktop apps, installers, and Windows-native tools.",
            RequiredTools = ["dotnet-sdk", "vs-build-tools", "git", "vscode"],
            RecommendedTools = ["powershell7", "windows-terminal", "7zip", "github-cli", "nuget", "dotnet-format"],
            OptionalTools = ["visual-studio-community"]
        },
        new()
        {
            Id = "windows_cli_scripting",
            Name = "Windows CLI Tools & Scripts",
            Description = "PowerShell, Python automation, small CLIs, and repeatable local tooling.",
            RequiredTools = ["powershell7", "python3", "git", "vscode", "windows-terminal"],
            RecommendedTools = ["7zip", "ripgrep", "fd", "jq", "yq", "github-cli", "pipx", "uv"],
            OptionalTools = ["go", "rust"]
        },
        new()
        {
            Id = "python_automation",
            Name = "Python Automation",
            Description = "Scripts, bots, packaging, tests, and Windows automation.",
            RequiredTools = ["python3", "py-launcher", "pip", "pipx", "uv", "git", "vscode"],
            RecommendedTools = ["ruff", "pytest", "mypy", "pyinstaller", "rich", "typer", "pydantic", "pre-commit"],
            OptionalTools = ["nuitka", "poetry"]
        },
        new()
        {
            Id = "dotnet_csharp",
            Name = "C# / .NET Apps",
            Description = "C# apps, APIs, desktop tools, tests, and NuGet packages.",
            RequiredTools = ["dotnet-sdk", "git", "vscode", "vs-build-tools"],
            RecommendedTools = ["nuget", "dotnet-format", "powershell7", "github-cli"],
            OptionalTools = ["roslynator", "stylecop"]
        },
        new()
        {
            Id = "java",
            Name = "Java Apps",
            Description = "Java command-line apps, services, and JVM builds.",
            RequiredTools = ["java-jdk", "javac", "git", "vscode"],
            RecommendedTools = ["maven", "gradle"],
            OptionalTools = ["checkstyle", "spotbugs", "pmd"]
        },
        new()
        {
            Id = "web_typescript",
            Name = "Web & TypeScript Apps",
            Description = "Frontend apps, Node services, TypeScript tooling, and tests.",
            RequiredTools = ["nodejs-lts", "npm", "git", "vscode"],
            RecommendedTools = ["pnpm", "typescript", "eslint", "prettier", "vite", "vitest"],
            OptionalTools = ["yarn", "docker-desktop"]
        },
        new()
        {
            Id = "android",
            Name = "Android Apps",
            Description = "Android Studio, SDK, platform tools, Java, and Gradle.",
            RequiredTools = ["java-jdk", "android-studio", "android-sdk", "adb", "git"],
            RecommendedTools = ["gradle", "vscode"],
            OptionalTools = ["android-emulator"]
        },
        new()
        {
            Id = "linux_crossplatform",
            Name = "Linux & Cross-platform",
            Description = "WSL, Linux tools, containers, and cross-platform app validation.",
            RequiredTools = ["git", "vscode", "powershell7", "python3"],
            RecommendedTools = ["wsl2", "windows-terminal", "docker-desktop", "ripgrep", "fd", "jq", "yq"],
            OptionalTools = ["go", "rust", "cmake", "ninja"]
        },
        new()
        {
            Id = "ai_codex_ready",
            Name = "AI / Codex-ready Workstation",
            Description = "Broad app creation stack tuned for agentic coding, scripts, GitHub, and validation.",
            RequiredTools = ["git", "github-cli", "vscode", "python3", "dotnet-sdk", "nodejs-lts", "powershell7"],
            RecommendedTools = ["ripgrep", "fd", "jq", "yq", "7zip", "pre-commit", "ruff", "pytest", "eslint", "prettier", "dotnet-format", "pipx", "uv", "pnpm", "java-jdk"],
            OptionalTools = ["docker-desktop", "wsl2", "android-studio"]
        },
        new()
        {
            Id = "everything",
            Name = "Everything / Dev Workstation",
            Description = "A broad Windows/Linux/Android/app creation workstation, with heavy tools gated by approval.",
            RequiredTools = ["winget", "powershell7", "windows-terminal", "git", "github-cli", "git-lfs", "vscode", "dotnet-sdk", "python3", "nodejs-lts", "java-jdk", "7zip", "ripgrep", "fd", "jq", "yq"],
            RecommendedTools = ["pipx", "uv", "pnpm", "pre-commit", "ruff", "pytest", "mypy", "typescript", "eslint", "prettier", "dotnet-format", "maven", "gradle", "scc"],
            OptionalTools = ["vs-build-tools", "docker-desktop", "wsl2", "android-studio", "semgrep"]
        }
    ];

    public GoalProfile GetProfile(string id)
    {
        return Profiles.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
               Profiles.First(x => x.Id == "ai_codex_ready");
    }

    public InstallPlan BuildPlan(ToolCatalog catalog, WizardSelection selection, IEnumerable<ToolScanResult>? results = null)
    {
        var profile = GetProfile(selection.GoalProfileId);
        var toolById = catalog.Tools.ToDictionary(x => x.ToolId, StringComparer.OrdinalIgnoreCase);
        var required = profile.RequiredTools.Where(toolById.ContainsKey).ToList();
        var recommended = profile.RecommendedTools.Where(toolById.ContainsKey).ToList();
        var optional = profile.OptionalTools.Where(toolById.ContainsKey).ToList();

        ApplyLanguagePreferences(toolById, selection, required, recommended, optional);
        ApplyInstallStyle(catalog, selection, required, recommended, optional);
        GateHeavyTools(toolById, selection, required, recommended, optional);

        var selected = required.Concat(recommended).Concat(optional).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unnecessary = catalog.Tools.Select(x => x.ToolId).Where(x => !selected.Contains(x)).ToList();
        var resultById = results?.ToDictionary(x => x.ToolId, StringComparer.OrdinalIgnoreCase) ?? [];
        var actions = new List<string>();

        foreach (var id in required.Concat(recommended))
        {
            if (resultById.TryGetValue(id, out var result))
            {
                if (result.Status is ToolStatus.Missing_Recommended or ToolStatus.Broken)
                {
                    actions.Add($"Install or repair {result.DisplayName}");
                }
                else if (result.Status == ToolStatus.Installed_NotOnPath)
                {
                    actions.Add($"Fix PATH for {result.DisplayName}");
                }
                else if (result.Status == ToolStatus.Installed_Outdated)
                {
                    actions.Add($"Update {result.DisplayName}");
                }
            }
            else if (toolById.TryGetValue(id, out var tool))
            {
                actions.Add($"Install {tool.DisplayName}");
            }
        }

        return new InstallPlan
        {
            GoalProfileId = profile.Id,
            GoalProfileName = profile.Name,
            RequiredTools = required,
            RecommendedTools = recommended.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OptionalTools = optional.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            UnnecessaryTools = unnecessary,
            Actions = actions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            EstimatedDiskImpact = EstimateDiskImpact(toolById, required.Concat(recommended).Concat(optional)),
            AdminRequired = required.Concat(recommended).Concat(optional).Any(id => toolById.TryGetValue(id, out var tool) && (tool.IsHeavy || tool.InstallMethod.Contains("winget", StringComparison.OrdinalIgnoreCase))),
            RebootLikely = required.Concat(recommended).Concat(optional).Any(id => id is "vs-build-tools" or "wsl2" or "docker-desktop" or "android-studio")
        };
    }

    public bool IsRequired(string toolId, InstallPlan plan) => plan.RequiredTools.Contains(toolId, StringComparer.OrdinalIgnoreCase);
    public bool IsRecommended(string toolId, InstallPlan plan) => plan.RequiredTools.Contains(toolId, StringComparer.OrdinalIgnoreCase) || plan.RecommendedTools.Contains(toolId, StringComparer.OrdinalIgnoreCase);

    private static void ApplyLanguagePreferences(Dictionary<string, ToolDefinition> toolById, WizardSelection selection, List<string> required, List<string> recommended, List<string> optional)
    {
        foreach (var language in selection.Languages)
        {
            switch (language.ToLowerInvariant())
            {
                case "python":
                    AddMany(required, ["python3", "py-launcher", "pip", "pipx", "uv"], toolById);
                    AddMany(recommended, ["ruff", "pytest", "mypy"], toolById);
                    break;
                case "c#":
                case "csharp":
                    AddMany(required, ["dotnet-sdk"], toolById);
                    AddMany(recommended, ["dotnet-format", "nuget"], toolById);
                    break;
                case "java":
                    AddMany(required, ["java-jdk", "javac"], toolById);
                    AddMany(recommended, ["maven", "gradle"], toolById);
                    break;
                case "javascript/typescript":
                case "javascript":
                case "typescript":
                    AddMany(required, ["nodejs-lts", "npm"], toolById);
                    AddMany(recommended, ["pnpm", "typescript", "eslint", "prettier"], toolById);
                    break;
                case "kotlin":
                    AddMany(required, ["java-jdk"], toolById);
                    AddMany(recommended, ["gradle", "android-studio"], toolById);
                    break;
            }
        }
    }

    private static void ApplyInstallStyle(ToolCatalog catalog, WizardSelection selection, List<string> required, List<string> recommended, List<string> optional)
    {
        if (selection.InstallStyle == InstallStyle.Minimal)
        {
            optional.Clear();
            return;
        }

        if (selection.InstallStyle == InstallStyle.FullPowerUser)
        {
            AddMany(recommended, catalog.Tools.Where(x => !x.IsHeavy && x.InstallTier.Equals("Recommended", StringComparison.OrdinalIgnoreCase)).Select(x => x.ToolId), catalog.Tools.ToDictionary(x => x.ToolId, StringComparer.OrdinalIgnoreCase));
            AddMany(optional, catalog.Tools.Where(x => x.InstallTier.Equals("Optional", StringComparison.OrdinalIgnoreCase)).Select(x => x.ToolId), catalog.Tools.ToDictionary(x => x.ToolId, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void GateHeavyTools(Dictionary<string, ToolDefinition> toolById, WizardSelection selection, List<string> required, List<string> recommended, List<string> optional)
    {
        if (selection.GoalProfileId is "android" or "linux_crossplatform" or "windows_desktop" or "dotnet_csharp")
        {
            return;
        }

        var allowedHeavy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (selection.AllowVisualStudioBuildTools) allowedHeavy.Add("vs-build-tools");
        if (selection.AllowAndroidStudio) allowedHeavy.Add("android-studio");
        if (selection.AllowDockerDesktop) allowedHeavy.Add("docker-desktop");
        if (selection.AllowWsl2) allowedHeavy.Add("wsl2");
        if (selection.AllowVisualStudioCommunity) allowedHeavy.Add("visual-studio-community");

        foreach (var list in new[] { required, recommended })
        {
            var gated = list.Where(id => toolById.TryGetValue(id, out var tool) && tool.IsHeavy && !allowedHeavy.Contains(id)).ToList();
            foreach (var id in gated)
            {
                list.Remove(id);
                if (!optional.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    optional.Add(id);
                }
            }
        }
    }

    private static void AddMany(List<string> target, IEnumerable<string> ids, Dictionary<string, ToolDefinition> toolById)
    {
        foreach (var id in ids)
        {
            if (toolById.ContainsKey(id) && !target.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(id);
            }
        }
    }

    private static string EstimateDiskImpact(Dictionary<string, ToolDefinition> toolById, IEnumerable<string> ids)
    {
        var tools = ids.Where(id => toolById.ContainsKey(id)).Select(id => toolById[id]).ToList();
        if (tools.Any(x => x.ToolId is "android-studio" or "docker-desktop" or "wsl2"))
        {
            return "High; heavy tools or SDKs selected";
        }

        if (tools.Any(x => x.ToolId == "vs-build-tools"))
        {
            return "Moderate to high; Visual Studio Build Tools can be several GB";
        }

        return "Moderate; mostly command-line tools and SDKs";
    }
}
