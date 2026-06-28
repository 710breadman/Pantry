[CmdletBinding()]
param(
  [switch]$SmokeTest,
  [switch]$NoAutoScan
)

$ErrorActionPreference = "Stop"
$script:GuiRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$script:ProjectRoot = Split-Path -Parent $script:GuiRoot
$script:BackendScript = Join-Path $script:ProjectRoot "setup-devtools.ps1"
$script:CatalogPath = Join-Path $script:ProjectRoot "tool-catalog.json"
$script:ReportDir = Join-Path $script:ProjectRoot "devtools_setup_report"
$script:DashboardTracePath = Join-Path $script:ReportDir "gui_dashboard.log"
$script:GuiModelModule = Join-Path $script:GuiRoot "DevTools.GuiModel.psm1"
$script:CurrentProcess = $null
$script:CurrentOperation = ""
$script:CurrentLogPath = ""
$script:CurrentLogLineCount = 0
$script:PostActionValidationPending = $false

if (-not $SmokeTest) {
  $releaseExe = Join-Path $script:ProjectRoot "release\DevKit\DevKit.exe"
  if (Test-Path -LiteralPath $releaseExe) {
    Start-Process -FilePath $releaseExe -WorkingDirectory (Split-Path -Parent $releaseExe)
    return
  }

  throw "The legacy PowerShell dashboard is retired. Build and run the native DevKit app with: .\build-release.ps1"
}

Import-Module $script:GuiModelModule -Force

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

function Read-JsonFile {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    return $null
  }
  return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Quote-ProcessArgument {
  param([string]$Value)
  if ($Value -match '[\s"]') {
    return '"' + ($Value -replace '"', '\"') + '"'
  }
  return $Value
}

function Quote-PowerShellLiteral {
  param([string]$Value)
  return "'" + ($Value -replace "'", "''") + "'"
}

function Write-DashboardTrace {
  param([string]$Message)
  try {
    New-Item -ItemType Directory -Path $script:ReportDir -Force | Out-Null
    Add-Content -LiteralPath $script:DashboardTracePath -Value ("[{0}] {1}" -f (Get-Date -Format "s"), $Message) -Encoding UTF8
  } catch {
  }
}

