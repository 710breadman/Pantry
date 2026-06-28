[CmdletBinding()]
param(
  [switch]$Preview,
  [switch]$Apply,
  [switch]$Repair,
  [switch]$Update,
  [ValidateSet("Core", "Recommended", "Full")]
  [string]$Tier,
  [switch]$Unattended,
  [switch]$FixPath,
  [switch]$InstallVSCodeExtensions,
  [switch]$SkipVSCodeExtensions,
  [string]$ConfigPath = ".\config.json",
  [string]$CatalogPath = ".\tool-catalog.json",
  [string]$ReportDir,
  [switch]$SkipValidationProjects,
  [switch]$SelfTest,
  [switch]$LoadOnly,
  [switch]$Gui,
  [string[]]$ToolKeys = @(),
  [string[]]$Categories = @(),
  [switch]$RunValidationProjectsOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$script:Results = New-Object System.Collections.Generic.List[object]
$script:ValidationResults = New-Object System.Collections.Generic.List[object]
$script:ManualActions = New-Object System.Collections.Generic.List[string]
$script:RepairSuggestions = New-Object System.Collections.Generic.List[string]
$script:ValidationProjectResults = New-Object System.Collections.Generic.List[object]
$script:WingetIdCache = @{}
$script:RebootRecommended = $false
$script:GitHubLoginNeeded = $false
$script:GitIdentityMissing = $false
$script:GitLongPathsEnabled = $false
$script:TierWasProvided = $PSBoundParameters.ContainsKey("Tier")

$script:DefaultConfig = [ordered]@{
  installTier = "Recommended"
  useWinget = $true
  installDocker = $false
  installGitHubDesktop = $false
  installVisualStudioBuildTools = $true
  installCppBuildTools = $true
  installWindowsDesktopBuildTools = $false
  installPython = $true
  installDotNet = $true
  installJava = $true
  installNode = $true
  installPowerShell7 = $true
  installWindowsTerminal = $true
  installGit = $true
  installGitHubCli = $true
  installVSCode = $true
  installVSCodeExtensions = $true
  configureGitLongPaths = $true
  installPythonQualityTools = $true
  installDotNetQualityTools = $true
  installJavaQualityTools = $false
  installNodeQualityTools = $true
  installGeneralQualityTools = $true
  installPowerUserTools = $true
  installPowerShellQualityTools = $true
  installRuntimeManagers = $false
  installAltJsRuntimes = $false
  installRustToolchain = $false
  installLLVM = $false
  installCloudTools = $false
  installSecurityTools = $false
  createValidationProjects = $true
  fixPathAutomatically = $false
  allowFallbackDownloads = $false
  reportOutputFolder = "devtools_setup_report"
  validationProjectsFolder = "devtools_validation_projects"
}

function ConvertTo-Hashtable {
  param([Parameter(ValueFromPipeline = $true)]$InputObject)

  if ($null -eq $InputObject) {
    return $null
  }

  if ($InputObject -is [System.Collections.IDictionary]) {
    $hash = [ordered]@{}
    foreach ($key in $InputObject.Keys) {
      $hash[$key] = ConvertTo-Hashtable $InputObject[$key]
    }
    return $hash
  }

  if (($InputObject -is [System.Collections.IEnumerable]) -and -not ($InputObject -is [string])) {
    $items = @()
    foreach ($item in $InputObject) {
      $items += ,(ConvertTo-Hashtable $item)
    }
    return $items
  }

  if ($InputObject.GetType().Name -eq "PSCustomObject") {
    $hash = [ordered]@{}
    foreach ($property in $InputObject.PSObject.Properties) {
      $hash[$property.Name] = ConvertTo-Hashtable $property.Value
    }
    return $hash
  }

  return $InputObject
}

function Get-HashValue {
  param(
    [hashtable]$Hash,
    [string]$Name,
    $Default = $null
  )

  if ($null -ne $Hash -and $Hash.Contains($Name)) {
    return $Hash[$Name]
  }

  return $Default
}

function Resolve-ProjectPath {
  param(
    [string]$Path,
    [switch]$MustExist
  )

  if ([string]::IsNullOrWhiteSpace($Path)) {
    return $null
  }

  if ([System.IO.Path]::IsPathRooted($Path)) {
    $resolved = $Path
  } else {
    $resolved = Join-Path $PSScriptRoot $Path
  }

  $full = [System.IO.Path]::GetFullPath($resolved)
  if ($MustExist -and -not (Test-Path -LiteralPath $full)) {
    throw "Path not found: $full"
  }

  return $full
}

function Read-JsonHashtable {
  param([string]$Path)

  $full = Resolve-ProjectPath -Path $Path -MustExist
  $raw = Get-Content -LiteralPath $full -Raw
  return ConvertTo-Hashtable ($raw | ConvertFrom-Json)
}

function Get-EffectiveConfig {
  param([string]$Path)

  $config = [ordered]@{}
  foreach ($key in $script:DefaultConfig.Keys) {
    $config[$key] = $script:DefaultConfig[$key]
  }

  $full = Resolve-ProjectPath -Path $Path
  if (Test-Path -LiteralPath $full) {
    $loaded = Read-JsonHashtable -Path $full
    foreach ($key in $loaded.Keys) {
      $config[$key] = $loaded[$key]
    }
  }

  if ($script:TierWasProvided) {
    $config["installTier"] = $Tier
  }
  if ($InstallVSCodeExtensions) {
    $config["installVSCodeExtensions"] = $true
  }
  if ($SkipVSCodeExtensions) {
    $config["installVSCodeExtensions"] = $false
  }
  if ($SkipValidationProjects) {
    $config["createValidationProjects"] = $false
  }
  if ($FixPath) {
    $config["fixPathAutomatically"] = $true
  }

  return $config
}

function Get-ToolCatalog {
  param([string]$Path)

  $catalog = Read-JsonHashtable -Path $Path
  $tools = @(Get-HashValue -Hash $catalog -Name "tools" -Default @())
  return $tools | Sort-Object { [int](Get-HashValue -Hash $_ -Name "order" -Default 9999) }, { Get-HashValue -Hash $_ -Name "displayName" -Default "" }
}

function Get-TierRank {
  param([string]$Name)

  switch ($Name) {
    "Core" { return 1 }
    "Recommended" { return 2 }
    "Full" { return 3 }
    default { return 2 }
  }
}

function Test-ToolSelected {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$SelectedTier,
    [bool]$TierWasExplicit
  )

  $toolTier = Get-HashValue -Hash $Tool -Name "tier" -Default "Recommended"
  if ((Get-TierRank $toolTier) -gt (Get-TierRank $SelectedTier)) {
    return $false
  }

  $configFlag = Get-HashValue -Hash $Tool -Name "configFlag"
  if (-not [string]::IsNullOrWhiteSpace($configFlag)) {
    $flagValue = [bool](Get-HashValue -Hash $Config -Name $configFlag -Default $false)
    $allowTierOverride = [bool](Get-HashValue -Hash $Tool -Name "allowTierOverride" -Default $false)
    if (-not $flagValue) {
      if (-not ($TierWasExplicit -and $SelectedTier -eq "Full" -and $allowTierOverride)) {
        return $false
      }
    }
  }

  $manager = Get-HashValue -Hash $Tool -Name "manager" -Default ""
  if ($manager -eq "vscodeExtension") {
    if (-not [bool](Get-HashValue -Hash $Config -Name "installVSCode" -Default $true)) {
      return $false
    }
    if (-not [bool](Get-HashValue -Hash $Config -Name "installVSCodeExtensions" -Default $false)) {
      return $false
    }
  }

  return $true
}

function Write-Info {
  param([string]$Message)
  Write-Host $Message -ForegroundColor Cyan
}

function Write-Good {
  param([string]$Message)
  Write-Host $Message -ForegroundColor Green
}

function Write-Warn {
  param([string]$Message)
  Write-Host $Message -ForegroundColor Yellow
}

function Write-Bad {
  param([string]$Message)
  Write-Host $Message -ForegroundColor Red
}

function Limit-Text {
  param(
    [string]$Text,
    [int]$Length = 1200
  )

  if ([string]::IsNullOrEmpty($Text)) {
    return ""
  }

  if ($Text.Length -le $Length) {
    return $Text.Trim()
  }

  return ($Text.Substring(0, $Length) + "`n...[truncated]").Trim()
}

function Escape-SingleQuote {
  param([string]$Text)
  return ($Text -replace "'", "''")
}

function Get-StringList {
  param($Value)

  if ($null -eq $Value) {
    return @()
  }
  if ($Value -is [string]) {
    return @([string]$Value)
  }
  return @($Value | ForEach-Object { [string]$_ })
}

function Invoke-CapturedCommand {
  param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [string[]]$Arguments = @()
  )

  try {
    $global:LASTEXITCODE = 0
    $lines = & $FilePath @Arguments 2>&1 | ForEach-Object { $_.ToString() }
    $exit = if ($null -eq $global:LASTEXITCODE) { 0 } else { [int]$global:LASTEXITCODE }
    return [pscustomobject]@{
      ExitCode = $exit
      Output = (($lines | Out-String).Trim())
      Succeeded = ($exit -eq 0)
    }
  } catch {
    return [pscustomobject]@{
      ExitCode = 127
      Output = $_.Exception.Message
      Succeeded = $false
    }
  }
}

function Get-CommandPath {
  param([string]$Name)

  if ([string]::IsNullOrWhiteSpace($Name)) {
    return $null
  }

  $command = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($null -eq $command) {
    return $null
  }

  foreach ($property in @("Source", "Path", "Definition")) {
    $propertyInfo = $command.PSObject.Properties[$property]
    if ($null -ne $propertyInfo) {
      $value = [string]$propertyInfo.Value
      if (-not [string]::IsNullOrWhiteSpace($value)) {
        return $value
      }
    }
  }

  return $null
}

