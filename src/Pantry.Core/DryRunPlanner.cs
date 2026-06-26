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

        var items = request.Catalog.Recipes
            .OrderBy(recipe => recipe.Catalog.Name, StringComparer.OrdinalIgnoreCase)
            .Select(recipe => CreateItem(recipe, profileSelections, request))
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
        IReadOnlyDictionary<string, Selection> profileSelections,
        DryRunPlanRequest request)
    {
        var selected = IsSelected(recipe.Id, profileSelections, request.SelectionOverrides);
        var detectedState = request.DetectedStates.TryGetValue(recipe.Id, out var state)
            ? state
            : DetectedAppState.NotInstalled;

        var intent = ResolveIntent(selected, detectedState);
        var reason = ResolveReason(selected, detectedState);

        return new DryRunPlanItem
        {
            AppId = recipe.Id,
            AppName = recipe.Catalog.Name,
            Intent = intent,
            PreferredProvider = recipe.PreferredProvider,
            TrustLevel = recipe.TrustLevel,
            ScopePreference = recipe.ScopePreference,
            AdministratorRequirement = recipe.AdministratorRequirement,
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

    private static string ResolveReason(bool selected, DetectedAppState detectedState)
    {
        if (!selected)
        {
            return "Not selected for this review.";
        }

        return detectedState switch
        {
            DetectedAppState.InstalledCurrent => "Already current according to supplied detection state.",
            DetectedAppState.UpdateAvailable => "Selected and an update is available according to supplied detection state.",
            _ => "Selected and not known to be installed in this read-only slice."
        };
    }
}

