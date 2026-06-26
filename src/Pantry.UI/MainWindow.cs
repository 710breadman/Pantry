using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pantry.Domain;
using Pantry.UI.ViewModels;

namespace Pantry.UI;

public sealed class MainWindow : Window
{
    private readonly ServiceProvider _services;
    private readonly MainViewModel _viewModel;
    private readonly ComboBox _profilePicker = new();
    private readonly StackPanel _catalogPanel = new();
    private readonly StackPanel _reviewPanel = new();
    private readonly StackPanel _logsPanel = new();
    private readonly TextBlock _catalogSummary = new();
    private readonly TextBlock _selectionSummary = new();
    private readonly TextBlock _planSummary = new();
    private readonly TextBlock _detectionSummary = new();
    private readonly TextBlock _modeSummary = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _portableDestination = new();

    public MainWindow()
    {
        Title = "The Pantry";
        _services = PantryServiceProvider.Build();
        _viewModel = _services.GetRequiredService<MainViewModel>();
        Closed += (_, _) => _services.Dispose();
        Content = BuildLayout();

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Status))
            {
                _status.Text = _viewModel.Status;
            }

            if (args.PropertyName == nameof(MainViewModel.CatalogSummary))
            {
                _catalogSummary.Text = _viewModel.CatalogSummary;
            }

            if (args.PropertyName == nameof(MainViewModel.SelectionSummary))
            {
                _selectionSummary.Text = _viewModel.SelectionSummary;
            }

            if (args.PropertyName == nameof(MainViewModel.PlanSummary))
            {
                _planSummary.Text = _viewModel.PlanSummary;
            }

            if (args.PropertyName == nameof(MainViewModel.DetectionSummary))
            {
                _detectionSummary.Text = _viewModel.DetectionSummary;
            }

            if (args.PropertyName == nameof(MainViewModel.ModeSummary))
            {
                _modeSummary.Text = _viewModel.ModeSummary;
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
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        _profilePicker.Header = "Profile";
        _profilePicker.DisplayMemberPath = nameof(Profile.Name);
        _profilePicker.SelectionChanged += async (_, _) =>
        {
            await _viewModel.SelectProfileAsync(_profilePicker.SelectedItem as Profile).ConfigureAwait(true);
            RenderCatalog();
            RenderReview();
        };
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

        var scanButton = new Button
        {
            Content = "Scan installed",
            VerticalAlignment = VerticalAlignment.Bottom
        };
        scanButton.Click += async (_, _) => await _viewModel.ScanInstalledAppsAsync().ConfigureAwait(true);
        Grid.SetColumn(scanButton, 2);
        controls.Children.Add(scanButton);

        var logsButton = new Button
        {
            Content = "Refresh logs",
            VerticalAlignment = VerticalAlignment.Bottom
        };
        logsButton.Click += async (_, _) => await _viewModel.RefreshLogsAsync().ConfigureAwait(true);
        Grid.SetColumn(logsButton, 3);
        controls.Children.Add(logsButton);

        var refreshButton = new Button
        {
            Content = "Refresh review",
            VerticalAlignment = VerticalAlignment.Bottom
        };
        refreshButton.Click += async (_, _) => await _viewModel.RefreshPlanAsync().ConfigureAwait(true);
        Grid.SetColumn(refreshButton, 4);
        controls.Children.Add(refreshButton);

        header.Children.Add(controls);
        header.Children.Add(BuildSummaryBand());
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid
        {
            ColumnSpacing = 18,
            Margin = new Thickness(0, 22, 0, 18),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }
            }
        };

        body.Children.Add(BuildCatalogColumn());
        var reviewColumn = BuildReviewColumn();
        Grid.SetColumn(reviewColumn, 1);
        body.Children.Add(reviewColumn);
        var logColumn = BuildLogColumn();
        Grid.SetColumn(logColumn, 2);
        body.Children.Add(logColumn);

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

    private FrameworkElement BuildSummaryBand()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }
            }
        };

        AddSummaryCell(grid, _catalogSummary, 0);
        AddSummaryCell(grid, _selectionSummary, 1);
        AddSummaryCell(grid, _planSummary, 2);
        AddSummaryCell(grid, _detectionSummary, 3);
        AddSummaryCell(grid, _modeSummary, 4);

        return grid;
    }

    private static void AddSummaryCell(Grid grid, TextBlock textBlock, int column)
    {
        textBlock.FontSize = 13;
        textBlock.TextWrapping = TextWrapping.Wrap;
        textBlock.Padding = new Thickness(10);
        textBlock.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 28, 33, 40));
        Grid.SetColumn(textBlock, column);
        grid.Children.Add(textBlock);
    }

    private FrameworkElement BuildLogColumn()
    {
        var scrollViewer = new ScrollViewer
        {
            Content = _logsPanel
        };

        _logsPanel.Spacing = 10;
        _logsPanel.Children.Add(SectionTitle("Recent logs"));

        return scrollViewer;
    }

    private async Task LoadAsync()
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(true);
            _profilePicker.ItemsSource = _viewModel.Profiles;
            _profilePicker.SelectedItem = _viewModel.SelectedProfile;
            _portableDestination.Text = _viewModel.PortableDestination;
            RenderCatalog();
            RenderReview();
            RenderLogs();
            RenderSummary();
            _viewModel.ReviewItems.CollectionChanged += (_, _) => RenderReview();
            _viewModel.RecentLogs.CollectionChanged += (_, _) => RenderLogs();
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
            panel.Children.Add(new TextBlock { Text = $"Detection: {item.Detection}" });
            panel.Children.Add(new TextBlock { Text = $"Dependencies: {item.Dependencies}" });
            panel.Children.Add(new TextBlock { Text = $"Conflicts: {item.Conflicts}" });
            panel.Children.Add(new TextBlock { Text = $"Portable destination: {item.PortableDestination}" });
            panel.Children.Add(new TextBlock
            {
                Text = item.DetectionSummary,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.Reason,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
            });

            _reviewPanel.Children.Add(panel);
        }
    }

    private void RenderLogs()
    {
        _logsPanel.Children.Clear();
        _logsPanel.Children.Add(SectionTitle("Recent logs"));

        foreach (var item in _viewModel.RecentLogs)
        {
            var panel = new StackPanel
            {
                Padding = new Thickness(10),
                Spacing = 3,
                Background = new SolidColorBrush(Colors.White)
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"{item.Time} | {item.Category}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.Message,
                TextWrapping = TextWrapping.Wrap
            });

            if (!string.IsNullOrWhiteSpace(item.Details))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = item.Details,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 67, 72, 80))
                });
            }

            _logsPanel.Children.Add(panel);
        }
    }

    private void RenderSummary()
    {
        _catalogSummary.Text = _viewModel.CatalogSummary;
        _selectionSummary.Text = _viewModel.SelectionSummary;
        _planSummary.Text = _viewModel.PlanSummary;
        _detectionSummary.Text = _viewModel.DetectionSummary;
        _modeSummary.Text = _viewModel.ModeSummary;
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