function Get-VersionToken {
  param([string]$Text)

  if ([string]::IsNullOrWhiteSpace($Text)) {
    return ""
  }

  $match = [regex]::Match($Text, "\d+(\.\d+){1,4}([\-+][A-Za-z0-9\.\-_]+)?")
  if ($match.Success) {
    return $match.Value
  }

  return ""
}

function Get-FirstMeaningfulLine {
  param([string]$Text)

  if ([string]::IsNullOrWhiteSpace($Text)) {
    return ""
  }

  foreach ($line in ($Text -split "`r?`n")) {
    $trimmed = $line.Trim()
    if (-not [string]::IsNullOrWhiteSpace($trimmed) -and -not $trimmed.StartsWith("__DEVTOOLS_PATH__")) {
      return $trimmed
    }
  }

  return ""
}

function Get-FreshShell {
  $pwsh = Get-CommandPath "pwsh"
  if (-not [string]::IsNullOrWhiteSpace($pwsh)) {
    return $pwsh
  }

  return (Get-CommandPath "powershell.exe")
}

function Invoke-FreshShellCommand {
  param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string[]]$Arguments = @(),
    [string]$ExactPath
  )

  $shell = Get-FreshShell
  if ([string]::IsNullOrWhiteSpace($shell)) {
    return [pscustomobject]@{
      ExitCode = 127
      Output = "No PowerShell executable was available for fresh-shell validation."
      Succeeded = $false
      ExecutablePath = ""
    }
  }

  $argLiteral = (($Arguments | ForEach-Object { "'" + (Escape-SingleQuote $_) + "'" }) -join ", ")
  $exactLiteral = Escape-SingleQuote $ExactPath
  $exeLiteral = Escape-SingleQuote $Executable
  $validationScript = @"
`$ErrorActionPreference = 'Stop'
try {
  if ('$exactLiteral' -ne '') {
    `$cmdPath = '$exactLiteral'
  } else {
    `$cmd = Get-Command '$exeLiteral' -ErrorAction Stop
    `$cmdPath = `$cmd.Source
  }
  Write-Output ('__DEVTOOLS_PATH__' + `$cmdPath)
  `$toolArgs = @($argLiteral)
  `$toolOutput = & `$cmdPath @toolArgs 2>&1
  if (`$null -ne `$toolOutput) {
    `$toolOutput | ForEach-Object { `$_.ToString() }
  }
  if (`$null -eq `$global:LASTEXITCODE) { exit 0 }
  exit `$global:LASTEXITCODE
} catch {
  Write-Error `$_.Exception.Message
  exit 127
}
"@

  $tempScript = Join-Path ([System.IO.Path]::GetTempPath()) ("devtools-validate-" + [guid]::NewGuid().ToString("N") + ".ps1")
  Set-Content -LiteralPath $tempScript -Value $validationScript -Encoding UTF8
  try {
    $result = Invoke-CapturedCommand -FilePath $shell -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $tempScript)
  } finally {
    Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
  }

  $path = ""
  foreach ($line in ($result.Output -split "`r?`n")) {
    if ($line.StartsWith("__DEVTOOLS_PATH__")) {
      $path = $line.Substring("__DEVTOOLS_PATH__".Length)
      break
    }
  }

  return [pscustomobject]@{
    ExitCode = $result.ExitCode
    Output = $result.Output
    Succeeded = $result.Succeeded
    ExecutablePath = $path
  }
}

function Test-IsAdmin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($identity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Normalize-PathEntry {
  param([string]$Entry)

  if ([string]::IsNullOrWhiteSpace($Entry)) {
    return ""
  }

  $expanded = [Environment]::ExpandEnvironmentVariables($Entry.Trim().Trim('"'))
  try {
    $full = [System.IO.Path]::GetFullPath($expanded)
  } catch {
    $full = $expanded
  }

  return $full.TrimEnd('\').ToLowerInvariant()
}

function Split-PathValue {
  param([string]$PathValue)

  if ([string]::IsNullOrWhiteSpace($PathValue)) {
    return @()
  }

  return @($PathValue -split ";" | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Add-PathEntryToList {
  param(
    [string]$PathValue,
    [string]$Entry
  )

  if ([string]::IsNullOrWhiteSpace($Entry)) {
    return $PathValue
  }

  $entries = @(Split-PathValue $PathValue)
  $normalizedEntry = Normalize-PathEntry $Entry
  foreach ($existing in $entries) {
    if ((Normalize-PathEntry $existing) -eq $normalizedEntry) {
      return $PathValue
    }
  }

  if ($entries.Count -eq 0) {
    return $Entry
  }

  return (($entries + $Entry) -join ";")
}

function Test-PathListContains {
  param(
    [string]$PathValue,
    [string]$Entry
  )

  $normalizedEntry = Normalize-PathEntry $Entry
  foreach ($existing in (Split-PathValue $PathValue)) {
    if ((Normalize-PathEntry $existing) -eq $normalizedEntry) {
      return $true
    }
  }
  return $false
}

function Get-PathAudit {
  $scopes = @("Machine", "User")
  $rows = New-Object System.Collections.Generic.List[object]
  $seen = @{}

  foreach ($scope in $scopes) {
    $pathValue = [Environment]::GetEnvironmentVariable("Path", $scope)
    foreach ($entry in (Split-PathValue $pathValue)) {
      $normalized = Normalize-PathEntry $entry
      $expanded = [Environment]::ExpandEnvironmentVariables($entry.Trim().Trim('"'))
      $exists = $false
      if ($expanded -notmatch "[\*\?]") {
        $exists = Test-Path -LiteralPath $expanded
      }
      $duplicate = $false
      if (-not [string]::IsNullOrWhiteSpace($normalized)) {
        if ($seen.ContainsKey($normalized)) {
          $duplicate = $true
        } else {
          $seen[$normalized] = $true
        }
      }

      $rows.Add([pscustomobject]@{
        Scope = $scope
        Entry = $entry
        ExpandedEntry = $expanded
        Exists = $exists
        Duplicate = $duplicate
      }) | Out-Null
    }
  }

  return $rows
}

function Update-CurrentProcessEnvironment {
  $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
  $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
  $env:Path = (@($machinePath, $userPath) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ";"

  foreach ($name in @("JAVA_HOME", "DOTNET_ROOT")) {
    $userValue = [Environment]::GetEnvironmentVariable($name, "User")
    $machineValue = [Environment]::GetEnvironmentVariable($name, "Machine")
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
      Set-Item -Path "Env:\$name" -Value $userValue
    } elseif (-not [string]::IsNullOrWhiteSpace($machineValue)) {
      Set-Item -Path "Env:\$name" -Value $machineValue
    }
  }
}

function Approve-Change {
  param(
    [string]$Message,
    [hashtable]$Config
  )

  if ([bool](Get-HashValue -Hash $Config -Name "fixPathAutomatically" -Default $false)) {
    return $true
  }
  if ($Unattended) {
    return $false
  }

  $answer = Read-Host "$Message [y/N]"
  return ($answer -match "^(y|yes)$")
}

function Add-UserPathEntry {
  param(
    [string]$Entry,
    [hashtable]$Config,
    [string]$Reason
  )

  if ([string]::IsNullOrWhiteSpace($Entry)) {
    return $false
  }

  if (-not (Test-Path -LiteralPath $Entry)) {
    $script:RepairSuggestions.Add("PATH candidate does not exist yet: $Entry ($Reason)") | Out-Null
    return $false
  }

  $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
  $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
  if ((Test-PathListContains -PathValue $machinePath -Entry $Entry) -or (Test-PathListContains -PathValue $userPath -Entry $Entry)) {
    return $false
  }

  if (-not (Approve-Change -Message "Add $Entry to your user PATH for $Reason?" -Config $Config)) {
    $script:RepairSuggestions.Add("PATH entry missing for $Reason`: $Entry") | Out-Null
    return $false
  }

  $newUserPath = Add-PathEntryToList -PathValue $userPath -Entry $Entry
  [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
  Update-CurrentProcessEnvironment
  return $true
}

function Build-WingetArgs {
  param(
    [ValidateSet("install", "upgrade")]
    [string]$Action,
    [Parameter(Mandatory = $true)][string]$Id,
    [string]$Override
  )

  $args = @(
    $Action,
    "--id", $Id,
    "--exact",
    "--source", "winget",
    "--accept-source-agreements",
    "--disable-interactivity"
  )

  if ($Action -in @("install", "upgrade")) {
    $args += "--accept-package-agreements"
    $args += "--silent"
  }

  if (-not [string]::IsNullOrWhiteSpace($Override)) {
    $args += "--override"
    $args += $Override
  }

  return $args
}

function Test-WingetPackageId {
  param([string]$Id)

  if ([string]::IsNullOrWhiteSpace($Id)) {
    return $false
  }
  if ($script:WingetIdCache.ContainsKey($Id)) {
    return [bool]$script:WingetIdCache[$Id].Available
  }

  $winget = Get-CommandPath "winget"
  if ([string]::IsNullOrWhiteSpace($winget)) {
    $script:WingetIdCache[$Id] = [pscustomobject]@{ Available = $false; Version = "" }
    return $false
  }

  $result = Invoke-CapturedCommand -FilePath $winget -Arguments @("show", "--id", $Id, "--exact", "--source", "winget", "--accept-source-agreements", "--disable-interactivity")
  $available = ($result.Output -match [regex]::Escape($Id) -and $result.Output -notmatch "No package found|No package")
  $version = ""
  if ($available) {
    foreach ($line in ($result.Output -split "`r?`n")) {
      if ($line -match "^\s*Version:\s*(.+)\s*$") {
        $version = $Matches[1].Trim()
        break
      }
    }
  }

  $script:WingetIdCache[$Id] = [pscustomobject]@{ Available = $available; Version = $version }
  return $available
}

