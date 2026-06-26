using Pantry.Domain;

namespace Pantry.Core;

public sealed class DryRunPlanner
{
    public Task<DryRunPlan> CreatePlanAsync(DryRunPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var profileSelections = request.Profile.Selections
            .ToDictionary(selection => selection.AppId, StringComparer.OrdinalIgnoreCase);

        var recipesById = request.Catalog.Recipes
            .ToDictionary(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase);
        var explicitlySelected = request.Catalog.Recipes
            .ToDictionary(
                recipe => recipe.Id,
                recipe => IsSelected(recipe.Id, profileSelections, request.SelectionOverrides),
                StringComparer.OrdinalIgnoreCase);
        var requiredBy = FindRequiredDependencies(recipesById, explicitlySelected);

        var items = OrderByDependencies(request.Catalog.Recipes, recipesById)
            .Select(recipe => CreateItem(
                recipe,
                explicitlySelected[recipe.Id],
                requiredBy.TryGetValue(recipe.Id, out var dependents) ? dependents : [],
                request))
            .ToArray();

        var plan = new DryRunPlan
        {
            ProfileId = request.Profile.Id,
            ProfileName = request.Profile.Name,
            Items = items
        };

        return Task.FromResult(plan);
    }

    private static DryRunPlanItem CreateItem(
        Recipe recipe,
        bool explicitlySelected,
        IReadOnlyList<string> requiredBy,
        DryRunPlanRequest request)
    {
        var selected = explicitlySelected || requiredBy.Count > 0;
        var detection = request.DetectionResults.TryGetValue(recipe.Id, out var result)
            ? result
            : NotScanned(recipe.Id);

        var intent = ResolveIntent(selected, detection.State);
        var reason = ResolveReason(explicitlySelected, requiredBy, detection);

        return new DryRunPlanItem
        {
            AppId = recipe.Id,
            AppName = recipe.Catalog.Name,
            Intent = intent,
            PreferredProvider = recipe.PreferredProvider,
            TrustLevel = recipe.TrustLevel,
            ScopePreference = recipe.ScopePreference,
            AdministratorRequirement = recipe.AdministratorRequirement,
            DetectionState = detection.State,
            DetectionConfidence = detection.Confidence,
            DetectionSummary = detection.Summary,
            Dependencies = recipe.Dependencies,
            PortableDestination = recipe.Catalog.IsPortable
                ? request.PortableDestination ?? recipe.PortableDestinationHint
                : null,
            Reason = reason
        };
    }

    private static bool IsSelected(
        string appId,
        IReadOnlyDictionary<string, Selection> profileSelections,
        IReadOnlyDictionary<string, AppSelectionState> selectionOverrides)
    {
        if (selectionOverrides.TryGetValue(appId, out var overrideState))
        {
            return overrideState == AppSelectionState.Selected;
        }

        return profileSelections.TryGetValue(appId, out var selection) && selection.Preselected;
    }

    private static DryRunIntent ResolveIntent(bool selected, DetectedAppState detectedState)
    {
        if (!selected)
        {
            return DryRunIntent.Skip;
        }

        return detectedState switch
        {
            DetectedAppState.InstalledCurrent => DryRunIntent.Skip,
            DetectedAppState.UpdateAvailable => DryRunIntent.Update,
            _ => DryRunIntent.Install
        };
    }

    private static string ResolveReason(bool selected, AppDetectionResult detection)
    {
        return ResolveReason(selected, [], detection);
    }

    private static string ResolveReason(
        bool explicitlySelected,
        IReadOnlyList<string> requiredBy,
        AppDetectionResult detection)
    {
        if (!explicitlySelected && requiredBy.Count > 0)
        {
            return $"Required by selected app(s): {string.Join(", ", requiredBy)}.";
        }

        var selected = explicitlySelected || requiredBy.Count > 0;
        if (!selected)
        {
            return "Not selected for this review.";
        }

        return detection.State switch
        {
            DetectedAppState.InstalledCurrent => "Already current according to read-only detection.",
            DetectedAppState.UpdateAvailable => "Selected and an update is available according to read-only detection.",
            DetectedAppState.NotInstalled => "Selected and not found by read-only detection.",
            _ => $"Selected, but detection is unknown: {detection.Summary}"
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> FindRequiredDependencies(
        IReadOnlyDictionary<string, Recipe> recipesById,
        IReadOnlyDictionary<string, bool> explicitlySelected)
    {
        var requiredBy = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selected in explicitlySelected.Where(selection => selection.Value))
        {
            MarkDependencies(selected.Key, selected.Key);
        }

        return requiredBy.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        void MarkDependencies(string appId, string rootSelectedAppId)
        {
            if (!recipesById.TryGetValue(appId, out var recipe))
            {
                return;
            }

            var visitKey = $"{rootSelectedAppId}\0{appId}";
            if (!visited.Add(visitKey))
            {
                return;
            }

            foreach (var dependencyId in recipe.Dependencies)
            {
                if (!recipesById.ContainsKey(dependencyId))
                {
                    continue;
                }

                if (!requiredBy.TryGetValue(dependencyId, out var dependents))
                {
                    dependents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    requiredBy[dependencyId] = dependents;
                }

                dependents.Add(rootSelectedAppId);
                MarkDependencies(dependencyId, rootSelectedAppId);
            }
        }
    }

    private static IReadOnlyList<Recipe> OrderByDependencies(
        IReadOnlyList<Recipe> recipes,
        IReadOnlyDictionary<string, Recipe> recipesById)
    {
        var ordered = new List<Recipe>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipe in recipes.OrderBy(recipe => recipe.Catalog.Name, StringComparer.OrdinalIgnoreCase))
        {
            Visit(recipe);
        }

        return ordered;

        void Visit(Recipe recipe)
        {
            if (visited.Contains(recipe.Id))
            {
                return;
            }

            if (!visiting.Add(recipe.Id))
            {
                return;
            }

            foreach (var dependencyId in recipe.Dependencies.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (recipesById.TryGetValue(dependencyId, out var dependency))
                {
                    Visit(dependency);
                }
            }

            visiting.Remove(recipe.Id);
            if (visited.Add(recipe.Id))
            {
                ordered.Add(recipe);
            }
        }
    }

    private static AppDetectionResult NotScanned(string appId)
    {
        return new AppDetectionResult
        {
            AppId = appId,
            State = DetectedAppState.Unknown,
            Confidence = DetectionConfidence.Unknown,
            Evidence = [],
            Summary = "Detection has not been run."
        };
    }
}