$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Windows DevTools Dashboard"
        Width="1280"
        Height="820"
        MinWidth="980"
        MinHeight="640"
        WindowStartupLocation="CenterScreen"
        Background="#0b1120"
        Foreground="#e5e7eb"
        FontFamily="Segoe UI">
  <Window.Resources>
    <Style x:Key="PanelBorder" TargetType="Border">
      <Setter Property="Background" Value="#111827"/>
      <Setter Property="BorderBrush" Value="#273449"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius" Value="8"/>
      <Setter Property="Padding" Value="12"/>
      <Setter Property="Margin" Value="0,0,10,10"/>
    </Style>
    <Style x:Key="PrimaryButton" TargetType="Button">
      <Setter Property="Background" Value="#2563eb"/>
      <Setter Property="Foreground" Value="#ffffff"/>
      <Setter Property="BorderBrush" Value="#3b82f6"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="12,7"/>
      <Setter Property="Margin" Value="0,0,8,8"/>
      <Setter Property="MinHeight" Value="34"/>
      <Setter Property="Cursor" Value="Hand"/>
    </Style>
    <Style x:Key="SecondaryButton" TargetType="Button" BasedOn="{StaticResource PrimaryButton}">
      <Setter Property="Background" Value="#1f2937"/>
      <Setter Property="BorderBrush" Value="#374151"/>
    </Style>
    <Style x:Key="SmallButton" TargetType="Button" BasedOn="{StaticResource SecondaryButton}">
      <Setter Property="Padding" Value="8,4"/>
      <Setter Property="Margin" Value="4,2"/>
      <Setter Property="MinHeight" Value="28"/>
    </Style>
    <Style TargetType="TextBox">
      <Setter Property="Background" Value="#0f172a"/>
      <Setter Property="Foreground" Value="#e5e7eb"/>
      <Setter Property="BorderBrush" Value="#334155"/>
      <Setter Property="CaretBrush" Value="#e5e7eb"/>
      <Setter Property="Padding" Value="8,6"/>
    </Style>
    <Style TargetType="ComboBox">
      <Setter Property="Background" Value="#0f172a"/>
      <Setter Property="Foreground" Value="#e5e7eb"/>
      <Setter Property="BorderBrush" Value="#334155"/>
      <Setter Property="Padding" Value="8,4"/>
    </Style>
    <Style TargetType="DataGrid">
      <Setter Property="Background" Value="#0b1120"/>
      <Setter Property="Foreground" Value="#e5e7eb"/>
      <Setter Property="RowBackground" Value="#111827"/>
      <Setter Property="AlternatingRowBackground" Value="#0f172a"/>
      <Setter Property="GridLinesVisibility" Value="Horizontal"/>
      <Setter Property="HorizontalGridLinesBrush" Value="#1f2937"/>
      <Setter Property="VerticalGridLinesBrush" Value="#1f2937"/>
      <Setter Property="HeadersVisibility" Value="Column"/>
      <Setter Property="BorderBrush" Value="#273449"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CanUserResizeRows" Value="False"/>
    </Style>
    <Style TargetType="DataGridColumnHeader">
      <Setter Property="Background" Value="#172033"/>
      <Setter Property="Foreground" Value="#cbd5e1"/>
      <Setter Property="BorderBrush" Value="#334155"/>
      <Setter Property="Padding" Value="8,7"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>
  </Window.Resources>

  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="118"/>
    </Grid.RowDefinitions>

    <DockPanel Grid.Row="0" LastChildFill="True" Margin="0,0,0,12">
      <StackPanel DockPanel.Dock="Left">
        <TextBlock Text="Windows DevTools Dashboard" FontSize="24" FontWeight="SemiBold"/>
        <TextBlock Name="SubTitleText" Text="Safe installer and validation dashboard" Foreground="#94a3b8" Margin="0,4,0,0"/>
      </StackPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" HorizontalAlignment="Right">
        <Button Name="OpenReportButton" Style="{StaticResource SecondaryButton}" Content="Open Report Folder"/>
        <Button Name="ExportSummaryButton" Style="{StaticResource SecondaryButton}" Content="Export Summary"/>
        <Button Name="CopyDiagnosticsButton" Style="{StaticResource SecondaryButton}" Content="Copy Diagnostics"/>
      </StackPanel>
    </DockPanel>

    <UniformGrid Grid.Row="1" Columns="7" Margin="0,0,0,12">
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Readiness" Foreground="#94a3b8"/>
          <TextBlock Name="ScoreText" Text="--%" FontSize="26" FontWeight="Bold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Total" Foreground="#94a3b8"/>
          <TextBlock Name="TotalText" Text="0" FontSize="24" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Installed" Foreground="#94a3b8"/>
          <TextBlock Name="InstalledText" Text="0" FontSize="24" Foreground="#22c55e" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Outdated" Foreground="#94a3b8"/>
          <TextBlock Name="OutdatedText" Text="0" FontSize="24" Foreground="#eab308" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Missing" Foreground="#94a3b8"/>
          <TextBlock Name="MissingText" Text="0" FontSize="24" Foreground="#ef4444" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Errors" Foreground="#94a3b8"/>
          <TextBlock Name="ErrorText" Text="0" FontSize="24" Foreground="#ef4444" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource PanelBorder}">
        <StackPanel>
          <TextBlock Text="Action Needed" Foreground="#94a3b8"/>
          <TextBlock Name="ActionText" Text="0" FontSize="24" Foreground="#f59e0b" FontWeight="SemiBold"/>
        </StackPanel>
      </Border>
    </UniformGrid>

    <Grid Grid.Row="2" Margin="0,0,0,10">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <WrapPanel Grid.Column="0">
        <Button Name="RescanButton" Style="{StaticResource PrimaryButton}" Content="Rescan"/>
        <Button Name="InstallUpdateAllButton" Style="{StaticResource PrimaryButton}" Content="Install/Update All"/>
        <Button Name="InstallMissingButton" Style="{StaticResource SecondaryButton}" Content="Install Missing"/>
        <Button Name="UpdateAllButton" Style="{StaticResource SecondaryButton}" Content="Update All Installed"/>
        <Button Name="RepairBrokenButton" Style="{StaticResource SecondaryButton}" Content="Repair Broken"/>
        <Button Name="InstallSelectedButton" Style="{StaticResource SecondaryButton}" Content="Install Selected"/>
        <Button Name="UpdateSelectedButton" Style="{StaticResource SecondaryButton}" Content="Update Selected"/>
        <Button Name="RepairSelectedButton" Style="{StaticResource SecondaryButton}" Content="Repair Selected"/>
        <Button Name="ValidateSelectedButton" Style="{StaticResource SecondaryButton}" Content="Validate Selected"/>
        <Button Name="RunStackValidationButton" Style="{StaticResource SecondaryButton}" Content="Run Stack Validation"/>
        <Button Name="SelectVisibleButton" Style="{StaticResource SecondaryButton}" Content="Select Visible"/>
        <Button Name="ClearSelectionButton" Style="{StaticResource SecondaryButton}" Content="Clear"/>
        <Button Name="CancelButton" Style="{StaticResource SecondaryButton}" Content="Cancel" IsEnabled="False"/>
      </WrapPanel>
      <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right">
        <TextBox Name="SearchBox" Width="220" Margin="0,0,8,8" ToolTip="Search tools, categories, paths, or diagnostics"/>
        <ComboBox Name="StatusFilter" Width="130" Margin="0,0,8,8"/>
        <ComboBox Name="CategoryFilter" Width="190" Margin="0,0,0,8"/>
      </StackPanel>
    </Grid>

    <DataGrid Name="ToolsGrid"
              Grid.Row="3"
              AutoGenerateColumns="False"
              CanUserSortColumns="True"
              SelectionMode="Extended"
              IsReadOnly="False"
              RowDetailsVisibilityMode="VisibleWhenSelected">
      <DataGrid.GroupStyle>
        <GroupStyle>
          <GroupStyle.ContainerStyle>
            <Style TargetType="{x:Type GroupItem}">
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="{x:Type GroupItem}">
                    <Expander IsExpanded="True" Background="#111827" Foreground="#e5e7eb" Margin="0,8,0,0" BorderBrush="#273449" BorderThickness="1">
                      <Expander.Header>
                        <DockPanel>
                          <TextBlock Text="{Binding Name}" FontSize="15" FontWeight="SemiBold" Margin="8,6"/>
                          <TextBlock Text="{Binding ItemCount, StringFormat=({0})}" Foreground="#94a3b8" Margin="0,6"/>
                        </DockPanel>
                      </Expander.Header>
                      <ItemsPresenter/>
                    </Expander>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
          </GroupStyle.ContainerStyle>
        </GroupStyle>
      </DataGrid.GroupStyle>
      <DataGrid.RowDetailsTemplate>
        <DataTemplate>
          <Border Background="#0f172a" BorderBrush="#273449" BorderThickness="1" Padding="8" Margin="24,0,8,8">
            <TextBlock Text="{Binding Diagnostic}" TextWrapping="Wrap" Foreground="#cbd5e1"/>
          </Border>
        </DataTemplate>
      </DataGrid.RowDetailsTemplate>
      <DataGrid.Columns>
        <DataGridCheckBoxColumn Header="" Binding="{Binding Selected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="42"/>
        <DataGridTemplateColumn Header="Status" Width="70" SortMemberPath="StatusKind">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
              <TextBlock Text="{Binding StatusGlyph}" Foreground="{Binding StatusBrush}" FontSize="18" FontWeight="Bold" HorizontalAlignment="Center"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
        <DataGridTextColumn Header="Tool" Binding="{Binding Name}" Width="210" IsReadOnly="True"/>
        <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="130" IsReadOnly="True"/>
        <DataGridTextColumn Header="Installed" Binding="{Binding InstalledVersion}" Width="170" IsReadOnly="True"/>
        <DataGridTextColumn Header="Latest" Binding="{Binding LatestVersion}" Width="110" IsReadOnly="True"/>
        <DataGridTextColumn Header="Path" Binding="{Binding ExecutablePath}" Width="260" IsReadOnly="True"/>
        <DataGridTextColumn Header="Method" Binding="{Binding InstallMethod}" Width="105" IsReadOnly="True"/>
        <DataGridTemplateColumn Header="Action" Width="100">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
              <Button Content="{Binding ActionLabel}" Tag="{Binding}" Style="{StaticResource SmallButton}"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
      </DataGrid.Columns>
    </DataGrid>

    <Grid Grid.Row="4" Margin="0,12,0,0">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
      </Grid.RowDefinitions>
      <DockPanel Grid.Row="0">
        <TextBlock Name="ProgressText" Text="Ready" Foreground="#cbd5e1" DockPanel.Dock="Left"/>
        <TextBlock Name="LastScanText" Text="Last scan: never" Foreground="#94a3b8" DockPanel.Dock="Right"/>
      </DockPanel>
      <Grid Grid.Row="1" Margin="0,6,0,0">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="2*"/>
          <ColumnDefinition Width="3*"/>
        </Grid.ColumnDefinitions>
        <ProgressBar Name="OverallProgress" Height="16" Grid.Column="0" Margin="0,0,12,0"/>
        <ListBox Name="EventsList" Grid.Column="1" Background="#0f172a" Foreground="#cbd5e1" BorderBrush="#273449"/>
      </Grid>
    </Grid>
  </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