function Test-WingetInstalledPackage {
  param([string]$Id)

  $winget = Get-CommandPath "winget"
  if ([string]::IsNullOrWhiteSpace($winget) -or [string]::IsNullOrWhiteSpace($Id)) {
    return $false
  }

  $result = Invoke-CapturedCommand -FilePath $winget -Arguments @("list", "--id", $Id, "--exact", "--source", "winget", "--accept-source-agreements", "--disable-interactivity")
  return ($result.Output -match [regex]::Escape($Id))
}

function Get-PreferredWingetPackage {
  param([hashtable]$Tool)

  $ids = @()
  $primary = Get-HashValue -Hash $Tool -Name "wingetId"
  if (-not [string]::IsNullOrWhiteSpace($primary)) {
    $ids += $primary
  }
  $ids += Get-StringList (Get-HashValue -Hash $Tool -Name "alternateWingetIds" -Default @())

  foreach ($id in $ids) {
    if (Test-WingetPackageId -Id $id) {
      $cached = $script:WingetIdCache[$id]
      return [pscustomobject]@{
        Id = $id
        Version = $cached.Version
      }
    }
  }

  return [pscustomobject]@{
    Id = ""
    Version = ""
  }
}

function Test-WingetUpgradeAvailable {
  param([string]$Id)

  $winget = Get-CommandPath "winget"
  if ([string]::IsNullOrWhiteSpace($winget) -or [string]::IsNullOrWhiteSpace($Id)) {
    return $false
  }

  $result = Invoke-CapturedCommand -FilePath $winget -Arguments @("upgrade", "--id", $Id, "--exact", "--source", "winget", "--accept-source-agreements", "--disable-interactivity")
  if ($result.Output -match [regex]::Escape($Id) -and $result.Output -notmatch "No available upgrade|No installed package") {
    return $true
  }
  return $false
}

function Get-WingetOverride {
  param(
    [hashtable]$Tool,
    [hashtable]$Config
  )

  $template = Get-HashValue -Hash $Tool -Name "wingetOverrideTemplate"
  if ($template -ne "visualStudioBuildTools") {
    return ""
  }

  $workloads = @("Microsoft.VisualStudio.Workload.MSBuildTools")
  if ([bool](Get-HashValue -Hash $Config -Name "installCppBuildTools" -Default $true)) {
    $workloads += "Microsoft.VisualStudio.Workload.VCTools"
  }
  if ([bool](Get-HashValue -Hash $Config -Name "installWindowsDesktopBuildTools" -Default $false)) {
    $workloads += "Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools"
  }

  $parts = @("--quiet", "--wait", "--norestart", "--includeRecommended")
  foreach ($workload in $workloads) {
    $parts += "--add"
    $parts += $workload
  }

  return ($parts -join " ")
}

function Get-MSBuildPath {
  $vswhereCandidates = @(
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
  )

  foreach ($candidate in $vswhereCandidates) {
    if (Test-Path -LiteralPath $candidate) {
      $result = Invoke-CapturedCommand -FilePath $candidate -Arguments @("-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild", "-find", "MSBuild\**\Bin\MSBuild.exe")
      foreach ($line in ($result.Output -split "`r?`n")) {
        $trimmed = $line.Trim()
        if (Test-Path -LiteralPath $trimmed) {
          return $trimmed
        }
      }
    }
  }

  $common = @(
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2026\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2026\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
  )
  foreach ($path in $common) {
    if (Test-Path -LiteralPath $path) {
      return $path
    }
  }

  return $null
}

function Find-ToolPathFromHints {
  param([hashtable]$Tool)

  $validation = Get-HashValue -Hash $Tool -Name "validation" -Default @{}
  $pathDiscovery = Get-HashValue -Hash $validation -Name "pathDiscovery"
  if ($pathDiscovery -eq "msbuild") {
    return Get-MSBuildPath
  }

  foreach ($hint in (Get-StringList (Get-HashValue -Hash $Tool -Name "pathHints" -Default @()))) {
    $expanded = [Environment]::ExpandEnvironmentVariables($hint)
    if (Test-Path -LiteralPath $expanded) {
      return $expanded
    }
  }

  return $null
}

function Test-PSModuleAvailable {
  param([string]$ModuleName)

  $module = Get-Module -ListAvailable -Name $ModuleName | Sort-Object Version -Descending | Select-Object -First 1
  if ($null -eq $module) {
    return [pscustomobject]@{ Installed = $false; Version = ""; Path = "" }
  }
  return [pscustomobject]@{ Installed = $true; Version = [string]$module.Version; Path = $module.Path }
}

function Test-VSCodeExtensionInstalled {
  param([string]$ExtensionId)

  $code = Get-CommandPath "code"
  if ([string]::IsNullOrWhiteSpace($code)) {
    return $false
  }
  $result = Invoke-CapturedCommand -FilePath $code -Arguments @("--list-extensions")
  return (($result.Output -split "`r?`n") -contains $ExtensionId)
}

function Invoke-ToolValidation {
  param(
    [hashtable]$Tool,
    [switch]$FreshShell
  )

  $manager = Get-HashValue -Hash $Tool -Name "manager" -Default ""
  $displayName = Get-HashValue -Hash $Tool -Name "displayName" -Default ""

  if ($manager -eq "vscodeExtension") {
    $extensionId = Get-HashValue -Hash $Tool -Name "extensionId"
    $installed = Test-VSCodeExtensionInstalled -ExtensionId $extensionId
    return [pscustomobject]@{
      Tool = $displayName
      Success = $installed
      Status = if ($installed) { "Installed" } else { "Missing" }
      ExecutablePath = "code extension:$extensionId"
      VersionText = ""
      Output = ""
      ExitCode = if ($installed) { 0 } else { 1 }
    }
  }

  if ($manager -eq "psModule") {
    $moduleName = Get-HashValue -Hash $Tool -Name "moduleName" -Default (Get-HashValue -Hash $Tool -Name "package")
    $module = Test-PSModuleAvailable -ModuleName $moduleName
    return [pscustomobject]@{
      Tool = $displayName
      Success = $module.Installed
      Status = if ($module.Installed) { "Installed" } else { "Missing" }
      ExecutablePath = $module.Path
      VersionText = $module.Version
      Output = ""
      ExitCode = if ($module.Installed) { 0 } else { 1 }
    }
  }

  $validation = Get-HashValue -Hash $Tool -Name "validation" -Default @{}
  $executable = Get-HashValue -Hash $validation -Name "executable"
  $arguments = Get-StringList (Get-HashValue -Hash $validation -Name "arguments" -Default @())

  if ([string]::IsNullOrWhiteSpace($executable)) {
    if ($manager -eq "winget") {
      $package = Get-PreferredWingetPackage -Tool $Tool
      $installed = Test-WingetInstalledPackage -Id $package.Id
      return [pscustomobject]@{
        Tool = $displayName
        Success = $installed
        Status = if ($installed) { "Installed" } else { "Missing" }
        ExecutablePath = "winget:$($package.Id)"
        VersionText = $package.Version
        Output = ""
        ExitCode = if ($installed) { 0 } else { 1 }
      }
    }

    return [pscustomobject]@{
      Tool = $displayName
      Success = $true
      Status = "NoValidationCommand"
      ExecutablePath = ""
      VersionText = ""
      Output = ""
      ExitCode = 0
    }
  }

  $commandPath = Get-CommandPath $executable
  $exactPath = ""
  if ([string]::IsNullOrWhiteSpace($commandPath)) {
    $exactPath = Find-ToolPathFromHints -Tool $Tool
  }

  if ([string]::IsNullOrWhiteSpace($commandPath) -and [string]::IsNullOrWhiteSpace($exactPath)) {
    return [pscustomobject]@{
      Tool = $displayName
      Success = $false
      Status = "Missing"
      ExecutablePath = ""
      VersionText = ""
      Output = "$executable was not found."
      ExitCode = 127
    }
  }

  if ($FreshShell) {
    $result = Invoke-FreshShellCommand -Executable $executable -Arguments $arguments -ExactPath $exactPath
    $path = if (-not [string]::IsNullOrWhiteSpace($result.ExecutablePath)) { $result.ExecutablePath } else { $exactPath }
  } else {
    $path = if (-not [string]::IsNullOrWhiteSpace($exactPath)) { $exactPath } else { $commandPath }
    $result = Invoke-CapturedCommand -FilePath $path -Arguments $arguments
  }

  $firstLine = Get-FirstMeaningfulLine $result.Output
  return [pscustomobject]@{
    Tool = $displayName
    Success = ($result.ExitCode -eq 0)
    Status = if ($result.ExitCode -eq 0) { "Installed" } else { "ValidationFailed" }
    ExecutablePath = $path
    VersionText = $firstLine
    Output = Limit-Text $result.Output
    ExitCode = $result.ExitCode
  }
}

function Get-ToolState {
  param([hashtable]$Tool)

  $validation = Invoke-ToolValidation -Tool $Tool
  return [pscustomobject]@{
    Status = $validation.Status
    Installed = $validation.Success
    ExecutablePath = $validation.ExecutablePath
    VersionText = $validation.VersionText
    Error = if ($validation.Success) { "" } else { $validation.Output }
  }
}

function Should-RunInstallAction {
  param(
    [ValidateSet("Preview", "Apply", "Repair", "Update")]
    [string]$Mode,
    [string]$State,
    [bool]$IsOutdated
  )

  switch ($Mode) {
    "Preview" { return $false }
    "Apply" { return (($State -eq "Missing") -or $IsOutdated) }
    "Repair" { return ($State -in @("Missing", "ValidationFailed")) }
    "Update" { return ($State -notin @("Missing", "NoValidationCommand")) }
  }
}

