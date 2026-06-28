function Get-DevToolSection {
  param(
    [string]$Key,
    [string]$Category
  )

  $systemKeys = @("winget", "powershell7", "windows-terminal", "git", "github-cli", "git-lfs", "vscode", "vs-build-tools", "7zip", "ripgrep", "fd", "jq", "yq", "curl", "bat", "fzf", "delta", "just", "zoxide", "hyperfine", "powertoys", "sysinternals-suite")
  $workflowKeys = @("gh-auth", "git-config", "git-longpaths", "github-desktop", "actionlint")
  $qualityKeys = @("pre-commit", "psscriptanalyzer", "pester", "shellcheck", "semgrep", "scc", "markdownlint", "hadolint", "trivy")

  if ($systemKeys -contains $Key) { return "1_System Core" }
  if ($Category -match "Python" -or $Key -in @("pipx", "uv", "virtualenv", "pytest", "ruff", "mypy", "pyright", "python-build", "pyinstaller", "nuitka", "poetry", "cookiecutter", "copier")) { return "2_Python" }
  if ($Category -match "C#|\.NET" -or $Key -in @("dotnet-sdk", "nuget", "roslynator", "dotnet-ef")) { return "3_DotNet_CSharp" }
  if ($Category -match "Java") { return "4_Java" }
  if ($Category -match "JavaScript|TypeScript" -or $Key -in @("node", "npm", "pnpm", "yarn", "typescript", "eslint", "prettier", "vitest", "npm-check-updates", "degit", "deno", "bun")) { return "5_Node_TypeScript" }
  if ($workflowKeys -contains $Key -or $Category -match "GitHub") { return "6_GitHub_Workflow" }
  if ($qualityKeys -contains $Key -or $Category -match "quality") { return "7_Logic_Quality_Tools" }
  return "8_Optional_Extras"
}

function Get-GuiStatusKind {
  param([string]$Status)

  switch -Regex ($Status) {
    "AlreadyPresent|Installed|Updated|OnPath" { return "Installed" }
    "Outdated" { return "Outdated" }
    "Missing|SkippedMissing" { return "Missing" }
    "ValidationFailed|Failed|NeedsAdmin|MissingFromPath" { return "Error" }
    "ManualAction|ActionNeeded" { return "Action" }
    "NoValidationCommand|NotSelected|Optional" { return "Optional" }
    default { return "Error" }
  }
}

function Get-GuiStatusGlyph {
  param([string]$Kind)

  switch ($Kind) {
    "Installed" { return ([char]0x2713).ToString() }
    "Outdated" { return "!" }
    "Missing" { return ([char]0x2715).ToString() }
    "Error" { return ([char]0x2715).ToString() }
    "Action" { return "!" }
    default { return ([char]0x25CB).ToString() }
  }
}

function Get-GuiStatusBrush {
  param([string]$Kind)

  switch ($Kind) {
    "Installed" { return "#22c55e" }
    "Outdated" { return "#eab308" }
    "Missing" { return "#ef4444" }
    "Error" { return "#ef4444" }
    "Action" { return "#f59e0b" }
    default { return "#94a3b8" }
  }
}

function Get-GuiActionLabel {
  param([string]$Kind)

  switch ($Kind) {
    "Installed" { return "Open" }
    "Outdated" { return "Update" }
    "Missing" { return "Install" }
    "Error" { return "Repair" }
    "Action" { return "Details" }
    default { return "Skip" }
  }
}