$script:Window = [Windows.Markup.XamlReader]::Load($reader)

$controls = @{}
([xml]$xaml).SelectNodes("//*[@Name]") | ForEach-Object {
  $controls[$_.Name] = $script:Window.FindName($_.Name)
}

$script:ToolsRows = New-Object 'System.Collections.ObjectModel.ObservableCollection[object]'
$controls.ToolsGrid.ItemsSource = $script:ToolsRows
$script:ToolsView = [System.Windows.Data.CollectionViewSource]::GetDefaultView($controls.ToolsGrid.ItemsSource)
$script:ToolsView.GroupDescriptions.Add((New-Object System.Windows.Data.PropertyGroupDescription("Section")))

foreach ($item in @("All", "Missing", "Outdated", "Errors", "Installed", "Optional", "Selected")) {
  [void]$controls.StatusFilter.Items.Add($item)
}
$controls.StatusFilter.SelectedIndex = 0

foreach ($item in @("All", "1_System Core", "2_Python", "3_DotNet_CSharp", "4_Java", "5_Node_TypeScript", "6_GitHub_Workflow", "7_Logic_Quality_Tools", "8_Optional_Extras")) {
  [void]$controls.CategoryFilter.Items.Add($item)
}
$controls.CategoryFilter.SelectedIndex = 0