function Invoke-FallbackAction {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Reason
  )

  $name = Get-HashValue -Hash $Tool -Name "displayName" -Default ""
  $url = Get-HashValue -Hash $Tool -Name "fallbackUrl" -Default ""
  $fallbackType = Get-HashValue -Hash $Tool -Name "fallbackType" -Default "manual"

  if ($fallbackType -eq "download" -and [bool](Get-HashValue -Hash $Config -Name "allowFallbackDownloads" -Default $false)) {
    $silentArgs = Get-StringList (Get-HashValue -Hash $Tool -Name "fallbackSilentArgs" -Default @())
    if ([string]::IsNullOrWhiteSpace($url) -or $silentArgs.Count -eq 0) {
      $fallbackType = "manual"
    } else {
      $downloadPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetFileName(([uri]$url).AbsolutePath))
      try {
        Invoke-WebRequest -Uri $url -OutFile $downloadPath -UseBasicParsing
        $process = Start-Process -FilePath $downloadPath -ArgumentList $silentArgs -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -in @(0, 3010, 1641)) {
          if ($process.ExitCode -in @(3010, 1641)) {
            $script:RebootRecommended = $true
          }
          return [pscustomobject]@{ Success = $true; Status = "Installed"; Error = "" }
        }
        return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = "Fallback installer exit code $($process.ExitCode)" }
      } catch {
        return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $_.Exception.Message }
      }
    }
  }

  $message = if ([string]::IsNullOrWhiteSpace($url)) {
    "$name needs manual action: $Reason"
  } else {
    "$name needs manual action: $Reason. Official link: $url"
  }
  $script:ManualActions.Add($message) | Out-Null
  return [pscustomobject]@{ Success = $false; Status = "ManualAction"; Error = $message }
}

function Invoke-WingetInstall {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Action
  )

  $name = Get-HashValue -Hash $Tool -Name "displayName" -Default ""
  if (-not [bool](Get-HashValue -Hash $Config -Name "useWinget" -Default $true)) {
    return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "winget is disabled in config.json"
  }

  $winget = Get-CommandPath "winget"
  if ([string]::IsNullOrWhiteSpace($winget)) {
    return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "winget is not available"
  }

  if ([bool](Get-HashValue -Hash $Tool -Name "adminRequired" -Default $false) -and -not (Test-IsAdmin)) {
    $message = "$name requires an elevated PowerShell session."
    $script:RepairSuggestions.Add($message) | Out-Null
    return [pscustomobject]@{ Success = $false; Status = "NeedsAdmin"; Error = $message }
  }

  $package = Get-PreferredWingetPackage -Tool $Tool
  if ([string]::IsNullOrWhiteSpace($package.Id)) {
    return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "winget package ID was not available"
  }

  $override = Get-WingetOverride -Tool $Tool -Config $Config
  $args = Build-WingetArgs -Action $Action -Id $package.Id -Override $override
  $result = Invoke-CapturedCommand -FilePath $winget -Arguments $args
  if ($result.ExitCode -in @(0, 3010, 1641)) {
    if ($result.ExitCode -in @(3010, 1641) -or [bool](Get-HashValue -Hash $Tool -Name "rebootMayBeRequired" -Default $false)) {
      $script:RebootRecommended = $true
    }
    return [pscustomobject]@{ Success = $true; Status = if ($Action -eq "upgrade") { "Updated" } else { "Installed" }; Error = ""; PackageId = $package.Id }
  }

  return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason ("winget $Action failed: " + (Limit-Text $result.Output 600))
}

function Ensure-Pip {
  $python = Get-CommandPath "python"
  if ([string]::IsNullOrWhiteSpace($python)) {
    return [pscustomobject]@{ Success = $false; Error = "python was not available" }
  }

  $pip = Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "pip", "--version")
  if ($pip.ExitCode -eq 0) {
    return [pscustomobject]@{ Success = $true; Error = "" }
  }

  $ensure = Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "ensurepip", "--upgrade")
  return [pscustomobject]@{ Success = ($ensure.ExitCode -eq 0); Error = $ensure.Output }
}

function Ensure-Pipx {
  param([hashtable]$Config)

  $existing = Get-CommandPath "pipx"
  if (-not [string]::IsNullOrWhiteSpace($existing)) {
    return [pscustomobject]@{ Success = $true; Error = "" }
  }

  $pip = Ensure-Pip
  if (-not $pip.Success) {
    return $pip
  }

  $python = Get-CommandPath "python"
  $install = Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "pip", "install", "--user", "--upgrade", "pipx")
  if ($install.ExitCode -ne 0) {
    return [pscustomobject]@{ Success = $false; Error = $install.Output }
  }

  Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "pipx", "ensurepath") | Out-Null
  Update-CurrentProcessEnvironment
  Add-PythonScriptPaths -Config $Config | Out-Null
  return [pscustomobject]@{ Success = $true; Error = "" }
}

