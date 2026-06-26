using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pantry.Catalog;
using Pantry.Core;
using Pantry.Domain;
using Pantry.UI.ViewModels;

namespace Pantry.UI;

public sealed class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ComboBox _profilePicker = new();
    private readonly StackPanel _catalogPanel = new();
    private readonly StackPanel _reviewPanel = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _portableDestination = new();

    public MainWindow()
    {
        Title = "The Pantry";
        _viewModel = new MainViewModel(new BundledCatalogLoader(new RecipeValidator()), new DryRunPlanner());
        Content = BuildLayout();

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Status))
            {
                _status.Text = _viewModel.Status;
            }
        };
    }

    private FrameworkElement BuildLayout()
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 247, 247, 244)),
            Padding = new Thickness(24),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        root.Loaded += async (_, _) => await LoadAsync().ConfigureAwait(true);

        var header = new StackPanel
        {
            Spacing = 12
        };

        header.Children.Add(new TextBlock
        {
            Text = "The Pantry",
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.Black)
        });

        header.Children.Add(new TextBlock
        {
            Text = "Read-only catalog, profile selection, and dry-run review. No installs run in this slice.",
            FontSize = 14,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
        });

        var controls = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(280) },
                new ColumnDefinition { Width = new GridLength(280) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        _profilePicker.Header = "Profile";
        _profilePicker.DisplayMemberPath = nameof(Profile.Name);
        _profilePicker.SelectionChanged += async (_, _) =>
            await _viewModel.SelectProfileAsync(_profilePicker.SelectedItem as Profile).ConfigureAwait(true);
        Grid.SetColumn(_profilePicker, 0);
        controls.Children.Add(_profilePicker);

        _portableDestination.Header = "Portable destination";
        _portableDestination.Text = _viewModel.PortableDestination;
        _portableDestination.TextChanged += async (_, _) =>
        {
            _viewModel.PortableDestination = _portableDestination.Text;
            await _viewModel.RefreshPlanAsync().ConfigureAwait(true);
        };
        Grid.SetColumn(_portableDestination, 1);
        controls.Children.Add(_portableDestination);

        var refreshButton = new Button
        {
            Content = "Refresh review",
            VerticalAlignment = VerticalAlignment.Bottom
        };
        refreshButton.Click += async (_, _) => await _viewModel.RefreshPlanAsync().ConfigureAwait(true);
        Grid.SetColumn(refreshButton, 2);
        controls.Children.Add(refreshButton);

        header.Children.Add(controls);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid
        {
            ColumnSpacing = 18,
            Margin = new Thickness(0, 22, 0, 18),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }
            }
        };

        body.Children.Add(BuildCatalogColumn());
        var reviewColumn = BuildReviewColumn();
        Grid.SetColumn(reviewColumn, 1);
        body.Children.Add(reviewColumn);

        Grid.SetRow(body, 1);
        root.Children.Add(body);

        _status.FontSize = 13;
        _status.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80));
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        return root;
    }

    private FrameworkElement BuildCatalogColumn()
    {
        var scrollViewer = new ScrollViewer
        {
            Content = _catalogPanel
        };

        _catalogPanel.Spacing = 10;
        _catalogPanel.Children.Add(SectionTitle("Catalog"));

        return scrollViewer;
    }

    private FrameworkElement BuildReviewColumn()
    {
        var scrollViewer = new ScrollViewer
        {
            Content = _reviewPanel
        };

        _reviewPanel.Spacing = 10;
        _reviewPanel.Children.Add(SectionTitle("Dry-run review"));

        return scrollViewer;
    }

    private async Task LoadAsync()
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(true);
            _profilePicker.ItemsSource = _viewModel.Profiles;
            _profilePicker.SelectedItem = _viewModel.SelectedProfile;
            RenderCatalog();
            RenderReview();
            _viewModel.ReviewItems.CollectionChanged += (_, _) => RenderReview();
            _status.Text = _viewModel.Status;
        }
        catch (Exception ex)
        {
            _status.Text = $"Catalog failed to load safely: {ex.Message}";
        }
    }

    private void RenderCatalog()
    {
        _catalogPanel.Children.Clear();
        _catalogPanel.Children.Add(SectionTitle("Catalog"));

        foreach (var app in _viewModel.Apps)
        {
            var checkBox = new CheckBox
            {
                IsChecked = app.IsSelected,
                Content = BuildAppContent(app),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            checkBox.Checked += (_, _) => app.IsSelected = true;
            checkBox.Unchecked += (_, _) => app.IsSelected = false;
            _catalogPanel.Children.Add(checkBox);
        }
    }

    private void RenderReview()
    {
        _reviewPanel.Children.Clear();
        _reviewPanel.Children.Add(SectionTitle("Dry-run review"));

        foreach (var item in _viewModel.ReviewItems)
        {
            var panel = new StackPanel
            {
                Padding = new Thickness(12),
                Spacing = 4,
                Background = new SolidColorBrush(Colors.White)
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"{item.Intent}: {item.App}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 15
            });
            panel.Children.Add(new TextBlock { Text = $"Provider: {item.Provider} | Trust: {item.Trust}" });
            panel.Children.Add(new TextBlock { Text = $"Scope: {item.Scope} | Admin: {item.Admin}" });
            panel.Children.Add(new TextBlock { Text = $"Dependencies: {item.Dependencies}" });
            panel.Children.Add(new TextBlock { Text = $"Portable destination: {item.PortableDestination}" });
            panel.Children.Add(new TextBlock
            {
                Text = item.Reason,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
            });

            _reviewPanel.Children.Add(panel);
        }
    }

    private static FrameworkElement BuildAppContent(AppSelectionViewModel app)
    {
        var panel = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(0, 6, 0, 6)
        };

        panel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = app.Description,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{app.Category} | {app.Provider} | {app.Trust}",
            FontSize = 12,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
        });

        return panel;
    }

    private static TextBlock SectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }
}