$script:ProcessPollTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:ProcessPollTimer.Interval = [TimeSpan]::FromMilliseconds(800)

function Add-EventMessage {
  param([string]$Message)
  if ([string]::IsNullOrWhiteSpace($Message)) { return }
  $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message.Trim()
  [void]$controls.EventsList.Items.Add($line)
  if ($controls.EventsList.Items.Count -gt 300) {
    $controls.EventsList.Items.RemoveAt(0)
  }
  $controls.EventsList.ScrollIntoView($line)
}

function Set-Busy {
  param(
    [bool]$Busy,
    [string]$Text
  )

  $controls.OverallProgress.IsIndeterminate = $Busy
  $controls.CancelButton.IsEnabled = $Busy
  $controls.ProgressText.Text = $Text
  foreach ($name in @("RescanButton", "InstallUpdateAllButton", "InstallMissingButton", "UpdateAllButton", "RepairBrokenButton", "InstallSelectedButton", "UpdateSelectedButton", "RepairSelectedButton", "ValidateSelectedButton", "RunStackValidationButton")) {
    $controls[$name].IsEnabled = -not $Busy
  }
}

function Test-RowVisible {
  param($Row)

  $search = $controls.SearchBox.Text
  if (-not [string]::IsNullOrWhiteSpace($search)) {
    $haystack = @($Row.Name, $Row.Key, $Row.Category, $Row.Section, $Row.Status, $Row.ExecutablePath, $Row.Diagnostic) -join " "
    if ($haystack.IndexOf($search, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
      return $false
    }
  }

  $status = [string]$controls.StatusFilter.SelectedItem
  switch ($status) {
    "Missing" { if ($Row.StatusKind -ne "Missing") { return $false } }
    "Outdated" { if ($Row.StatusKind -ne "Outdated") { return $false } }
    "Errors" { if ($Row.StatusKind -ne "Error") { return $false } }
    "Installed" { if ($Row.StatusKind -ne "Installed") { return $false } }
    "Optional" { if (-not $Row.Optional) { return $false } }
    "Selected" { if (-not $Row.Selected) { return $false } }
  }

  $category = [string]$controls.CategoryFilter.SelectedItem
  if ($category -ne "All" -and $Row.Section -ne $category) {
    return $false
  }

  return $true
}

$script:ToolsView.Filter = [Predicate[object]]{ param($item) Test-RowVisible -Row $item }

function Refresh-Dashboard {
  $stats = Get-GuiDashboardStats -Rows @($script:ToolsRows)
  $controls.ScoreText.Text = "$($stats.Score)%"
  $controls.TotalText.Text = [string]$stats.Total
  $controls.InstalledText.Text = [string]$stats.Installed
  $controls.OutdatedText.Text = [string]$stats.Outdated
  $controls.MissingText.Text = [string]$stats.Missing
  $controls.ErrorText.Text = [string]$stats.Errors
  $controls.ActionText.Text = [string]$stats.Actions
}

function Set-ToolRows {
  param([array]$Rows)

  $script:ToolsRows.Clear()
  foreach ($row in $Rows) {
    [void]$script:ToolsRows.Add($row)
  }
  $script:ToolsView.Refresh()
  Refresh-Dashboard
}

function Read-OperationLog {
  if ([string]::IsNullOrWhiteSpace($script:CurrentLogPath) -or -not (Test-Path -LiteralPath $script:CurrentLogPath)) {
    return
  }

  try {
    $lines = @(Get-Content -LiteralPath $script:CurrentLogPath -ErrorAction Stop)
  } catch {
    return
  }

  if ($lines.Count -le $script:CurrentLogLineCount) {
    return
  }

  for ($i = $script:CurrentLogLineCount; $i -lt $lines.Count; $i++) {
    $line = [string]$lines[$i]
    if (-not [string]::IsNullOrWhiteSpace($line)) {
      Add-EventMessage $line
      $controls.ProgressText.Text = $line
    }
  }
  $script:CurrentLogLineCount = $lines.Count
}

function Complete-BackendOperation {
  param([int]$ExitCode)

  $completedOperation = $script:CurrentOperation
  $postValidate = $script:PostActionValidationPending
  $script:PostActionValidationPending = $false
  Set-Busy -Busy $false -Text "Ready"
  Add-EventMessage "Completed $completedOperation with exit code $ExitCode"
  Load-RowsFromScanFile
  $controls.LastScanText.Text = "Last scan: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:CurrentProcess = $null
  $script:CurrentOperation = ""

  if ($postValidate) {
    Start-BackendOperation -Mode "Preview" -Label "Post-action validation scan"
  }
}

function Load-RowsFromScanFile {
  param([switch]$UseCacheOnly)

  $catalog = (Read-JsonFile -Path $script:CatalogPath).tools
  $scanPath = Join-Path $script:ReportDir "gui_last_scan.json"
  if (-not (Test-Path -LiteralPath $scanPath)) {
    $scanPath = Join-Path $script:ReportDir "validation_results.json"
  }
  $scan = Read-JsonFile -Path $scanPath
  $rows = ConvertTo-GuiToolRows -CatalogTools @($catalog) -Scan $scan
  Set-ToolRows -Rows $rows
  if ($null -ne $scan -and $null -ne $scan.mode) {
    $controls.LastScanText.Text = "Last scan: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  } elseif ($UseCacheOnly) {
    $controls.LastScanText.Text = "Last scan: no cache"
  }
}

function Get-SelectedKeys {
  param([string]$Action)
  return Get-GuiToolKeysForAction -Rows @($script:ToolsRows) -Action $Action
}

function Start-BackendOperation {
  param(
    [ValidateSet("Preview", "Apply", "Repair", "Update", "ValidateProjects")]
    [string]$Mode,
    [string[]]$Keys = @(),
    [string]$Label = ""
  )

  if ($null -ne $script:CurrentProcess -and -not $script:CurrentProcess.HasExited) {
    Add-EventMessage "Another operation is already running."
    Write-DashboardTrace "Skipped $Mode because another operation is running."
    return
  }

  New-Item -ItemType Directory -Path $script:ReportDir -Force | Out-Null
  Write-DashboardTrace "Start-BackendOperation mode=$Mode label=$Label keys=$($Keys -join ',')"
  $script:CurrentOperation = $Mode
  $script:PostActionValidationPending = ($Mode -in @("Apply", "Repair", "Update"))
  $script:CurrentLogPath = Join-Path $script:ReportDir ("gui_operation_{0:yyyyMMdd_HHmmss}_{1}.log" -f (Get-Date), $Mode)
  $script:CurrentLogLineCount = 0
  Remove-Item -LiteralPath $script:CurrentLogPath -Force -ErrorAction SilentlyContinue
  $displayLabel = if ([string]::IsNullOrWhiteSpace($Label)) { $Mode } else { $Label }
  Set-Busy -Busy $true -Text $displayLabel
  Add-EventMessage "Starting: $displayLabel"

  $backendArgs = @("-Unattended", "-ReportDir", $script:ReportDir)
  switch ($Mode) {
    "Preview" { $backendArgs += "-Preview" }
    "Apply" { $backendArgs += "-Apply" }
    "Repair" { $backendArgs += "-Repair" }
    "Update" { $backendArgs += "-Update" }
    "ValidateProjects" { $backendArgs += "-RunValidationProjectsOnly" }
  }
  if ($Keys.Count -gt 0) {
    $backendArgs += "-ToolKeys"
    $backendArgs += ($Keys -join ",")
  }

  $runnerPath = Join-Path $script:ReportDir ("gui_backend_runner_{0:yyyyMMdd_HHmmss}_{1}.ps1" -f (Get-Date), $Mode)
  $backendInvocation = @("&", (Quote-PowerShellLiteral $script:BackendScript))
  foreach ($arg in $backendArgs) {
    if ($arg -like "-*") {
      $backendInvocation += $arg
    } else {
      $backendInvocation += (Quote-PowerShellLiteral $arg)
    }
  }
  $runnerContent = @"
`$ProgressPreference = 'SilentlyContinue'
`$ErrorActionPreference = 'Continue'
$(($backendInvocation -join " ")) *>&1 | ForEach-Object { `$_.ToString() } | Tee-Object -FilePath $(Quote-PowerShellLiteral $script:CurrentLogPath)
if (`$null -ne `$global:LASTEXITCODE) { exit `$global:LASTEXITCODE }
exit 0
"@
  Set-Content -LiteralPath $runnerPath -Value $runnerContent -Encoding UTF8

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = "powershell.exe"
  $psi.Arguments = ((@("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $runnerPath) | ForEach-Object { Quote-ProcessArgument $_ }) -join " ")
  $psi.WorkingDirectory = $script:ProjectRoot
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $false
  $psi.RedirectStandardError = $false
  $psi.CreateNoWindow = $true

  $process = New-Object System.Diagnostics.Process
  $process.StartInfo = $psi
  $process.EnableRaisingEvents = $false
  $script:CurrentProcess = $process

  Write-DashboardTrace "Backend runner: $runnerPath"
  Write-DashboardTrace "Backend command: powershell.exe $($psi.Arguments)"
  [void]$process.Start()
  Write-DashboardTrace "Backend process started: $($process.Id), log=$script:CurrentLogPath"
  $script:ProcessPollTimer.Start()
}

function Invoke-RowAction {
  param($Row)

  switch ($Row.ActionLabel) {
    "Install" { Start-BackendOperation -Mode "Apply" -Keys @($Row.Key) -Label "Install $($Row.Name)" }
    "Update" { Start-BackendOperation -Mode "Update" -Keys @($Row.Key) -Label "Update $($Row.Name)" }
    "Repair" { Start-BackendOperation -Mode "Repair" -Keys @($Row.Key) -Label "Repair $($Row.Name)" }
    "Open" {
      if (-not [string]::IsNullOrWhiteSpace($Row.ExecutablePath) -and (Test-Path -LiteralPath $Row.ExecutablePath)) {
        $target = if ((Get-Item -LiteralPath $Row.ExecutablePath).PSIsContainer) { $Row.ExecutablePath } else { Split-Path -Parent $Row.ExecutablePath }
        Start-Process $target
      } elseif (-not [string]::IsNullOrWhiteSpace($Row.FallbackUrl)) {
        Start-Process $Row.FallbackUrl
      } else {
        Add-EventMessage "No open target for $($Row.Name)."
      }
    }
    "Details" {
      if (-not [string]::IsNullOrWhiteSpace($Row.FallbackUrl)) {
        Start-Process $Row.FallbackUrl
      }
      Add-EventMessage "$($Row.Name): $($Row.Diagnostic)"
    }
  }
}

$controls.ToolsGrid.AddHandler([System.Windows.Controls.Button]::ClickEvent, [System.Windows.RoutedEventHandler]{
  param($sender, $eventArgs)
  $button = $eventArgs.OriginalSource -as [System.Windows.Controls.Button]
  if ($null -ne $button -and $null -ne $button.Tag -and $button.Tag.PSObject.Properties["Key"]) {
    Invoke-RowAction -Row $button.Tag
    $eventArgs.Handled = $true
  }
})

$script:ProcessPollTimer.Add_Tick({
  if ($null -eq $script:CurrentProcess) {
    $script:ProcessPollTimer.Stop()
    return
  }

  Read-OperationLog
  if ($script:CurrentProcess.HasExited) {
    Read-OperationLog
    $exitCode = $script:CurrentProcess.ExitCode
    $script:ProcessPollTimer.Stop()
    Complete-BackendOperation -ExitCode $exitCode
  }
})

$controls.SearchBox.Add_TextChanged({ $script:ToolsView.Refresh(); Refresh-Dashboard })
$controls.StatusFilter.Add_SelectionChanged({ $script:ToolsView.Refresh(); Refresh-Dashboard })
$controls.CategoryFilter.Add_SelectionChanged({ $script:ToolsView.Refresh(); Refresh-Dashboard })

$controls.RescanButton.Add_Click({ Start-BackendOperation -Mode "Preview" -Label "Rescan all tools" })
$controls.InstallUpdateAllButton.Add_Click({ Start-BackendOperation -Mode "Apply" -Label "Install missing and update outdated tools" })
$controls.InstallMissingButton.Add_Click({
  $keys = Get-SelectedKeys -Action "Missing"
  Start-BackendOperation -Mode "Apply" -Keys $keys -Label "Install missing tools"
})
$controls.UpdateAllButton.Add_Click({ Start-BackendOperation -Mode "Update" -Label "Update installed tools" })
$controls.RepairBrokenButton.Add_Click({
  $keys = Get-SelectedKeys -Action "Broken"
  Start-BackendOperation -Mode "Repair" -Keys $keys -Label "Repair broken tools"
})
$controls.InstallSelectedButton.Add_Click({
  $keys = Get-SelectedKeys -Action "SelectedMissingOrOutdated"
  Start-BackendOperation -Mode "Apply" -Keys $keys -Label "Install selected tools"
})
$controls.UpdateSelectedButton.Add_Click({
  $keys = Get-SelectedKeys -Action "SelectedInstalled"
  Start-BackendOperation -Mode "Update" -Keys $keys -Label "Update selected tools"
})
$controls.RepairSelectedButton.Add_Click({
  $keys = Get-SelectedKeys -Action "Selected"
  Start-BackendOperation -Mode "Repair" -Keys $keys -Label "Repair selected tools"
})
$controls.ValidateSelectedButton.Add_Click({
  $keys = Get-SelectedKeys -Action "Selected"
  Start-BackendOperation -Mode "Preview" -Keys $keys -Label "Validate selected tools"
})
$controls.RunStackValidationButton.Add_Click({ Start-BackendOperation -Mode "ValidateProjects" -Label "Run stack validation projects" })
$controls.SelectVisibleButton.Add_Click({
  foreach ($row in $script:ToolsView) { $row.Selected = $true }
  $controls.ToolsGrid.Items.Refresh()
  Refresh-Dashboard
})
$controls.ClearSelectionButton.Add_Click({
  foreach ($row in $script:ToolsRows) { $row.Selected = $false }
  $controls.ToolsGrid.Items.Refresh()
  Refresh-Dashboard
})
$controls.CancelButton.Add_Click({
  if ($null -eq $script:CurrentProcess -or $script:CurrentProcess.HasExited) { return }
  if ($script:CurrentOperation -eq "Preview") {
    $script:CurrentProcess.Kill()
    Add-EventMessage "Scan cancelled."
  } else {
    Add-EventMessage "Cancel requested. Current install/update process will be allowed to finish safely."
  }
})
$controls.OpenReportButton.Add_Click({
  New-Item -ItemType Directory -Path $script:ReportDir -Force | Out-Null
  Start-Process $script:ReportDir
})
$controls.ExportSummaryButton.Add_Click({
  $source = Join-Path $script:ReportDir "summary.md"
  if (-not (Test-Path -LiteralPath $source)) {
    Add-EventMessage "No summary.md exists yet. Run a scan first."
    return
  }
  $dialog = New-Object Microsoft.Win32.SaveFileDialog
  $dialog.Filter = "Markdown summary (*.md)|*.md|All files (*.*)|*.*"
  $dialog.FileName = "devtools-summary.md"
  if ($dialog.ShowDialog() -eq $true) {
    Copy-Item -LiteralPath $source -Destination $dialog.FileName -Force
    Add-EventMessage "Exported summary to $($dialog.FileName)"
  }
})
$controls.CopyDiagnosticsButton.Add_Click({
  $stats = Get-GuiDashboardStats -Rows @($script:ToolsRows)
  $problemRows = @($script:ToolsRows | Where-Object { $_.StatusKind -in @("Missing", "Outdated", "Error", "Action") } | Select-Object -First 25)
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("Windows DevTools Dashboard diagnostics") | Out-Null
  $lines.Add("Readiness: $($stats.Score)%") | Out-Null
  $lines.Add("Installed=$($stats.Installed) Outdated=$($stats.Outdated) Missing=$($stats.Missing) Errors=$($stats.Errors) Actions=$($stats.Actions)") | Out-Null
  foreach ($row in $problemRows) {
    $lines.Add("$($row.StatusKind): $($row.Name) [$($row.Key)] $($row.Diagnostic)") | Out-Null
  }
  [System.Windows.Clipboard]::SetText(($lines -join [Environment]::NewLine))
  Add-EventMessage "Diagnostics copied to clipboard."
})

Load-RowsFromScanFile -UseCacheOnly
Add-EventMessage "Loaded dashboard."
Write-DashboardTrace "Dashboard loaded. SmokeTest=$([bool]$SmokeTest) NoAutoScan=$([bool]$NoAutoScan)"

if ($SmokeTest) {
  "GUI smoke test OK"
  return
}

if (-not $NoAutoScan) {
  Start-BackendOperation -Mode "Preview" -Label "Initial validation scan"
}

[void]$script:Window.ShowDialog()