function Invoke-PythonUserPipInstall {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Mode
  )

  $package = Get-HashValue -Hash $Tool -Name "package"
  if ($package -eq "pipx") {
    return Ensure-Pipx -Config $Config
  }

  $python = Get-CommandPath "python"
  if ([string]::IsNullOrWhiteSpace($python)) {
    return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = "python was not available" }
  }

  $result = Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "pip", "install", "--user", "--upgrade", $package)
  if ($result.ExitCode -eq 0) {
    Add-PythonScriptPaths -Config $Config | Out-Null
    return [pscustomobject]@{ Success = $true; Status = "Installed"; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Invoke-PipxInstall {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Mode
  )

  $ensure = Ensure-Pipx -Config $Config
  if (-not $ensure.Success) {
    return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $ensure.Error }
  }

  $python = Get-CommandPath "python"
  $package = Get-HashValue -Hash $Tool -Name "package"
  $arguments = if ($Mode -eq "Update") {
    @("-m", "pipx", "upgrade", $package)
  } else {
    @("-m", "pipx", "install", $package)
  }
  $result = Invoke-CapturedCommand -FilePath $python -Arguments $arguments
  if ($result.ExitCode -eq 0 -or $result.Output -match "already seems to be installed") {
    Add-PythonScriptPaths -Config $Config | Out-Null
    return [pscustomobject]@{ Success = $true; Status = if ($Mode -eq "Update") { "Updated" } else { "Installed" }; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Invoke-NpmGlobalInstall {
  param(
    [hashtable]$Tool,
    [string]$Mode
  )

  $npm = Get-CommandPath "npm"
  if ([string]::IsNullOrWhiteSpace($npm)) {
    return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = "npm was not available" }
  }

  $package = Get-HashValue -Hash $Tool -Name "package"
  $arguments = if ($Mode -eq "Update") { @("update", "-g", $package) } else { @("install", "-g", $package) }
  $result = Invoke-CapturedCommand -FilePath $npm -Arguments $arguments
  if ($result.ExitCode -eq 0) {
    return [pscustomobject]@{ Success = $true; Status = if ($Mode -eq "Update") { "Updated" } else { "Installed" }; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Invoke-DotNetToolInstall {
  param(
    [hashtable]$Tool,
    [string]$Mode
  )

  $dotnet = Get-CommandPath "dotnet"
  if ([string]::IsNullOrWhiteSpace($dotnet)) {
    return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = "dotnet was not available" }
  }

  $package = Get-HashValue -Hash $Tool -Name "package"
  $arguments = if ($Mode -eq "Update") { @("tool", "update", "--global", $package) } else { @("tool", "install", "--global", $package) }
  $result = Invoke-CapturedCommand -FilePath $dotnet -Arguments $arguments
  if ($result.ExitCode -eq 0 -or $result.Output -match "already installed") {
    Add-DotNetToolsPath | Out-Null
    return [pscustomobject]@{ Success = $true; Status = if ($Mode -eq "Update") { "Updated" } else { "Installed" }; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Invoke-PSModuleInstall {
  param(
    [hashtable]$Tool,
    [string]$Mode
  )

  $moduleName = Get-HashValue -Hash $Tool -Name "moduleName" -Default (Get-HashValue -Hash $Tool -Name "package")
  $shell = Get-FreshShell
  if ([string]::IsNullOrWhiteSpace($shell)) {
    return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = "PowerShell was not available" }
  }

  $escapedModule = $moduleName -replace "'", "''"
  $command = @"
`$ErrorActionPreference = 'Stop'
`$moduleName = '$escapedModule'
if (Get-Command Install-PSResource -ErrorAction SilentlyContinue) {
  `$params = @{ Name = `$moduleName; Scope = 'CurrentUser'; TrustRepository = `$true; Reinstall = `$true }
  `$cmd = Get-Command Install-PSResource
  if (`$cmd.Parameters.ContainsKey('AcceptLicense')) { `$params['AcceptLicense'] = `$true }
  if (`$cmd.Parameters.ContainsKey('Quiet')) { `$params['Quiet'] = `$true }
  Install-PSResource @params
} else {
  if (-not (Get-PackageProvider -Name NuGet -ListAvailable -ErrorAction SilentlyContinue)) {
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Scope CurrentUser -Force | Out-Null
  }
  `$params = @{ Name = `$moduleName; Scope = 'CurrentUser'; Force = `$true; AllowClobber = `$true; Repository = 'PSGallery' }
  `$cmd = Get-Command Install-Module
  if (`$cmd.Parameters.ContainsKey('AcceptLicense')) { `$params['AcceptLicense'] = `$true }
  Install-Module @params
}
"@
  $result = Invoke-CapturedCommand -FilePath $shell -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command)
  if ($result.ExitCode -eq 0) {
    return [pscustomobject]@{ Success = $true; Status = if ($Mode -eq "Update") { "Updated" } else { "Installed" }; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Invoke-VSCodeExtensionInstall {
  param([hashtable]$Tool)

  $code = Get-CommandPath "code"
  if ([string]::IsNullOrWhiteSpace($code)) {
    return [pscustomobject]@{ Success = $false; Status = "Skipped"; Error = "VS Code command 'code' was not available" }
  }

  $extensionId = Get-HashValue -Hash $Tool -Name "extensionId"
  $result = Invoke-CapturedCommand -FilePath $code -Arguments @("--install-extension", $extensionId, "--force")
  if ($result.ExitCode -eq 0) {
    return [pscustomobject]@{ Success = $true; Status = "Installed"; Error = "" }
  }

  return [pscustomobject]@{ Success = $false; Status = "Failed"; Error = $result.Output }
}

function Add-PythonScriptPaths {
  param([hashtable]$Config)

  $python = Get-CommandPath "python"
  if ([string]::IsNullOrWhiteSpace($python)) {
    return
  }

  $paths = @()
  $userBase = Invoke-CapturedCommand -FilePath $python -Arguments @("-m", "site", "--user-base")
  if ($userBase.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($userBase.Output)) {
    $paths += (Join-Path $userBase.Output.Trim() "Scripts")
  }
  $paths += (Join-Path $env:USERPROFILE ".local\bin")

  foreach ($path in $paths | Select-Object -Unique) {
    Add-UserPathEntry -Entry $path -Config $Config -Reason "Python user scripts and pipx commands" | Out-Null
  }
}

function Add-DotNetToolsPath {
  $path = Join-Path $env:USERPROFILE ".dotnet\tools"
  $config = @{ fixPathAutomatically = $true }
  return Add-UserPathEntry -Entry $path -Config $config -Reason ".NET global tools"
}

function Add-NpmGlobalPath {
  param([hashtable]$Config)

  $npm = Get-CommandPath "npm"
  if ([string]::IsNullOrWhiteSpace($npm)) {
    return
  }

  $prefix = Invoke-CapturedCommand -FilePath $npm -Arguments @("config", "get", "prefix")
  if ($prefix.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($prefix.Output)) {
    return
  }

  $path = $prefix.Output.Trim()
  Add-UserPathEntry -Entry $path -Config $Config -Reason "npm global commands" | Out-Null
}

function Invoke-ToolInstallByManager {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Mode,
    [bool]$Outdated
  )

  $manager = Get-HashValue -Hash $Tool -Name "manager" -Default ""
  switch ($manager) {
    "winget" {
      $action = if ($Mode -eq "Update" -or $Outdated) { "upgrade" } else { "install" }
      return Invoke-WingetInstall -Tool $Tool -Config $Config -Action $action
    }
    "pythonUserPip" { return Invoke-PythonUserPipInstall -Tool $Tool -Config $Config -Mode $Mode }
    "pipx" { return Invoke-PipxInstall -Tool $Tool -Config $Config -Mode $Mode }
    "npmGlobal" { return Invoke-NpmGlobalInstall -Tool $Tool -Mode $Mode }
    "dotnetTool" { return Invoke-DotNetToolInstall -Tool $Tool -Mode $Mode }
    "psModule" { return Invoke-PSModuleInstall -Tool $Tool -Mode $Mode }
    "vscodeExtension" { return Invoke-VSCodeExtensionInstall -Tool $Tool }
    "manual" { return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "catalog entry is manual by design" }
    "builtin" {
      $key = Get-HashValue -Hash $Tool -Name "key"
      if ($key -eq "pip") {
        $pip = Ensure-Pip
        return [pscustomobject]@{ Success = $pip.Success; Status = if ($pip.Success) { "Installed" } else { "Failed" }; Error = $pip.Error }
      }
      return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "built-in component was missing"
    }
    default { return Invoke-FallbackAction -Tool $Tool -Config $Config -Reason "unknown manager '$manager'" }
  }
}

function Add-ToolResult {
  param(
    [hashtable]$Tool,
    [string]$Status,
    [string]$Mode,
    [string]$CurrentVersion = "",
    [string]$DesiredVersion = "",
    [string]$InstallMethod = "",
    [string]$ValidationStatus = "",
    [string]$ExecutablePath = "",
    [string]$Error = "",
    [string]$ManualAction = ""
  )

  $script:Results.Add([pscustomobject]@{
    Tool = Get-HashValue -Hash $Tool -Name "displayName" -Default ""
    Key = Get-HashValue -Hash $Tool -Name "key" -Default ""
    Category = Get-HashValue -Hash $Tool -Name "category" -Default ""
    Mode = $Mode
    Status = $Status
    CurrentVersion = $CurrentVersion
    DesiredVersion = $DesiredVersion
    InstallMethod = $InstallMethod
    ValidationStatus = $ValidationStatus
    ExecutablePath = $ExecutablePath
    Error = Limit-Text $Error 600
    ManualAction = $ManualAction
  }) | Out-Null
}

function Invoke-ToolProcessing {
  param(
    [hashtable]$Tool,
    [hashtable]$Config,
    [string]$Mode
  )

  $name = Get-HashValue -Hash $Tool -Name "displayName" -Default ""
  $manager = Get-HashValue -Hash $Tool -Name "manager" -Default ""
  $state = Get-ToolState -Tool $Tool
  $desiredVersion = ""
  $outdated = $false

  if ($manager -eq "winget" -and [bool](Get-HashValue -Hash $Config -Name "useWinget" -Default $true)) {
    $package = Get-PreferredWingetPackage -Tool $Tool
    $desiredVersion = $package.Version
    if ($state.Installed) {
      $outdated = Test-WingetUpgradeAvailable -Id $package.Id
    }
  }

  if ($Mode -eq "Preview") {
    $status = if ($state.Installed) { if ($outdated) { "Outdated" } else { "AlreadyPresent" } } else { "Missing" }
    Add-ToolResult -Tool $Tool -Status $status -Mode $Mode -CurrentVersion $state.VersionText -DesiredVersion $desiredVersion -InstallMethod $manager -ValidationStatus $state.Status -ExecutablePath $state.ExecutablePath -Error $state.Error
    return
  }

  $shouldInstall = Should-RunInstallAction -Mode $Mode -State $state.Status -IsOutdated:$outdated
  if ($shouldInstall) {
    Write-Host ("{0,-34} {1}" -f $name, "install/update") -ForegroundColor White
    $install = Invoke-ToolInstallByManager -Tool $Tool -Config $Config -Mode $Mode -Outdated:$outdated
    Update-CurrentProcessEnvironment
    Add-NpmGlobalPath -Config $Config | Out-Null
    $validation = Invoke-ToolValidation -Tool $Tool -FreshShell
    $status = if ($validation.Success) { $install.Status } else { "ValidationFailed" }
    Add-ToolResult -Tool $Tool -Status $status -Mode $Mode -CurrentVersion $validation.VersionText -DesiredVersion $desiredVersion -InstallMethod $manager -ValidationStatus $validation.Status -ExecutablePath $validation.ExecutablePath -Error ($install.Error + " " + $validation.Output).Trim()
    $script:ValidationResults.Add($validation) | Out-Null
  } else {
    $validation = Invoke-ToolValidation -Tool $Tool -FreshShell
    $status = if ($validation.Success) { "AlreadyPresent" } else { $validation.Status }
    if ($Mode -eq "Update" -and -not $state.Installed) {
      $status = "SkippedMissing"
    }
    Add-ToolResult -Tool $Tool -Status $status -Mode $Mode -CurrentVersion $validation.VersionText -DesiredVersion $desiredVersion -InstallMethod $manager -ValidationStatus $validation.Status -ExecutablePath $validation.ExecutablePath -Error $validation.Output
    $script:ValidationResults.Add($validation) | Out-Null
  }
}

function Ensure-JavaHome {
  param(
    [hashtable]$Config,
    [string]$Mode
  )

  if (-not [bool](Get-HashValue -Hash $Config -Name "installJava" -Default $true)) {
    return
  }

  $existing = [Environment]::GetEnvironmentVariable("JAVA_HOME", "User")
  if ([string]::IsNullOrWhiteSpace($existing)) {
    $existing = [Environment]::GetEnvironmentVariable("JAVA_HOME", "Machine")
  }

  if (-not [string]::IsNullOrWhiteSpace($existing)) {
    if (-not (Test-Path -LiteralPath $existing)) {
      $script:RepairSuggestions.Add("JAVA_HOME points to a missing path: $existing") | Out-Null
      Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "ValidationFailed" -Mode $Mode -CurrentVersion $existing -InstallMethod "environment" -ValidationStatus "Invalid" -ExecutablePath $existing -Error "JAVA_HOME points to a missing path."
    } else {
      Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "AlreadyPresent" -Mode $Mode -CurrentVersion $existing -InstallMethod "environment" -ValidationStatus "Installed" -ExecutablePath $existing
    }
    return
  }

  $javac = Get-CommandPath "javac"
  if ([string]::IsNullOrWhiteSpace($javac)) {
    Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "Missing" -Mode $Mode -InstallMethod "environment" -ValidationStatus "Missing" -Error "javac was not available, so JAVA_HOME could not be inferred."
    return
  }

  $bin = Split-Path -Parent $javac
  $root = Split-Path -Parent $bin
  if (-not (Test-Path -LiteralPath $root)) {
    return
  }

  if ($Mode -eq "Preview") {
    $script:RepairSuggestions.Add("JAVA_HOME is not set. Candidate: $root") | Out-Null
    Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "ActionNeeded" -Mode $Mode -InstallMethod "environment" -ValidationStatus "Missing" -Error "JAVA_HOME is not set. Candidate: $root"
    return
  }

  if (([bool](Get-HashValue -Hash $Config -Name "fixPathAutomatically" -Default $false)) -or (Approve-Change -Message "Set user JAVA_HOME to $root?" -Config $Config)) {
    [Environment]::SetEnvironmentVariable("JAVA_HOME", $root, "User")
    $env:JAVA_HOME = $root
    Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "Installed" -Mode $Mode -CurrentVersion $root -InstallMethod "environment" -ValidationStatus "Installed" -ExecutablePath $root
  } else {
    $script:RepairSuggestions.Add("JAVA_HOME is not set. Candidate: $root") | Out-Null
    Add-ToolResult -Tool @{ displayName = "JAVA_HOME"; key = "java-home"; category = "Java" } -Status "ActionNeeded" -Mode $Mode -InstallMethod "environment" -ValidationStatus "Missing" -Error "JAVA_HOME is not set. Candidate: $root"
  }
}

function Invoke-GitConfiguration {
  param(
    [hashtable]$Config,
    [string]$Mode
  )

  $git = Get-CommandPath "git"
  if ([string]::IsNullOrWhiteSpace($git)) {
    return
  }

  $nameResult = Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "--get", "user.name")
  $emailResult = Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "--get", "user.email")
  if ([string]::IsNullOrWhiteSpace($nameResult.Output) -or [string]::IsNullOrWhiteSpace($emailResult.Output)) {
    $script:GitIdentityMissing = $true
    $script:RepairSuggestions.Add("Git user.name or user.email is missing. Configure with: git config --global user.name `"Your Name`" and git config --global user.email `"you@example.com`"") | Out-Null
    Add-ToolResult -Tool @{ displayName = "Git config status"; key = "git-config"; category = "GitHub workflow" } -Status "ActionNeeded" -Mode $Mode -InstallMethod "git config" -ValidationStatus "MissingIdentity" -Error "Git user.name or user.email is missing."
    if ($Mode -ne "Preview" -and -not $Unattended) {
      if ([string]::IsNullOrWhiteSpace($nameResult.Output)) {
        $newName = Read-Host "Git user.name is missing. Enter a name to set it, or press Enter to skip"
        if (-not [string]::IsNullOrWhiteSpace($newName)) {
          Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "user.name", $newName) | Out-Null
        }
      }
      if ([string]::IsNullOrWhiteSpace($emailResult.Output)) {
        $newEmail = Read-Host "Git user.email is missing. Enter an email to set it, or press Enter to skip"
        if (-not [string]::IsNullOrWhiteSpace($newEmail)) {
          Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "user.email", $newEmail) | Out-Null
        }
      }
    }
  } else {
    Add-ToolResult -Tool @{ displayName = "Git config status"; key = "git-config"; category = "GitHub workflow" } -Status "AlreadyPresent" -Mode $Mode -CurrentVersion "$($nameResult.Output.Trim()) <$($emailResult.Output.Trim())>" -InstallMethod "git config" -ValidationStatus "Installed"
  }

  if ([bool](Get-HashValue -Hash $Config -Name "configureGitLongPaths" -Default $true)) {
    $longPaths = Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "--get", "core.longpaths")
    if ($longPaths.Output -ne "true" -and $Mode -ne "Preview") {
      Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "core.longpaths", "true") | Out-Null
      $longPaths = Invoke-CapturedCommand -FilePath $git -Arguments @("config", "--global", "--get", "core.longpaths")
    }
    $script:GitLongPathsEnabled = ($longPaths.Output -eq "true")
    Add-ToolResult -Tool @{ displayName = "Git longpaths"; key = "git-longpaths"; category = "GitHub workflow" } -Status $(if ($script:GitLongPathsEnabled) { "AlreadyPresent" } else { "ActionNeeded" }) -Mode $Mode -CurrentVersion $longPaths.Output -InstallMethod "git config" -ValidationStatus $(if ($script:GitLongPathsEnabled) { "Installed" } else { "NeedsAttention" }) -Error $(if ($script:GitLongPathsEnabled) { "" } else { "core.longpaths is not enabled." })
  }

  $gh = Get-CommandPath "gh"
  if (-not [string]::IsNullOrWhiteSpace($gh)) {
    $auth = Invoke-CapturedCommand -FilePath $gh -Arguments @("auth", "status")
    if ($auth.ExitCode -ne 0) {
      $script:GitHubLoginNeeded = $true
      $script:ManualActions.Add("GitHub CLI is not authenticated. Run: gh auth login") | Out-Null
      Add-ToolResult -Tool @{ displayName = "GitHub auth status"; key = "gh-auth"; category = "GitHub workflow" } -Status "ActionNeeded" -Mode $Mode -InstallMethod "gh auth" -ValidationStatus "NotAuthenticated" -Error "Run: gh auth login"
    } else {
      Add-ToolResult -Tool @{ displayName = "GitHub auth status"; key = "gh-auth"; category = "GitHub workflow" } -Status "AlreadyPresent" -Mode $Mode -InstallMethod "gh auth" -ValidationStatus "Authenticated"
    }
  } else {
    Add-ToolResult -Tool @{ displayName = "GitHub auth status"; key = "gh-auth"; category = "GitHub workflow" } -Status "Missing" -Mode $Mode -InstallMethod "gh auth" -ValidationStatus "Missing" -Error "GitHub CLI was not available."
  }
}

function Invoke-RequiredPathValidations {
  $required = @("python", "py", "pip", "pipx", "uv", "git", "gh", "dotnet", "java", "javac", "node", "npm", "pnpm", "code", "rg", "fd", "jq")
  foreach ($tool in $required) {
    $result = Invoke-FreshShellCommand -Executable $tool -Arguments @("--version")
    if ($tool -in @("java", "javac")) {
      $result = Invoke-FreshShellCommand -Executable $tool -Arguments @("-version")
    }
    $script:ValidationResults.Add([pscustomobject]@{
      Tool = "PATH:$tool"
      Success = ($result.ExitCode -eq 0)
      Status = if ($result.ExitCode -eq 0) { "OnPath" } else { "MissingFromPath" }
      ExecutablePath = $result.ExecutablePath
      VersionText = Get-FirstMeaningfulLine $result.Output
      Output = Limit-Text $result.Output
      ExitCode = $result.ExitCode
    }) | Out-Null

    if ($result.ExitCode -ne 0) {
      $script:RepairSuggestions.Add("$tool was not available from a fresh PowerShell process.") | Out-Null
    }
  }
}

function New-FileIfChanged {
  param(
    [string]$Path,
    [string]$Content
  )

  $parent = Split-Path -Parent $Path
  New-Item -ItemType Directory -Path $parent -Force | Out-Null
  if ((Test-Path -LiteralPath $Path) -and ((Get-Content -LiteralPath $Path -Raw) -eq $Content)) {
    return
  }
  Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
}

function Invoke-ValidationProjects {
  param(
    [hashtable]$Config,
    [string]$Root
  )

  New-Item -ItemType Directory -Path $Root -Force | Out-Null

  if (-not [string]::IsNullOrWhiteSpace((Get-CommandPath "python"))) {
    $project = Join-Path $Root "python_hello"
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    New-FileIfChanged -Path (Join-Path $project "app.py") -Content @'
from pydantic import BaseModel
from rich import print
import requests
import typer


class Message(BaseModel):
    text: str


def main() -> None:
    message = Message(text="hello from python")
    print(message.text)
    assert requests.__version__


if __name__ == "__main__":
    typer.run(main)
'@
    New-FileIfChanged -Path (Join-Path $project "test_app.py") -Content @'
from app import Message


def test_message() -> None:
    assert Message(text="ok").text == "ok"
'@
    $venv = Join-Path $project ".venv"
    $python = Get-CommandPath "python"
    $steps = @(
      @{ Name = "venv"; Command = $python; Args = @("-m", "venv", $venv) },
      @{ Name = "pip"; Command = (Join-Path $venv "Scripts\python.exe"); Args = @("-m", "pip", "install", "--upgrade", "pip") },
      @{ Name = "deps"; Command = (Join-Path $venv "Scripts\python.exe"); Args = @("-m", "pip", "install", "requests", "rich", "typer", "pydantic", "pytest") },
      @{ Name = "pytest"; Command = (Join-Path $venv "Scripts\python.exe"); Args = @("-m", "pytest", "-q") },
      @{ Name = "run"; Command = (Join-Path $venv "Scripts\python.exe"); Args = @("app.py") }
    )
    Invoke-ValidationProjectSteps -Name "python_hello" -ProjectPath $project -Steps $steps
  }

  if (-not [string]::IsNullOrWhiteSpace((Get-CommandPath "dotnet"))) {
    $project = Join-Path $Root "dotnet_hello"
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    New-FileIfChanged -Path (Join-Path $project ".editorconfig") -Content @'
root = true

[*.cs]
dotnet_analyzer_diagnostic.category-Style.severity = warning
dotnet_analyzer_diagnostic.category-Performance.severity = warning
dotnet_analyzer_diagnostic.category-Reliability.severity = warning
'@
    New-FileIfChanged -Path (Join-Path $project "Directory.Build.props") -Content @'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest</AnalysisLevel>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
'@
    $dotnet = Get-CommandPath "dotnet"
    $steps = @(
      @{ Name = "new-console"; Command = $dotnet; Args = @("new", "console", "-n", "DotnetHello", "-o", (Join-Path $project "DotnetHello"), "--force") },
      @{ Name = "build"; Command = $dotnet; Args = @("build", (Join-Path $project "DotnetHello")) },
      @{ Name = "new-test"; Command = $dotnet; Args = @("new", "xunit", "-n", "DotnetHello.Tests", "-o", (Join-Path $project "DotnetHello.Tests"), "--force") },
      @{ Name = "test"; Command = $dotnet; Args = @("test", (Join-Path $project "DotnetHello.Tests")) }
    )
    Invoke-ValidationProjectSteps -Name "dotnet_hello" -ProjectPath $project -Steps $steps
  }

  if (-not [string]::IsNullOrWhiteSpace((Get-CommandPath "javac")) -and -not [string]::IsNullOrWhiteSpace((Get-CommandPath "java"))) {
    $project = Join-Path $Root "java_hello"
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    New-FileIfChanged -Path (Join-Path $project "Hello.java") -Content @'
public class Hello {
    public static void main(String[] args) {
        System.out.println("hello from java");
    }
}
'@
    $steps = @(
      @{ Name = "javac"; Command = (Get-CommandPath "javac"); Args = @((Join-Path $project "Hello.java")) },
      @{ Name = "java"; Command = (Get-CommandPath "java"); Args = @("-cp", $project, "Hello") }
    )
    Invoke-ValidationProjectSteps -Name "java_hello" -ProjectPath $project -Steps $steps
  }

  if (-not [string]::IsNullOrWhiteSpace((Get-CommandPath "node")) -and -not [string]::IsNullOrWhiteSpace((Get-CommandPath "tsc"))) {
    $project = Join-Path $Root "node_typescript_hello"
    New-Item -ItemType Directory -Path (Join-Path $project "src") -Force | Out-Null
    New-FileIfChanged -Path (Join-Path $project "src\index.ts") -Content @'
type Message = {
  text: string;
};

const message: Message = { text: "hello from typescript" };
console.log(message.text);
'@
    New-FileIfChanged -Path (Join-Path $project "tsconfig.json") -Content @'
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "CommonJS",
    "strict": true,
    "outDir": "dist",
    "rootDir": "src"
  },
  "include": ["src/**/*.ts"]
}
'@
    $steps = @(
      @{ Name = "tsc"; Command = (Get-CommandPath "tsc"); Args = @("-p", $project) },
      @{ Name = "node"; Command = (Get-CommandPath "node"); Args = @((Join-Path $project "dist\index.js")) }
    )
    Invoke-ValidationProjectSteps -Name "node_typescript_hello" -ProjectPath $project -Steps $steps
  }
}

