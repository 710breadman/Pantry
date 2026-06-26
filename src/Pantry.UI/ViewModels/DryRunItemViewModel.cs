using Pantry.Domain;

namespace Pantry.UI.ViewModels;

public sealed class DryRunItemViewModel
{
    public DryRunItemViewModel(DryRunPlanItem item)
    {
        App = item.AppName;
        Intent = item.Intent.ToString();
        Provider = item.PreferredProvider.ToString();
        Trust = item.TrustLevel.ToString();
        Scope = item.ScopePreference.ToString();
        Admin = item.AdministratorRequirement.ToString();
        Dependencies = item.Dependencies.Count == 0 ? "None" : string.Join(", ", item.Dependencies);
        PortableDestination = item.PortableDestination ?? "Not applicable";
        Reason = item.Reason;
    }

    public string App { get; }

    public string Intent { get; }

    public string Provider { get; }

    public string Trust { get; }

    public string Scope { get; }

    public string Admin { get; }

    public string Dependencies { get; }

    public string PortableDestination { get; }

    public string Reason { get; }
}

