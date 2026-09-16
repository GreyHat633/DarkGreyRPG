[CmdletBinding()]
param(
    [string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGreyRPG"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

function Resolve-FullPath([string]$Path) {
    [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

function Assert-UnderRoot([string]$Path, [string]$Root, [string]$Label) {
    $full = [IO.Path]::GetFullPath($Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label is outside the permitted subtree: $full"
    }
    $full
}

function Find-ByAutomationId(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId,
    [int]$TimeoutSeconds = 15) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "AutomationId '$AutomationId' was not found."
}

function Find-Window([int[]]$ExcludedProcessIds, [int]$TimeoutSeconds = 20) {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($window in $desktop.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($window.Current.Name -like 'DarkGrey RPG Studio*' -and
                $window.Current.ProcessId -notin $ExcludedProcessIds) { return $window }
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'The newly launched Studio window was not found.'
}

function Invoke-Element([System.Windows.Automation.AutomationElement]$Element) {
    $pattern = $null
    if (-not $Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        throw "'$($Element.Current.Name)' does not expose InvokePattern."
    }
    $pattern.Invoke()
}

function Get-LegacyPaths([string]$ProjectRoot) {
    $paths = @((Join-Path $ProjectRoot 'project.json'))
    foreach ($folder in @('actors', 'dialogues', 'quests', 'stories')) {
        $paths += @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -File -Recurse | ForEach-Object FullName)
    }
    @($paths | Sort-Object)
}

function Get-LegacySnapshot([string]$ProjectRoot) {
    $snapshot = [ordered]@{}
    foreach ($path in (Get-LegacyPaths $ProjectRoot)) {
        $relative = [IO.Path]::GetRelativePath($ProjectRoot, $path).Replace('\', '/')
        $snapshot[$relative] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $snapshot | ConvertTo-Json -Compress
}

function Save-WindowScreenshot(
    [System.Windows.Automation.AutomationElement]$Window,
    [string]$Destination) {
    $rect = $Window.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw 'Migration dialog has no capturable bounds.' }
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bitmap.Size)
        $bitmap.Save($Destination, [Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$repo = Resolve-FullPath $RepositoryRoot
$tooling = Assert-UnderRoot (Join-Path $repo '.tooling') $repo 'Tooling root'
$source = Assert-UnderRoot (Join-Path $tooling '2.1-acceptance\DarkGrey-2.1-Acceptance') $tooling 'Source fixture'
$work = Assert-UnderRoot (Join-Path $tooling 'stage6\migration-ui-live') $tooling 'QA work root'
$project = Assert-UnderRoot (Join-Path $work 'DarkGrey-2.1-Acceptance') $work 'QA project'
$settingsPath = Assert-UnderRoot (Join-Path $work 'settings.json') $work 'QA settings'
$screenshotPath = Assert-UnderRoot (Join-Path $work 'migration-preview.png') $work 'QA screenshot'
$studioDll = Join-Path $repo 'studio\src\DarkGreyRPG.Studio\bin\Release\net10.0-windows\DarkGreyRPGStudio.dll'
$dotnet = 'E:\Java\dotnet-sdk-10\dotnet.exe'

foreach ($required in @($source, $studioDll, $dotnet)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required input is missing: $required" }
}

if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null
Copy-Item -LiteralPath $source -Destination $project -Recurse
$settings = Get-Content -LiteralPath (Join-Path $tooling '2.1-acceptance\settings.json') -Raw | ConvertFrom-Json
$settings.last_project = $project
$settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath -Encoding utf8
$legacyBefore = Get-LegacySnapshot $project

$previousSettingsPath = $env:DARKGREYRPG_STUDIO_SETTINGS_PATH
$process = $null
$window = $null
try {
    $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $existingStudioPids = @($desktop.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition) | Where-Object {
            $_.Current.Name -like 'DarkGrey RPG Studio*'
        } | ForEach-Object { $_.Current.ProcessId })
    $process = Start-Process -FilePath $dotnet -ArgumentList @($studioDll) -WorkingDirectory $repo -PassThru
    $window = Find-Window $existingStudioPids

    $projectMenuCondition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty, '项目(P)'),
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::MenuItem))
    $projectMenu = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $projectMenuCondition)
    if ($null -eq $projectMenu) { throw "Project menu was not found in '$($window.Current.Name)'." }
    $expand = $null
    if ($projectMenu.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) {
        $expand.Expand()
    } else {
        Invoke-Element $projectMenu
    }
    $migrationMenu = Find-ByAutomationId ([System.Windows.Automation.AutomationElement]::RootElement) 'MigrateCanonicalProject'
    Invoke-Element $migrationMenu

    $dialog = Find-ByAutomationId ([System.Windows.Automation.AutomationElement]::RootElement) 'CanonicalProjectMigrationDialog'
    $confirm = Find-ByAutomationId $dialog 'CanonicalProjectMigrationConfirm'
    $all = @($dialog.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition))
    $names = @($all | ForEach-Object { $_.Current.Name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if (-not $confirm.Current.IsEnabled) { throw 'Confirm must be enabled for the applicable tracked project.' }
    if (-not ($names | Where-Object { $_ -like '源文件：*' })) { throw 'Rendered source count was not found.' }
    if (-not ($names | Where-Object { $_ -like '资源：*' })) { throw 'Rendered resource count was not found.' }
    if (-not ($names | Where-Object { $_ -eq '拟写入：10' })) { throw 'Rendered write count was not 10.' }
    if ($names -match '\{Binding') { throw 'A preview statistic rendered an unevaluated binding expression.' }

    Save-WindowScreenshot $dialog $screenshotPath
    Invoke-Element $confirm

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $remaining = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                'CanonicalProjectMigrationDialog'))
        if ($null -eq $remaining -and (Test-Path -LiteralPath (Join-Path $project 'migration.log'))) { break }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -ne $remaining) { throw 'Migration dialog remained open after confirmation.' }

    if ((Get-LegacySnapshot $project) -ne $legacyBefore) { throw 'Confirmed migration changed legacy project bytes.' }
    $canonicalRoot = Join-Path $project 'resources\canonical'
    $canonicalFiles = @(Get-ChildItem -LiteralPath $canonicalRoot -File -Recurse | Sort-Object FullName)
    if ($canonicalFiles.Count -ne 10) { throw "Expected 10 canonical files, found $($canonicalFiles.Count)." }
    foreach ($file in $canonicalFiles) {
        $null = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    }
    $backupRoot = Join-Path $project '.migration-backups'
    $backups = @(Get-ChildItem -LiteralPath $backupRoot -Directory)
    if ($backups.Count -ne 1) { throw "Expected one complete migration backup, found $($backups.Count)." }
    foreach ($sourcePath in (Get-LegacyPaths $project)) {
        $relative = [IO.Path]::GetRelativePath($project, $sourcePath)
        $backupPath = Join-Path $backups[0].FullName $relative
        if (-not (Test-Path -LiteralPath $backupPath)) { throw "Backup is missing legacy source: $relative" }
        $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
        $backupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
        if ($sourceHash -ne $backupHash) { throw "Backup hash mismatch: $relative" }
    }
    $log = Get-Content -LiteralPath (Join-Path $project 'migration.log') -Raw
    if ($log -notmatch 'succeeded; files=10; backup=') {
        throw 'Migration success log did not record 10 written files and the backup path.'
    }
    foreach ($required in @(
        'resources\canonical\stories\royal_mystery.json',
        'resources\canonical\sessions\final_confrontation.json',
        'resources\canonical\tasks\evidence.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $project $required))) {
            throw "Required canonical output is missing: $required"
        }
    }

    Write-Host 'MIGRATION_UI_REAL_WINDOW=PASS'
    Write-Host 'MIGRATION_UI_TRACKED_PROJECT_APPLY=PASS'
    Write-Host 'MIGRATION_UI_LEGACY_BYTES_PRESERVED=PASS'
    Write-Host 'MIGRATION_UI_CANONICAL_FILES_10=PASS'
    Write-Host 'MIGRATION_UI_COMPLETE_BACKUP=PASS'
    Write-Host 'MIGRATION_UI_SUCCESS_LOG=PASS'
    Write-Host "MIGRATION_UI_SCREENSHOT=$screenshotPath"
} finally {
    if ($null -ne $window) {
        try {
            $windowPattern = $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $windowPattern.Close()
        } catch { }
    }
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(5000) }
    }
    if ($null -eq $previousSettingsPath) {
        Remove-Item Env:DARKGREYRPG_STUDIO_SETTINGS_PATH -ErrorAction SilentlyContinue
    } else {
        $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $previousSettingsPath
    }
}