function Invoke-ValidationProjectSteps {
  param(
    [string]$Name,
    [string]$ProjectPath,
    [array]$Steps
  )

  $stepRows = New-Object System.Collections.Generic.List[object]
  Push-Location $ProjectPath
  try {
    foreach ($step in $Steps) {
      $result = Invoke-CapturedCommand -FilePath $step.Command -Arguments ([string[]]$step.Args)
      $stepRows.Add([pscustomobject]@{
        Step = $step.Name
        ExitCode = $result.ExitCode
        Success = ($result.ExitCode -eq 0)
        Output = Limit-Text $result.Output 800
      }) | Out-Null
      if ($result.ExitCode -ne 0) {
        break
      }
    }
  } finally {
    Pop-Location
  }

  $success = -not ($stepRows | Where-Object { -not $_.Success } | Select-Object -First 1)
  $script:ValidationProjectResults.Add([pscustomobject]@{
    Name = $Name
    Path = $ProjectPath
    Success = $success
    Steps = @($stepRows)
  }) | Out-Null
}

function Get-RequestedMode {
  if ($RunValidationProjectsOnly) {
    return "ValidateProjects"
  }

  $modes = @()
  if ($Preview) { $modes += "Preview" }
  if ($Apply) { $modes += "Apply" }
  if ($Repair) { $modes += "Repair" }
  if ($Update) { $modes += "Update" }
  if ($modes.Count -gt 1) {
    throw "Choose only one mode: -Preview, -Apply, -Repair, or -Update."
  }
  if ($modes.Count -eq 1) {
    return $modes[0]
  }
  if ($Unattended) {
    return "Preview"
  }

  Write-Host ""
  Write-Host "Windows Developer Tool Bootstrapper" -ForegroundColor Cyan
  Write-Host "1. Preview"
  Write-Host "2. Apply"
  Write-Host "3. Repair"
  Write-Host "4. Update"
  Write-Host "Q. Quit"
  $choice = Read-Host "Select an action"
  switch ($choice) {
    "1" { return "Preview" }
    "2" { return "Apply" }
    "3" { return "Repair" }
    "4" { return "Update" }
    default { throw "No action selected." }
  }
}

