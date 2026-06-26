using CommunityToolkit.Mvvm.ComponentModel;
using Pantry.Domain;

namespace Pantry.UI.ViewModels;

public sealed class AppSelectionViewModel : ObservableObject
{
    private bool _isSelected;

    public AppSelectionViewModel(Recipe recipe, bool isSelected)
    {
        Recipe = recipe;
        _isSelected = isSelected;
    }

    public Recipe Recipe { get; }

    public string AppId => Recipe.Id;

    public string Name => Recipe.Catalog.Name;

    public string Description => Recipe.Catalog.ShortDescription;

    public string Category => Recipe.Catalog.Category;

    public string Provider => Recipe.PreferredProvider.ToString();

    public string Trust => Recipe.TrustLevel.ToString();

    public bool IsPortable => Recipe.Catalog.IsPortable;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