function ConvertTo-GuiToolRows {
  param(
    [array]$CatalogTools,
    $Scan
  )

  $resultsByKey = @{}
  $catalogKeySet = @{}
  if ($null -ne $Scan -and $null -ne $Scan.results) {
    foreach ($result in @($Scan.results)) {
      if (-not [string]::IsNullOrWhiteSpace($result.Key)) {
        $resultsByKey[$result.Key] = $result
      }
    }
  }

  $rows = New-Object System.Collections.Generic.List[object]
  foreach ($tool in $CatalogTools) {
    $key = [string]$tool.key
    $catalogKeySet[$key] = $true
    $result = $null
    if ($resultsByKey.ContainsKey($key)) {
      $result = $resultsByKey[$key]
    }

    $status = if ($null -ne $result) { [string]$result.Status } else { "NotSelected" }
    $kind = Get-GuiStatusKind -Status $status
    $category = if ($null -ne $tool.category) { [string]$tool.category } elseif ($null -ne $result) { [string]$result.Category } else { "" }
    $tier = if ($null -ne $tool.tier) { [string]$tool.tier } else { "" }
    $optional = ($tier -eq "Full" -or $status -eq "NotSelected")

    $rows.Add([pscustomobject]@{
      Selected = ($kind -in @("Missing", "Outdated", "Error"))
      Key = $key
      Name = [string]$tool.displayName
      Category = $category
      Section = Get-DevToolSection -Key $key -Category $category
      Status = $status
      StatusKind = $kind
      StatusGlyph = Get-GuiStatusGlyph -Kind $kind
      StatusBrush = Get-GuiStatusBrush -Kind $kind
      InstalledVersion = if ($null -ne $result) { [string]$result.CurrentVersion } else { "" }
      LatestVersion = if ($null -ne $result) { [string]$result.DesiredVersion } else { "" }
      ExecutablePath = if ($null -ne $result) { [string]$result.ExecutablePath } else { "" }
      InstallMethod = if ($null -ne $result) { [string]$result.InstallMethod } else { [string]$tool.manager }
      ActionLabel = Get-GuiActionLabel -Kind $kind
      Diagnostic = if ($null -ne $result -and -not [string]::IsNullOrWhiteSpace($result.Error)) { [string]$result.Error } elseif ($null -ne $tool.installNotes) { [string]$tool.installNotes } else { "" }
      FallbackUrl = if ($null -ne $tool.fallbackUrl) { [string]$tool.fallbackUrl } else { "" }
      Optional = $optional
      Tier = $tier
      RequiresAdmin = [bool]$tool.adminRequired
    }) | Out-Null
  }

  if ($null -ne $Scan -and $null -ne $Scan.results) {
    foreach ($result in @($Scan.results)) {
      if (-not [string]::IsNullOrWhiteSpace($result.Key) -and -not $catalogKeySet.ContainsKey([string]$result.Key)) {
        $kind = Get-GuiStatusKind -Status ([string]$result.Status)
        $rows.Add([pscustomobject]@{
          Selected = ($kind -in @("Missing", "Outdated", "Error", "Action"))
          Key = [string]$result.Key
          Name = [string]$result.Tool
          Category = [string]$result.Category
          Section = Get-DevToolSection -Key ([string]$result.Key) -Category ([string]$result.Category)
          Status = [string]$result.Status
          StatusKind = $kind
          StatusGlyph = Get-GuiStatusGlyph -Kind $kind
          StatusBrush = Get-GuiStatusBrush -Kind $kind
          InstalledVersion = [string]$result.CurrentVersion
          LatestVersion = [string]$result.DesiredVersion
          ExecutablePath = [string]$result.ExecutablePath
          InstallMethod = [string]$result.InstallMethod
          ActionLabel = Get-GuiActionLabel -Kind $kind
          Diagnostic = [string]$result.Error
          FallbackUrl = ""
          Optional = $false
          Tier = ""
          RequiresAdmin = $false
        }) | Out-Null
      }
    }
  }

  return @($rows | Sort-Object Section, Name)
}

function Get-GuiDashboardStats {
  param([array]$Rows)

  $active = @($Rows | Where-Object { $_.StatusKind -ne "Optional" })
  $total = @($Rows).Count
  $installed = @($Rows | Where-Object { $_.StatusKind -eq "Installed" }).Count
  $outdated = @($Rows | Where-Object { $_.StatusKind -eq "Outdated" }).Count
  $missing = @($Rows | Where-Object { $_.StatusKind -eq "Missing" }).Count
  $errors = @($Rows | Where-Object { $_.StatusKind -eq "Error" }).Count
  $actions = @($Rows | Where-Object { $_.StatusKind -eq "Action" }).Count
  $denominator = [Math]::Max(@($active).Count, 1)
  $score = [int][Math]::Round(($installed / $denominator) * 100)

  return [pscustomobject]@{
    Score = $score
    Total = $total
    Installed = $installed
    Outdated = $outdated
    Missing = $missing
    Errors = $errors
    Actions = $actions
  }
}

function Get-GuiToolKeysForAction {
  param(
    [array]$Rows,
    [ValidateSet("Missing", "Outdated", "Broken", "Selected", "SelectedInstalled", "SelectedMissingOrOutdated")]
    [string]$Action
  )

  $filtered = switch ($Action) {
    "Missing" { $Rows | Where-Object { $_.StatusKind -eq "Missing" } }
    "Outdated" { $Rows | Where-Object { $_.StatusKind -eq "Outdated" -or $_.StatusKind -eq "Installed" } }
    "Broken" { $Rows | Where-Object { $_.StatusKind -eq "Error" } }
    "Selected" { $Rows | Where-Object { $_.Selected } }
    "SelectedInstalled" { $Rows | Where-Object { $_.Selected -and $_.StatusKind -in @("Installed", "Outdated") } }
    "SelectedMissingOrOutdated" { $Rows | Where-Object { $_.Selected -and $_.StatusKind -in @("Missing", "Outdated") } }
  }

  return @($filtered | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Key) } | Select-Object -ExpandProperty Key -Unique)
}

Export-ModuleMember -Function Get-DevToolSection, Get-GuiStatusKind, Get-GuiStatusGlyph, Get-GuiStatusBrush, Get-GuiActionLabel, ConvertTo-GuiToolRows, Get-GuiDashboardStats, Get-GuiToolKeysForAction