function Confirm-Mode {
  param([string]$Mode)

  if ($Mode -in @("Preview", "ValidateProjects") -or $Unattended) {
    return
  }
  $answer = Read-Host "Run in $Mode mode and make changes where needed? [y/N]"
  if ($answer -notmatch "^(y|yes)$") {
    throw "Cancelled."
  }
}

function Write-ReportFiles {
  param(
    [hashtable]$Config,
    [string]$Mode,
    [string]$SelectedTier,
    [string]$OutputFolder,
    [array]$PathAudit
  )

  New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

  $installedStatuses = @("AlreadyPresent", "Installed", "Updated", "Outdated")
  $installed = @($script:Results | Where-Object { $_.Status -in $installedStatuses })
  $failed = @($script:Results | Where-Object { $_.Status -notin $installedStatuses })
  $failedValidations = @($script:ValidationResults | Where-Object { -not $_.Success })
  $failedProjects = @($script:ValidationProjectResults | Where-Object { -not $_.Success })
  $brokenPath = @($PathAudit | Where-Object { -not $_.Exists })
  $duplicatePath = @($PathAudit | Where-Object { $_.Duplicate })
  $ready = ($failed.Count -eq 0 -and $failedValidations.Count -eq 0 -and $failedProjects.Count -eq 0)

  $summary = New-Object System.Collections.Generic.List[string]
  $summary.Add("# Windows Developer Tool Setup Summary") | Out-Null
  $summary.Add("") | Out-Null
  $summary.Add("- Date: $(Get-Date -Format s)") | Out-Null
  $summary.Add("- Mode: $Mode") | Out-Null
  $summary.Add("- Tier: $SelectedTier") | Out-Null
  $summary.Add("- Verdict: " + $(if ($ready) { "READY" } else { "NOT READY" })) | Out-Null
  $summary.Add("- Reboot recommended: $script:RebootRecommended") | Out-Null
  $summary.Add("- GitHub login needed: $script:GitHubLoginNeeded") | Out-Null
  $summary.Add("- Git identity missing: $script:GitIdentityMissing") | Out-Null
  $summary.Add("") | Out-Null
  $summary.Add("## Counts") | Out-Null
  $summary.Add("") | Out-Null
  $summary.Add("- Installed/already present/updated: $($installed.Count)") | Out-Null
  $summary.Add("- Missing/failed/skipped/manual action: $($failed.Count)") | Out-Null
  $summary.Add("- Validation failures: $($failedValidations.Count)") | Out-Null
  $summary.Add("- Validation project failures: $($failedProjects.Count)") | Out-Null
  $summary.Add("- Broken PATH entries: $($brokenPath.Count)") | Out-Null
  $summary.Add("- Duplicate PATH entries: $($duplicatePath.Count)") | Out-Null
  $summary.Add("") | Out-Null

  if ($script:ManualActions.Count -gt 0) {
    $summary.Add("## Manual Actions") | Out-Null
    $summary.Add("") | Out-Null
    foreach ($action in ($script:ManualActions | Select-Object -Unique)) {
      $summary.Add("- $action") | Out-Null
    }
    $summary.Add("") | Out-Null
  }

  if ($script:ValidationProjectResults.Count -gt 0) {
    $summary.Add("## Validation Projects") | Out-Null
    $summary.Add("") | Out-Null
    foreach ($project in $script:ValidationProjectResults) {
      $summary.Add("- $($project.Name): $(if ($project.Success) { "passed" } else { "failed" }) at $($project.Path)") | Out-Null
    }
    $summary.Add("") | Out-Null
  }

  $summary.Add("## Next Commands") | Out-Null
  $summary.Add("") | Out-Null
  if ($script:GitHubLoginNeeded) {
    $summary.Add('- `gh auth login`') | Out-Null
  }
  if ($script:GitIdentityMissing) {
    $summary.Add('- `git config --global user.name "Your Name"`') | Out-Null
    $summary.Add('- `git config --global user.email "you@example.com"`') | Out-Null
  }
  if ($script:RebootRecommended) {
    $summary.Add('- Restart Windows, then rerun `.\setup-devtools.ps1 -Repair`') | Out-Null
  }
  $summary.Add('- Open a new PowerShell window and run `.\setup-devtools.ps1 -Preview` to confirm the final state.') | Out-Null

  Set-Content -LiteralPath (Join-Path $OutputFolder "summary.md") -Value $summary -Encoding UTF8
  $installed | Export-Csv -LiteralPath (Join-Path $OutputFolder "installed_tools.csv") -NoTypeInformation
  $failed | Export-Csv -LiteralPath (Join-Path $OutputFolder "failed_tools.csv") -NoTypeInformation

  $validationJson = [ordered]@{
    mode = $Mode
    tier = $SelectedTier
    ready = $ready
    rebootRecommended = $script:RebootRecommended
    githubLoginNeeded = $script:GitHubLoginNeeded
    results = @($script:Results.ToArray())
    validations = @($script:ValidationResults.ToArray())
    validationProjects = @($script:ValidationProjectResults.ToArray())
    pathAudit = @($PathAudit)
  }
  $validationJson | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputFolder "validation_results.json") -Encoding UTF8
  $validationJson | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputFolder "gui_last_scan.json") -Encoding UTF8

  $repair = New-Object System.Collections.Generic.List[string]
  $repair.Add("# Repair Suggestions") | Out-Null
  $repair.Add("") | Out-Null
  foreach ($item in ($script:RepairSuggestions | Select-Object -Unique)) {
    $repair.Add("- $item") | Out-Null
  }
  foreach ($entry in $brokenPath) {
    $repair.Add("- Broken PATH entry [$($entry.Scope)]: $($entry.Entry)") | Out-Null
  }
  foreach ($entry in $duplicatePath) {
    $repair.Add("- Duplicate PATH entry [$($entry.Scope)]: $($entry.Entry)") | Out-Null
  }
  if ($repair.Count -eq 2) {
    $repair.Add("- No repair suggestions were generated.") | Out-Null
  }
  Set-Content -LiteralPath (Join-Path $OutputFolder "repair_suggestions.md") -Value $repair -Encoding UTF8

  return [pscustomobject]@{
    Ready = $ready
    InstalledCount = $installed.Count
    FailedCount = $failed.Count
    ValidationFailureCount = $failedValidations.Count
    OutputFolder = $OutputFolder
  }
}

