Describe "DevTools bootstrapper helpers" {
  BeforeAll {
    $script:ProjectRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $script:ProjectRoot "setup-devtools.ps1") -LoadOnly
    Import-Module (Join-Path $script:ProjectRoot "gui\DevTools.GuiModel.psm1") -Force
  }

  It "does not duplicate PATH entries" {
    $path = "C:\Tools;C:\Windows"
    Add-PathEntryToList -PathValue $path -Entry "c:\tools\" | Should Be $path
  }

  It "appends missing PATH entries safely" {
    Add-PathEntryToList -PathValue "C:\Tools" -Entry "C:\MoreTools" | Should Be "C:\Tools;C:\MoreTools"
  }

  It "parses version tokens" {
    Get-VersionToken "Python 3.14.1" | Should Be "3.14.1"
  }

  It "builds winget arguments as separate tokens" {
    $args = Build-WingetArgs -Action "install" -Id "Git.Git"
    ($args -contains "--id") | Should Be $true
    ($args -contains "Git.Git") | Should Be $true
    ($args -join " ") | Should Match "--accept-package-agreements"
  }

  It "loads config and catalog JSON" {
    $config = Get-EffectiveConfig -Path (Join-Path $script:ProjectRoot "config.json")
    $catalog = Get-ToolCatalog -Path (Join-Path $script:ProjectRoot "tool-catalog.json")
    $config.installTier | Should Not BeNullOrEmpty
    @($catalog).Count | Should BeGreaterThan 10
  }

  It "skips reinstall for healthy installed tools during apply" {
    Should-RunInstallAction -Mode "Apply" -State "Installed" -IsOutdated:$false | Should Be $false
  }

  It "maps backend statuses to GUI status states" {
    Get-GuiStatusKind -Status "AlreadyPresent" | Should Be "Installed"
    Get-GuiStatusKind -Status "Outdated" | Should Be "Outdated"
    Get-GuiStatusKind -Status "ValidationFailed" | Should Be "Error"
  }

  It "computes dashboard stats" {
    $rows = @(
      [pscustomobject]@{ StatusKind = "Installed" },
      [pscustomobject]@{ StatusKind = "Missing" },
      [pscustomobject]@{ StatusKind = "Outdated" },
      [pscustomobject]@{ StatusKind = "Optional" }
    )
    $stats = Get-GuiDashboardStats -Rows $rows
    $stats.Total | Should Be 4
    $stats.Installed | Should Be 1
    $stats.Missing | Should Be 1
    $stats.Outdated | Should Be 1
  }

  It "builds selected update/install queues" {
    $rows = @(
      [pscustomobject]@{ Key = "git"; Selected = $true; StatusKind = "Installed" },
      [pscustomobject]@{ Key = "python"; Selected = $true; StatusKind = "Missing" },
      [pscustomobject]@{ Key = "node"; Selected = $false; StatusKind = "Outdated" }
    )
    Get-GuiToolKeysForAction -Rows $rows -Action "SelectedInstalled" | Should Be "git"
    Get-GuiToolKeysForAction -Rows $rows -Action "SelectedMissingOrOutdated" | Should Be "python"
  }
}