function Invoke-SelfTest {
  $failures = New-Object System.Collections.Generic.List[string]

  if ((Add-PathEntryToList -PathValue "C:\Tools;C:\Windows" -Entry "c:\tools\") -ne "C:\Tools;C:\Windows") {
    $failures.Add("PATH de-duplication failed") | Out-Null
  }
  if ((Add-PathEntryToList -PathValue "C:\Tools" -Entry "C:\MoreTools") -ne "C:\Tools;C:\MoreTools") {
    $failures.Add("PATH append failed") | Out-Null
  }
  if ((Get-VersionToken "Python 3.14.1") -ne "3.14.1") {
    $failures.Add("Version parsing failed") | Out-Null
  }
  $wingetArgs = Build-WingetArgs -Action "install" -Id "Git.Git"
  if (-not ($wingetArgs -contains "--id") -or -not ($wingetArgs -contains "Git.Git")) {
    $failures.Add("winget argument construction failed") | Out-Null
  }
  if (Should-RunInstallAction -Mode "Apply" -State "Installed" -IsOutdated:$false) {
    $failures.Add("Idempotent apply decision failed") | Out-Null
  }
  if (-not (Should-RunInstallAction -Mode "Apply" -State "Missing" -IsOutdated:$false)) {
    $failures.Add("Missing apply decision failed") | Out-Null
  }
  $config = Get-EffectiveConfig -Path ".\config.json"
  if ([string](Get-HashValue -Hash $config -Name "installTier") -notin @("Core", "Recommended", "Full")) {
    $failures.Add("Config loading failed") | Out-Null
  }
  $catalog = Get-ToolCatalog -Path ".\tool-catalog.json"
  if (@($catalog).Count -lt 10) {
    $failures.Add("Catalog loading failed") | Out-Null
  }

  $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("devtools-report-test-" + [guid]::NewGuid().ToString("N"))
  try {
    $fakeTool = @{ displayName = "Fake"; key = "fake"; category = "test" }
    Add-ToolResult -Tool $fakeTool -Status "AlreadyPresent" -Mode "Preview"
    $report = Write-ReportFiles -Config $script:DefaultConfig -Mode "Preview" -SelectedTier "Core" -OutputFolder $temp -PathAudit @()
    if (-not (Test-Path -LiteralPath (Join-Path $temp "summary.md"))) {
      $failures.Add("Report generation failed") | Out-Null
    }
  } finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    $script:Results.Clear()
  }

  if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
      Write-Bad "Self-test failed: $failure"
    }
    return $false
  }

  Write-Good "Self-test passed."
  return $true
}

function Invoke-DevToolsSetup {
  $mode = Get-RequestedMode
  Confirm-Mode -Mode $mode

  $config = Get-EffectiveConfig -Path $ConfigPath
  $catalog = Get-ToolCatalog -Path $CatalogPath
  $selectedTier = [string](Get-HashValue -Hash $config -Name "installTier" -Default "Recommended")
  $tierWasExplicit = $script:TierWasProvided
  $outputFolder = if (-not [string]::IsNullOrWhiteSpace($ReportDir)) {
    Resolve-ProjectPath -Path $ReportDir
  } else {
    Resolve-ProjectPath -Path ([string](Get-HashValue -Hash $config -Name "reportOutputFolder" -Default "devtools_setup_report"))
  }
  $validationRoot = Resolve-ProjectPath -Path ([string](Get-HashValue -Hash $config -Name "validationProjectsFolder" -Default "devtools_validation_projects"))

  Write-Info "Windows developer tool setup"
  Write-Host "Mode: $mode"
  Write-Host "Tier: $selectedTier"
  Write-Host "PowerShell: $($PSVersionTable.PSVersion)"
  Write-Host "Admin: $(Test-IsAdmin)"
  Write-Host "Report: $outputFolder"
  Write-Host ""

  if ($mode -eq "ValidateProjects") {
    Update-CurrentProcessEnvironment
    Invoke-ValidationProjects -Config $config -Root $validationRoot
    Invoke-RequiredPathValidations
    $pathAuditOnly = @(Get-PathAudit)
    $reportOnly = Write-ReportFiles -Config $config -Mode $mode -SelectedTier $selectedTier -OutputFolder $outputFolder -PathAudit $pathAuditOnly
    Write-Host ""
    if ($reportOnly.Ready) {
      Write-Good "Verdict: READY"
    } else {
      Write-Warn "Verdict: NOT READY"
    }
    Write-Host "Summary: $(Join-Path $outputFolder "summary.md")"
    return
  }

  $os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
  if ($null -ne $os -and $os.Caption -notmatch "Windows 11") {
    Write-Warn "Target platform is Windows 11; detected: $($os.Caption)"
  }

  $pathAuditBefore = @(Get-PathAudit)
  $brokenBefore = @($pathAuditBefore | Where-Object { -not $_.Exists })
  if ($brokenBefore.Count -gt 0) {
    Write-Warn "Detected $($brokenBefore.Count) broken PATH entries. Details will be in the report."
  }

  $selectedTools = @($catalog | Where-Object { Test-ToolSelected -Tool $_ -Config $config -SelectedTier $selectedTier -TierWasExplicit:$tierWasExplicit })
  $normalizedToolKeys = @($ToolKeys | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  if ($normalizedToolKeys.Count -gt 0) {
    $keySet = @{}
    foreach ($key in $normalizedToolKeys) {
      $keySet[$key] = $true
    }
    $selectedTools = @($selectedTools | Where-Object { $keySet.ContainsKey([string](Get-HashValue -Hash $_ -Name "key" -Default "")) })
  }
  $normalizedCategories = @($Categories | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  if ($normalizedCategories.Count -gt 0) {
    $categorySet = @{}
    foreach ($category in $normalizedCategories) {
      $categorySet[$category] = $true
    }
    $selectedTools = @($selectedTools | Where-Object { $categorySet.ContainsKey([string](Get-HashValue -Hash $_ -Name "category" -Default "")) })
  }
  $count = $selectedTools.Count
  $index = 0

  foreach ($tool in $selectedTools) {
    $index++
    $name = Get-HashValue -Hash $tool -Name "displayName" -Default ""
    Write-Progress -Activity "Developer tool setup" -Status $name -PercentComplete (($index / [Math]::Max($count, 1)) * 100)
    Invoke-ToolProcessing -Tool $tool -Config $config -Mode $mode
  }
  Write-Progress -Activity "Developer tool setup" -Completed

  Update-CurrentProcessEnvironment
  Add-PythonScriptPaths -Config $config | Out-Null
  Add-NpmGlobalPath -Config $config | Out-Null
  Ensure-JavaHome -Config $config -Mode $mode
  Invoke-GitConfiguration -Config $config -Mode $mode
  Invoke-RequiredPathValidations

  if ($mode -ne "Preview" -and [bool](Get-HashValue -Hash $config -Name "createValidationProjects" -Default $true)) {
    Invoke-ValidationProjects -Config $config -Root $validationRoot
  }

  $pathAuditAfter = @(Get-PathAudit)
  $report = Write-ReportFiles -Config $config -Mode $mode -SelectedTier $selectedTier -OutputFolder $outputFolder -PathAudit $pathAuditAfter

  Write-Host ""
  if ($report.Ready) {
    Write-Good "Verdict: READY"
  } else {
    Write-Warn "Verdict: NOT READY"
  }
  Write-Host "Installed/already present/updated: $($report.InstalledCount)"
  Write-Host "Missing/failed/skipped/manual action: $($report.FailedCount)"
  Write-Host "Validation failures: $($report.ValidationFailureCount)"
  Write-Host "Summary: $(Join-Path $outputFolder "summary.md")"
}

if (-not $LoadOnly) {
  if ($Gui) {
    $releaseExe = Join-Path $PSScriptRoot "release\DevKit\DevKit.exe"
    $appProject = Join-Path $PSScriptRoot "src\DevToolsCurator.App\DevToolsCurator.App.csproj"
    $appExe = Join-Path $PSScriptRoot "src\DevToolsCurator.App\bin\Debug\net10.0-windows\DevKit.exe"
    $releaseBuilder = Join-Path $PSScriptRoot "build-release.ps1"
    $dotnet = Get-CommandPath "dotnet"
    if ([string]::IsNullOrWhiteSpace($dotnet)) {
      $programFilesDotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
      if (Test-Path -LiteralPath $programFilesDotnet) {
        $dotnet = $programFilesDotnet
      }
    }

    if (Test-Path -LiteralPath $releaseExe) {
      Start-Process -FilePath $releaseExe -WorkingDirectory (Split-Path -Parent $releaseExe)
    } elseif (Test-Path -LiteralPath $releaseBuilder) {
      & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $releaseBuilder
      if (Test-Path -LiteralPath $releaseExe) {
        Start-Process -FilePath $releaseExe -WorkingDirectory (Split-Path -Parent $releaseExe)
      } else {
        throw "Release build completed but DevKit.exe was not found at: $releaseExe"
      }
    } elseif (-not [string]::IsNullOrWhiteSpace($dotnet) -and (Test-Path -LiteralPath $appProject)) {
      & $dotnet "build" $appProject
      if (Test-Path -LiteralPath $appExe) {
        Start-Process -FilePath $appExe -WorkingDirectory (Split-Path -Parent $appExe)
      } else {
        & $dotnet "run" "--project" $appProject
      }
    } else {
      throw "DevKit GUI is unavailable. Build it with: .\build-release.ps1"
    }
  } elseif ($SelfTest) {
    $ok = Invoke-SelfTest
    if (-not $ok) {
      exit 1
    }
  } else {
    Invoke-DevToolsSetup
  }
}
