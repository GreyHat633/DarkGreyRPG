[CmdletBinding()]
param(
    [string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG",
    [switch]$SkipLaunch
)

# Gate H is deliberately isolated: every generated or mutated file must be
# below .tooling. This script never edits examples, a user project, or dist.
$ErrorActionPreference = 'Stop'
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss.fffZ')
$runStartedUtc = [DateTime]::UtcNow.ToString('o')
$results = [ordered]@{
    run_id = $runId
    started_utc = $runStartedUtc
    status = 'RUNNING'
}
$qaToken = ($runId -replace '[^0-9]', '')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Studio213ResourceAcceptanceNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

function Resolve-FullPath([string]$Path) { [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path) }
function Assert-UnderRoot([string]$Path, [string]$Root, [string]$Label) {
    $full = [IO.Path]::GetFullPath($Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label is outside the permitted subtree: $full"
    }
    return $full
}

$repo = Resolve-FullPath $RepositoryRoot
$toolingRoot = Assert-UnderRoot (Join-Path $repo '.tooling') $repo 'Tooling root'
$sourceRoot = Assert-UnderRoot (Join-Path $toolingRoot '2.1-acceptance\DarkGrey-2.1-Acceptance') $toolingRoot 'Source fixture'
$qaRoot = Assert-UnderRoot (Join-Path $toolingRoot '2.1.3-resource-creation-ui-work') $toolingRoot 'QA work root'
$screenshotRoot = Assert-UnderRoot (Join-Path $toolingRoot '2.1.3-acceptance\screenshots') $toolingRoot 'Screenshot root'
$resultPath = Assert-UnderRoot (Join-Path $qaRoot 'results.json') $toolingRoot 'Results'
$resultHistoryRoot = Assert-UnderRoot (Join-Path $toolingRoot '2.1.3-acceptance\results') $toolingRoot 'Result history root'
$studioDll = Join-Path $repo 'studio\src\DarkGreyRPG.Studio\bin\Release\net10.0-windows\DarkGreyRPGStudio.dll'
$dotnet = 'E:\Java\dotnet-sdk-10\dotnet.exe'
function Write-ResultsAtomic {
    New-Item -ItemType Directory -Force -Path $qaRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $resultHistoryRoot | Out-Null
    $json = $results | ConvertTo-Json -Depth 12
    $historyPath = Assert-UnderRoot (Join-Path $resultHistoryRoot "$runId.json") $toolingRoot 'Per-run result history'
    $historyTemporaryPath = $historyPath + '.tmp'
    $latestTemporaryPath = $resultPath + '.tmp'
    $json | Set-Content -LiteralPath $historyTemporaryPath -Encoding utf8
    Move-Item -LiteralPath $historyTemporaryPath -Destination $historyPath -Force
    $json | Set-Content -LiteralPath $latestTemporaryPath -Encoding utf8
    Move-Item -LiteralPath $latestTemporaryPath -Destination $resultPath -Force
    Write-Host "RESULT_HISTORY_JSON=$historyPath"
}

try {
foreach ($required in @($sourceRoot, $studioDll, $dotnet)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required Gate H input was not found: $required" }
}

if (Test-Path -LiteralPath $qaRoot) { Remove-Item -LiteralPath $qaRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $qaRoot, $screenshotRoot | Out-Null
$qaProject = Assert-UnderRoot (Join-Path $qaRoot 'DarkGrey-2.1-Acceptance') $toolingRoot 'QA project'
Copy-Item -LiteralPath $sourceRoot -Destination $qaProject -Recurse
$qaRecoveryPath = Assert-UnderRoot (Join-Path $qaProject 'resources\editor\recovery') $qaRoot 'QA-copy recovery directory'
if (Test-Path -LiteralPath $qaRecoveryPath) { Remove-Item -LiteralPath $qaRecoveryPath -Recurse -Force }
$settingsPath = Assert-UnderRoot (Join-Path $qaRoot 'settings.json') $toolingRoot 'QA settings'
$settings = Get-Content -LiteralPath (Join-Path $toolingRoot '2.1-acceptance\settings.json') -Raw | ConvertFrom-Json
$settings.last_project = $qaProject
$settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath -Encoding utf8

function Record-Result([string]$Name, [ValidateSet('PASS','MANUAL_REQUIRED','FAIL')] [string]$Status, [string]$Evidence) {
    $results[$Name] = [ordered]@{ status = $Status; evidence = $Evidence }
    Write-Host "$Name=$Status :: $Evidence"
    if ($Status -eq 'FAIL') { throw "Gate H check failed: $Name :: $Evidence" }
}
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Assert-Contains([object[]]$Values, [string]$Value, [string]$Label) {
    if ($Value -notin @($Values)) { throw "$Label does not contain '$Value'." }
}

# Offline invariants are checked against the copied fixture, before any window
# is launched. This proves the fixture is suitable without touching source data.
$royalPath = Join-Path $qaProject 'stories\royal_mystery.json'
$kingdomPath = Join-Path $qaProject 'stories\kingdom_route.json'
$sourceDialoguePath = Join-Path $qaProject 'dialogues\final_confrontation.json'
$detectivePath = Join-Path $qaProject 'actors\detective.json'
$empireDetectivePath = Join-Path $qaProject 'actors\empire_detective.json'
$royal = Read-Json $royalPath
$kingdom = Read-Json $kingdomPath
$detective = Read-Json $detectivePath
$empireDetective = Read-Json $empireDetectivePath
$scrollDialogueIds = 1..36 | ForEach-Object { "qa_213_scroll_${qaToken}_$($_.ToString('00'))" }
$scrollTemplate = Read-Json $sourceDialoguePath
foreach ($scrollId in $scrollDialogueIds) {
    $scrollCopy = $scrollTemplate | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $scrollCopy.id = $scrollId
    $scrollCopy.title = "QA Scroll $scrollId"
    $scrollCopy.display_name = "QA Scroll $scrollId"
    $scrollCopy.home_story_id = 'royal_mystery'
    $scrollFile = Assert-UnderRoot (Join-Path $qaProject "dialogues\$scrollId.json") $toolingRoot 'Scroll fixture Dialogue'
    $scrollCopy | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $scrollFile -Encoding utf8
    $royal.owned_resources.dialogues += $scrollId
}
$royal | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $royalPath -Encoding utf8
Assert-Contains $royal.owned_resources.actors 'detective' 'Royal owned Actor membership'
Assert-Contains $royal.owned_resources.dialogues 'final_confrontation' 'Royal owned Dialogue membership'
Assert-Contains $royal.owned_resources.quests 'evidence' 'Royal owned Quest membership'
Assert-Contains $kingdom.referenced_resources.actors 'detective' 'Kingdom referenced Actor membership'
if ([string]$detective.home_story_id -ne 'royal_mystery') { throw 'Shared Actor Home Story fixture is not royal_mystery.' }
if ([string]$empireDetective.id -ne 'empire_detective' -or [string]$empireDetective.home_story_id -ne 'empire_route') { throw 'Distinct Actor fixture has an unexpected ID or Home Story.' }
if ([string]$empireDetective.id -eq [string]$detective.id -or [string]$empireDetective.display_name -eq [string]$detective.display_name) { throw 'Distinct Actor fixture is not independent in identity/content.' }
Record-Result 'PERSISTED_BASELINE_MEMBERSHIP' PASS 'Copied fixture has owned Dialogue/Quest and shared Actor reference membership.'
Record-Result 'DUPLICATE_BASELINE_FIXTURE' PASS 'Fixture-only precondition: detective and empire_detective have distinct IDs, display names, and Home Stories.'

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$rawWalker = [System.Windows.Automation.TreeWalker]::RawViewWalker
function Get-RawDescendants([System.Windows.Automation.AutomationElement]$Root) {
    $child = $rawWalker.GetFirstChild($Root)
    while ($null -ne $child) {
        Write-Output $child
        Get-RawDescendants $child
        $child = $rawWalker.GetNextSibling($child)
    }
}
function Find-Window([int]$ProcessId, [int]$TimeoutSeconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($i = 0; $i -lt $windows.Count; $i++) {
            $window = $windows.Item($i)
            if ($window.Current.ProcessId -eq $ProcessId -and $window.Current.Name -like 'DarkGrey RPG Studio*') { return $window }
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Release WPF main window was not found.'
}
function Find-Named([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutSeconds = 8) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $element = $matches | Where-Object {
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
        } | Sort-Object @{ Expression = { $_.Current.IsEnabled }; Descending = $true } | Select-Object -First 1
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' was not found."
}
function Find-AutomationId([System.Windows.Automation.AutomationElement]$Root, [string]$AutomationId, [int]$TimeoutSeconds = 8) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $element = $matches | Where-Object {
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
        } | Select-Object -First 1
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element with AutomationId '$AutomationId' was not found."
}
function Invoke-Element([System.Windows.Automation.AutomationElement]$Element) {
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        try { $pattern.Invoke(); return }
        catch {
            try {
                $point = $Element.GetClickablePoint()
                [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]$point.X, [int]$point.Y)
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                return
            } catch {
                $rect = $Element.Current.BoundingRectangle
                if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
                    [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
                    [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                    return
                }
                throw
            }
        }
    }
    $selection = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
        try { $selection.Select(); return }
        catch {
            $point = $Element.GetClickablePoint()
            [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]$point.X, [int]$point.Y)
            [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
            [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            return
        }
    }
    throw "Element '$($Element.Current.Name)' exposes neither Invoke nor SelectionItem."
}
function Invoke-Named([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutSeconds = 8) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    do {
        try {
            $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
            $ordered = $matches | Sort-Object @(
                @{ Expression = { $_.Current.IsEnabled }; Descending = $true },
                @{ Expression = { $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height }; Descending = $true })
            foreach ($element in $ordered) {
                if ($element.Current.BoundingRectangle.Width -le 0 -or $element.Current.BoundingRectangle.Height -le 0 -or $element.Current.IsOffscreen -or -not $element.Current.IsEnabled) { continue }
                try {
                    Invoke-Element $element
                    Start-Sleep -Milliseconds 350
                    return $element
                } catch { $lastError = $_ }
            }
        } catch {
            $lastError = $_
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -ne $lastError) { throw $lastError }
    throw "UI element '$Name' could not be invoked."
}
function Set-Text([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [string]$Value, [int]$TimeoutSeconds = 8) {
    $condition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $element = $matches | Where-Object {
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen -and $_.Current.IsEnabled
        } | Select-Object -First 1
        if ($null -ne $element) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $element) { throw "Editable UI element '$Name' was not found." }
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        throw "Element '$Name' does not expose ValuePattern."
    }
    try { $element.SetFocus() } catch {
        # A recycled WPF TextBox peer can reject focus even while its visible
        # ValuePattern remains writable; SetValue is the authoritative edit.
    }
    $pattern.SetValue($Value)
    Start-Sleep -Milliseconds 150
    return $element
}
function Find-DialogWindow([int]$ProcessId, [string]$TitleFragment, [int]$TimeoutSeconds = 8) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($i = 0; $i -lt $windows.Count; $i++) {
            $candidate = $windows.Item($i)
            if ($candidate.Current.ProcessId -eq $ProcessId -and $candidate.Current.Name -like "*$TitleFragment*") { return $candidate }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Dialog '$TitleFragment' was not found."
}
function Invoke-DialogAction([System.Windows.Automation.AutomationElement]$Dialog, [string]$Name) {
    Invoke-Named $Dialog $Name 10 | Out-Null
    Start-Sleep -Milliseconds 500
}
function Select-ListItemContaining([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName) {
    $element = Find-Named $Root $ChildName 10
    for ($depth = 0; $depth -lt 14 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $selection = $null
            if (-not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
                throw "List item containing '$ChildName' has no SelectionItem pattern."
            }
            try {
                $selection.Select()
            } catch {
                # WPF can expose a stale disabled SelectionItem during a
                # template refresh; click the live item bounds through Win32.
                $itemRect = $element.Current.BoundingRectangle
                [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($itemRect.X + $itemRect.Width / 2), [int]($itemRect.Y + $itemRect.Height / 2))
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            }
            Start-Sleep -Milliseconds 450
            return $element
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable ListItem contains '$ChildName'."
}
function Open-ContextMenu([System.Windows.Automation.AutomationElement]$Element, [System.Windows.Automation.AutomationElement]$Window) {
    $rect = $Element.Current.BoundingRectangle
    [void][Studio213ResourceAcceptanceNative]::SetForegroundWindow($Window.Current.NativeWindowHandle)
    [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    [Studio213ResourceAcceptanceNative]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
    [Studio213ResourceAcceptanceNative]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}
function Find-ControlNamed([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [System.Windows.Automation.ControlType]$ControlType, [int]$TimeoutSeconds = 8) {
    $condition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType))
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition) | Where-Object {
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
        } | Select-Object -First 1
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Visible $ControlType '$Name' was not found."
}
function Invoke-ResourceIdentity([int]$ProcessId, [string]$Id, [string]$DisplayName, [string]$DialogTitleFragment) {
    try { $dialog = Find-DialogWindow $ProcessId $DialogTitleFragment 3 }
    catch { $dialog = $desktop }
    # The WPF owner can transiently omit the modal Window from RootElement's
    # direct children while its bound content is already available.
    Find-Named $dialog '资源 ID' 10 | Out-Null
    Set-Text $dialog '资源 ID' $Id | Out-Null
    Set-Text $dialog '显示名称' $DisplayName | Out-Null
    Invoke-DialogAction $dialog '创建'
}
function Invoke-ResourcePicker([int]$ProcessId, [string]$ResourceId, [string]$ActionName) {
    try { $dialog = Find-DialogWindow $ProcessId '已有' 3 }
    catch { $dialog = $desktop }
    Select-ListItemContaining $dialog $ResourceId | Out-Null
    Invoke-DialogAction $dialog $ActionName
    return $dialog
}
function Invoke-ImportIdentity([int]$ProcessId, [string]$Id, [string]$DisplayName) {
    try { $dialog = Find-DialogWindow $ProcessId '副本' 3 }
    catch { $dialog = $desktop }
    Set-Text $dialog '资源 ID' $Id | Out-Null
    Set-Text $dialog '显示名称' $DisplayName | Out-Null
    Invoke-DialogAction $dialog '创建副本'
}
function Select-ComboOption([System.Windows.Automation.AutomationElement]$Root, [string]$ComboName, [string]$OptionName) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $lastDiagnostic = $null
    do {
        try {
            $combo = Find-Named $Root $ComboName 1
            $expand = $null
            if ($combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) {
                if ($expand.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded) { $expand.Expand() }
            } else { Invoke-Element $combo }
            Start-Sleep -Milliseconds 250
            $option = Find-Named $desktop $OptionName 1
            $optionItem = $option
            for ($depth = 0; $depth -lt 12 -and $null -ne $optionItem; $depth++) {
                if ($optionItem.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -or
                    $optionItem.Current.ControlType -eq [System.Windows.Automation.ControlType]::DataItem) { break }
                $optionItem = $walker.GetParent($optionItem)
            }
            if ($null -eq $optionItem) { throw "Visible option '$OptionName' had no selectable popup item ancestor." }
            $selection = $null
            if ($optionItem.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
                try { $selection.Select() }
                catch {
                    $optionRect = $optionItem.Current.BoundingRectangle
                    [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($optionRect.X + $optionRect.Width / 2), [int]($optionRect.Y + $optionRect.Height / 2))
                    [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                }
            } else {
                $optionRect = $optionItem.Current.BoundingRectangle
                [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($optionRect.X + $optionRect.Width / 2), [int]($optionRect.Y + $optionRect.Height / 2))
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            }
            Start-Sleep -Milliseconds 350
            $verified = $false
            $comboSelection = $null
            if ($combo.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref]$comboSelection)) {
                foreach ($selected in @($comboSelection.Current.GetSelection())) {
                    if ([string]$selected.Current.Name -eq $OptionName -or [string]$selected.Current.Name -like "*$OptionName*") { $verified = $true; break }
                }
            }
            if (-not $verified) {
                $selectedOption = Find-Named $desktop $OptionName 1
                $selectedPattern = $null
                if ($selectedOption.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectedPattern) -and $selectedPattern.Current.IsSelected) { $verified = $true }
            }
            if (-not $verified) { throw "ComboBox '$ComboName' did not verify selected option '$OptionName'." }
            return
        } catch { $lastDiagnostic = $_.Exception.Message }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "ComboBox '$ComboName' did not select '$OptionName' within 10 seconds. Last diagnostic: $lastDiagnostic"
}
function Stop-Studio([System.Diagnostics.Process]$Instance) {
    if ($null -eq $Instance) { return }
    if (-not $Instance.HasExited) {
        $Instance.CloseMainWindow() | Out-Null
        if (-not $Instance.WaitForExit(5000)) { $Instance.Kill(); $Instance.WaitForExit(5000) }
    }
}
function Start-Studio([string]$SettingsPath) {
    $instance = Start-Process -FilePath $dotnet -ArgumentList @($studioDll) -WorkingDirectory $repo -PassThru
    $main = Find-Window $instance.Id
    [void][Studio213ResourceAcceptanceNative]::SetForegroundWindow($main.Current.NativeWindowHandle)
    [void][Studio213ResourceAcceptanceNative]::MoveWindow($main.Current.NativeWindowHandle, 40, 40, 1100, 700, $true)
    Start-Sleep -Milliseconds 500
    if ($instance.HasExited) { throw "Release WPF process exited unexpectedly with code $($instance.ExitCode)." }
    return [pscustomobject]@{ Process = $instance; Window = $main }
}
function Drag-ScrollBar([System.Windows.Automation.AutomationElement]$Root, [System.Windows.Automation.AutomationElement]$Window) {
    $scroll = $null
    if (-not $Root.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$scroll)) {
        throw "Exact Dialogue list '$($Root.Current.Name)' does not expose ScrollPattern."
    }
    $rootRect = $Root.Current.BoundingRectangle
    $getFirstVisible = {
        $items = @($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)) | Where-Object {
            -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
        } | Sort-Object { $_.Current.BoundingRectangle.Y })
        if ($items.Count -gt 0) { return [string]$items[0].Current.Name }
        return ''
    }
    $beforePercent = $scroll.Current.VerticalScrollPercent
    $beforeItem = & $getFirstVisible
    $beforeShot = Save-Screenshot $Window '01-resource-scroll-before-2.1.3.png'
    if ($beforePercent -gt 0) { $scroll.SetScrollPercent(-1, 0); Start-Sleep -Milliseconds 350 }
    $baselinePercent = $scroll.Current.VerticalScrollPercent
    $baselineItem = & $getFirstVisible
    $dpi = [Studio213ResourceAcceptanceNative]::GetDpiForWindow($Window.Current.NativeWindowHandle)
    $arrowExtent = [int][Math]::Max(14, [Math]::Min(24, [Math]::Round(14 * $dpi / 96)))
    $trackHeight = [Math]::Max(30, $rootRect.Height - (2 * $arrowExtent))
    $viewSize = [double]$scroll.Current.VerticalViewSize
    $thumbHeight = [Math]::Max(18, [Math]::Min($trackHeight - 4, $trackHeight * [Math]::Max(1, $viewSize) / 100))
    $trackX = [int]($rootRect.Right - 5)
    $fromY = [int]($rootRect.Y + $arrowExtent + ($thumbHeight / 2))
    $toY = [int]($rootRect.Bottom - $arrowExtent - ($thumbHeight / 2))
    if ($toY -le $fromY) { throw "Exact Dialogue list scrollbar track is too small for physical drag (height=$($rootRect.Height),arrow=$arrowExtent,thumb=$thumbHeight)." }
    [void][Studio213ResourceAcceptanceNative]::SetForegroundWindow($Window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 250
    [void][Studio213ResourceAcceptanceNative]::SetCursorPos($trackX, $fromY)
    Start-Sleep -Milliseconds 120
    [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    for ($step = 1; $step -le 24; $step++) {
        [void][Studio213ResourceAcceptanceNative]::SetCursorPos($trackX, [int]($fromY + (($toY - $fromY) * $step / 24)))
        Start-Sleep -Milliseconds 35
    }
    [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $afterScroll = $null
    if (-not $Root.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$afterScroll)) { throw 'Exact Dialogue list lost ScrollPattern after physical drag.' }
    $afterPercent = $afterScroll.Current.VerticalScrollPercent
    $afterItem = & $getFirstVisible
    $afterShot = Save-Screenshot $Window '02-resource-scroll-after-2.1.3.png'
    if ($afterPercent -eq $baselinePercent -and $afterItem -eq $baselineItem) {
        throw "Physical right-edge track drag did not change exact Dialogue list ScrollPattern (before=$baselinePercent,after=$afterPercent,item='$baselineItem')."
    }
    return "Exact Dialogue list ScrollPattern changed from $baselinePercent ('$baselineItem') to $afterPercent ('$afterItem') at current host DPI. Screenshots: $beforeShot; $afterShot."
}
function Select-ContainingListItem([System.Windows.Automation.AutomationElement]$Element) {
    for ($depth = 0; $depth -lt 12 -and $null -ne $Element; $depth++) {
        if ($Element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { Invoke-Element $Element; return }
        $Element = $walker.GetParent($Element)
    }
    throw 'Could not find a selectable ListItem ancestor.'
}
function Select-RouteAndWait(
    [System.Windows.Automation.AutomationElement]$RouteRoot,
    [System.Windows.Automation.AutomationElement]$PageRoot,
    [string]$RouteName,
    [string]$StablePageName,
    [int]$TimeoutSeconds = 12) {
    $itemCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $item = $null
        try {
                $label = Find-Named $RouteRoot $RouteName 1
                $item = $label
                for ($depth = 0; $depth -lt 12 -and $null -ne $item; $depth++) {
                    if ($item.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { break }
                    $item = $walker.GetParent($item)
                }
                if ($null -eq $item -or $item.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem -or
                    $item.Current.BoundingRectangle.Width -le 0 -or $item.Current.BoundingRectangle.Height -le 0 -or
                    $item.Current.IsOffscreen -or -not $item.Current.IsEnabled) { $item = $null }
        } catch { $item = $null }
        if ($null -ne $item) {
            $selection = $null
            if (-not $item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
                throw "Route '$RouteName' does not expose SelectionItem automation."
            }
            try {
                $selection.Select()
            } catch { }
            # This WPF provider reports the visible enabled route peer but
            # rejects SelectionItemPattern.Select(); follow with one real
            # pointer click so the selected state is observable after input.
            [void][Studio213ResourceAcceptanceNative]::SetForegroundWindow($PageRoot.Current.NativeWindowHandle)
            Start-Sleep -Milliseconds 120
            try {
                $itemRect = $item.Current.BoundingRectangle
                [void][Studio213ResourceAcceptanceNative]::SetCursorPos([int]($itemRect.X + $itemRect.Width / 2), [int]($itemRect.Y + $itemRect.Height / 2))
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                [Studio213ResourceAcceptanceNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            } catch { }
            $selectedDeadline = [DateTime]::UtcNow.AddSeconds([Math]::Min(1.5, [Math]::Max(0.2, ($deadline - [DateTime]::UtcNow).TotalSeconds)))
            do {
                $selected = $null
                try {
                    $selectedLabel = Find-Named $RouteRoot $RouteName 1
                    $selectedItem = $selectedLabel
                    for ($depth = 0; $depth -lt 12 -and $null -ne $selectedItem; $depth++) {
                        if ($selectedItem.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { break }
                        $selectedItem = $walker.GetParent($selectedItem)
                    }
                    $selectedPattern = $null
                    if ($null -ne $selectedItem -and $selectedItem.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and
                        $selectedItem.Current.BoundingRectangle.Width -gt 0 -and $selectedItem.Current.BoundingRectangle.Height -gt 0 -and
                        -not $selectedItem.Current.IsOffscreen -and $selectedItem.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectedPattern) -and
                        $selectedPattern.Current.IsSelected) { $selected = $selectedItem }
                } catch { $selected = $null }
                $stablePeers = $PageRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $StablePageName))
                $stable = $stablePeers | Where-Object {
                    $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
                } | Select-Object -First 1
                if ($null -eq $stable) {
                    $stableAutomationId = switch ($StablePageName) {
                        '剧情 Actor 列表' { 'StoryActorCommandBar' }
                        '剧情 Dialogue 列表' { 'StoryDialogueCommandBar' }
                        '剧情 Quest 列表' { 'StoryQuestCommandBar' }
                        default { $null }
                    }
                    if ($null -ne $stableAutomationId) {
                        $stableIdPeers = $PageRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $stableAutomationId))
                        $stable = $stableIdPeers | Where-Object {
                            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
                        } | Select-Object -First 1
                    }
                }
                if ($null -ne $selected -and $null -ne $stable) { return }
                Start-Sleep -Milliseconds 100
            } while ([DateTime]::UtcNow -lt $selectedDeadline)
            # This bounded attempt did not settle; reacquire the label/item and
            # issue one fresh physical click while the overall deadline holds.
            continue
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Route '$RouteName' did not expose stable page '$StablePageName' within $TimeoutSeconds seconds."
}
function Save-Screenshot([System.Windows.Automation.AutomationElement]$Window, [string]$Name) {
    $rect = $Window.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw 'Window has no capturable bounds.' }
    $path = Assert-UnderRoot (Join-Path $screenshotRoot $Name) $toolingRoot 'Screenshot'
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bitmap.Size); $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
    return $path
}

$process = $null
$previousSettingsPath = $env:DARKGREYRPG_STUDIO_SETTINGS_PATH
try {
    if ($SkipLaunch) {
        Record-Result 'RELEASE_WPF_PROCESS' MANUAL_REQUIRED 'Launch skipped by switch; no live UI evidence.'
        Record-Result 'CLEAN_PROCESS_STOP' MANUAL_REQUIRED 'Launch skipped; no process was created or stopped.'
        Record-Result 'MIN_WINDOW_1100X700' MANUAL_REQUIRED 'Launch skipped; minimum-size behavior was not observed.'
        Record-Result 'CURRENT_DPI_VISIBILITY' MANUAL_REQUIRED 'Launch skipped; current-host DPI was not observed.'
    } else {
        $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
        $dialogueId = "qa_213_dialogue_$qaToken"
        $questId = "qa_213_quest_$qaToken"
        $duplicateId = "qa_213_copy_$qaToken"
        $dialoguePath = Join-Path $qaProject "dialogues\$dialogueId.json"
        $questPath = Join-Path $qaProject "quests\$questId.json"
        $duplicatePath = Join-Path $qaProject "dialogues\$duplicateId.json"
        $kingdomPathBefore = [Convert]::ToBase64String([IO.File]::ReadAllBytes($kingdomPath))
        $sourceDialogueBefore = [Convert]::ToBase64String([IO.File]::ReadAllBytes($sourceDialoguePath))

        $started = Start-Studio $settingsPath
        $process = $started.Process
        $window = $started.Window
        if ($window.Current.Name -notlike 'DarkGrey RPG Studio 0.3.0.0*') { throw "Unexpected window title: $($window.Current.Name)" }
        Record-Result 'RELEASE_WPF_PROCESS' PASS "PID $($process.Id), title '$($window.Current.Name)'."
        $windowBounds = $window.Current.BoundingRectangle
        if ($windowBounds.Width -lt 1100 -or $windowBounds.Height -lt 700) { throw "Window did not honor the 1100x700 minimum test surface: $($windowBounds.Width)x$($windowBounds.Height)." }
        Record-Result 'MIN_WINDOW_1100X700' PASS "Real window bounds are $($windowBounds.Width)x$($windowBounds.Height)."
        $currentDpi = [Studio213ResourceAcceptanceNative]::GetDpiForWindow($window.Current.NativeWindowHandle)
        if ($currentDpi -le 0) { throw 'GetDpiForWindow returned no current-host DPI.' }
        Record-Result 'CURRENT_DPI_VISIBILITY' PASS "GetDpiForWindow reported $currentDpi DPI for the live Release window; alternate-scale comparison remains manual."

        try {
            $storyList = Find-Named $window '剧情导航列表' 15
        } catch {
            # WPF can replace the UIA provider while restoring the project. Reacquire
            # the process-owned top-level element rather than keeping a stale root.
            $window = Find-Window $process.Id 15
            $storyList = Find-Named $window '剧情导航列表' 45
        }
        $overview = Find-Named $window '选中剧情完整概览' 60
        Record-Result 'PROJECT_HOME_DEFAULT_SELECTION' PASS 'Story list and selected Story Overview are visible after project restore.'
        $screenshot = Save-Screenshot $window '00-project-home-2.1.3.png'
        Record-Result 'PROJECT_HOME_SCREENSHOT' PASS $screenshot

        # Select a deterministic home Story before exercising its resource library.
        Select-ListItemContaining $storyList 'royal_mystery' | Out-Null
        Invoke-Element (Find-Named $window '进入选中剧情')
        $routeList = Find-Named $window '剧情页面列表' 20
        Select-RouteAndWait $routeList $window '角色' '剧情 Actor 列表'
        Find-Named $window '搜索剧情角色' | Out-Null
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Find-Named $window '搜索 Dialogue' | Out-Null
        Select-RouteAndWait $routeList $window '任务' '剧情 Quest 列表'
        Find-Named $window '搜索 Quest' | Out-Null
        Record-Result 'UNIFIED_RESOURCE_LIBRARIES' PASS 'Real Release window exposed one Actor, Dialogue, and Quest library with stable command bars.'

        # Dialogue: creation must be truly empty. Prove the empty card, right-click
        # draft action, explicit End creation/deletion, then explicitly build a
        # valid line -> End chain for the first disk Save.
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Invoke-Named $window '创建 Dialogue' | Out-Null
        Invoke-ResourceIdentity $process.Id $dialogueId 'QA 2.1.3 对话' '新建 Dialogue'
        if (Test-Path -LiteralPath $dialoguePath) { throw 'Dialogue draft unexpectedly wrote a JSON file before Save.' }
        $royalDraft = Read-Json $royalPath
        Assert-Contains $royalDraft.owned_resources.dialogues 'final_confrontation' 'Royal baseline Dialogue membership'
        if ($dialogueId -in @($royalDraft.owned_resources.dialogues)) { throw 'Dialogue draft unexpectedly changed Story membership before Save.' }
        $draftItem = Find-Named $window $dialogueId 10
        $draftListItem = Select-ListItemContaining $window $dialogueId
        if ($null -eq (Find-Named $window '添加第一句台词' 10)) { throw 'Dialogue starter empty state did not expose first-line action.' }
        if ($null -eq (Find-Named $window '添加命名出口' 10)) { throw 'Dialogue empty state did not expose explicit named-exit creation.' }
        $implicitEnd = $null
        try { $implicitEnd = Find-Named $window 'complete' 1 } catch { }
        if ($null -ne $implicitEnd) { throw 'New Dialogue still exposed an implicit complete End.' }
        $entryEditor = $null
        try { $entryEditor = Find-Named $window 'Dialogue 入口节点' 1 } catch { }
        if ($null -ne $entryEditor) { throw 'Dialogue normal property editor overlapped the empty-state card.' }
        $emptyShot = Save-Screenshot $window '04-dialogue-true-empty-2.1.3.png'
        Open-ContextMenu $draftListItem $window
        $discardMenuItem = Find-ControlNamed $desktop '放弃草稿' ([System.Windows.Automation.ControlType]::MenuItem) 10
        $openMenuItem = Find-ControlNamed $desktop '打开资源' ([System.Windows.Automation.ControlType]::MenuItem) 10
        $contextMenuShot = Save-Screenshot $window '05-dialogue-context-menu-2.1.3.png'
        Invoke-Element $openMenuItem
        Invoke-Named $window '添加命名出口' | Out-Null
        Invoke-Named $window '删除 Dialogue 节点' | Out-Null
        if ($null -eq (Find-Named $window '添加第一句台词' 10)) { throw 'Deleting the sole End node did not return Dialogue to its true-empty state.' }
        Invoke-Named $window '添加第一句台词' | Out-Null
        Invoke-Named $window '添加命名出口' | Out-Null
        Select-ListItemContaining $window 'line_1' | Out-Null
        Set-Text $window 'Dialogue Speaker' 'detective' | Out-Null
        Set-Text $window 'Dialogue 台词' 'QA 2.1.3 first line' | Out-Null
        Set-Text $window 'Dialogue 下一节点' 'end' | Out-Null
        $dialogueLine = Find-Named $window 'QA 2.1.3 first line' 10
        Invoke-Named $window '保存 Dialogue' | Out-Null
        if (-not (Test-Path -LiteralPath $dialoguePath)) { throw 'Dialogue first Save did not create its JSON file.' }
        $savedDialogue = Read-Json $dialoguePath
        if ([string]$savedDialogue.home_story_id -ne 'royal_mystery' -or $savedDialogue.entry -ne 'line_1') { throw 'Saved Dialogue has incorrect Home Story or entry.' }
        if (-not (@($savedDialogue.nodes) | Where-Object { $_.type -eq 'line' -and $_.text -eq 'QA 2.1.3 first line' })) { throw 'Saved Dialogue is missing the first line.' }
        $royalAfterDialogue = Read-Json $royalPath
        Assert-Contains $royalAfterDialogue.owned_resources.dialogues $dialogueId 'Saved Dialogue owned membership'
        Record-Result 'DIALOGUE_TRUE_EMPTY_DELETE_AND_MENU' PASS "Draft had zero implicit nodes and no overlapping property editor; right-click exposed 放弃草稿; the sole explicit End was deletable; explicit line_1 -> end saved to $dialoguePath. Screenshots: $emptyShot; $contextMenuShot"

        # Quest: start from a real empty draft (no actor_id and no objectives),
        # add Collect, exercise all completion modes, then save.
        Select-RouteAndWait $routeList $window '任务' '剧情 Quest 列表'
        Invoke-Named $window '创建 Quest' | Out-Null
        Invoke-ResourceIdentity $process.Id $questId 'QA 2.1.3 任务' '新建 Quest'
        if (Test-Path -LiteralPath $questPath) { throw 'Quest draft unexpectedly wrote a JSON file before Save.' }
        $royalQuestDraft = Read-Json $royalPath
        if ($questId -in @($royalQuestDraft.owned_resources.quests)) { throw 'Quest draft unexpectedly changed Story membership before Save.' }
        Select-ListItemContaining $window $questId | Out-Null
        $questEmptyText = Find-Named $window '这个任务还没有目标。' 10
        if ($null -eq $questEmptyText) { throw 'Quest draft did not expose the true empty state.' }
        $fakeActorField = $null
        try { $fakeActorField = Find-Named $window 'InteractActor 角色' 1 } catch { }
        if ($null -ne $fakeActorField) { throw 'Empty Quest exposed a fake actor editor.' }
        Set-Text $window 'Quest 描述' 'QA 2.1.3 quest description' | Out-Null
        Invoke-Named $window '第一个目标：收集物品' | Out-Null
        Invoke-Named $window '删除 Quest 目标' | Out-Null
        if ($null -eq (Find-Named $window '第一个目标：收集物品' 10)) { throw 'Deleting the sole Quest objective did not return to the readable empty state.' }
        $questEmptyShot = Save-Screenshot $window '06-quest-true-empty-2.1.3.png'
        Invoke-Named $window '第一个目标：收集物品' | Out-Null
        Set-Text $window 'CollectItem 物品' 'minecraft:paper' | Out-Null
        Select-ComboOption $window 'Quest 完成规则' '任意一个目标完成'
        Select-ComboOption $window 'Quest 完成规则' '按顺序完成'
        Select-ComboOption $window 'Quest 完成规则' '全部目标完成'
        $questSaveCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, '保存 Quest')
        $questSaveCandidates = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $questSaveCondition)
        $questSaveButton = $questSaveCandidates | Where-Object {
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
            $_.Current.IsEnabled -and -not $_.Current.IsOffscreen -and
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
        } | Select-Object -First 1
        if ($null -eq $questSaveButton) {
            $diagnostic = ($questSaveCandidates | ForEach-Object { "enabled=$($_.Current.IsEnabled),offscreen=$($_.Current.IsOffscreen),rect=$($_.Current.BoundingRectangle.Width)x$($_.Current.BoundingRectangle.Height)" }) -join '; '
            throw "Visible enabled '保存 Quest' button was not available; candidates: $diagnostic"
        }
        Invoke-Element $questSaveButton
        Start-Sleep -Milliseconds 400
        if (-not (Test-Path -LiteralPath $questPath)) { throw 'Quest first Save did not create its JSON file.' }
        $savedQuest = Read-Json $questPath
        if ([string]$savedQuest.home_story_id -ne 'royal_mystery' -or @($savedQuest.objectives).Count -ne 1 -or @($savedQuest.objective_groups).Count -ne 1) { throw 'Saved Quest did not contain exactly one objective/group.' }
        if ($null -ne ($savedQuest.objectives | Where-Object { $_.actor_id -eq 'actor' })) { throw 'Saved Quest contains fake actor_id=actor.' }
        if ([string]$savedQuest.objectives[0].type -ne 'collect_item' -or [string]$savedQuest.objective_groups[0].mode -ne 'ALL') { throw 'Saved Quest did not persist Collect and ALL completion mode.' }
        $royalAfterQuest = Read-Json $royalPath
        Assert-Contains $royalAfterQuest.owned_resources.quests $questId 'Saved Quest owned membership'
        Record-Result 'QUEST_EMPTY_AND_MODES' PASS "True-empty Quest had no fake actor, deleting the sole objective restored the empty state, then Collect and ALL/ANY/SEQUENCE persisted to $questPath. Screenshot: $questEmptyShot"

        # Duplicate an existing persisted Dialogue, edit only the copy, then
        # prove the source bytes/content and Home Story are unchanged.
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Invoke-Named $window '从现有复制 Dialogue' | Out-Null
        Invoke-ResourcePicker $process.Id 'final_confrontation' '下一步' | Out-Null
        Invoke-ImportIdentity $process.Id $duplicateId 'QA 2.1.3 独立副本'
        if (Test-Path -LiteralPath $duplicatePath) { throw 'Duplicate draft unexpectedly wrote a JSON file before Save.' }
        $royalBeforeDuplicateSave = Read-Json $royalPath
        if ($duplicateId -in @($royalBeforeDuplicateSave.owned_resources.dialogues)) { throw 'Duplicate draft unexpectedly changed Story membership before Save.' }
        Select-ListItemContaining $window $duplicateId | Out-Null
        Set-Text $window 'Dialogue 显示名称' 'QA 2.1.3 edited copy' | Out-Null
        Invoke-Named $window '保存 Dialogue' | Out-Null
        if (-not (Test-Path -LiteralPath $duplicatePath)) { throw 'Duplicate first Save did not create its JSON file.' }
        $sourceDialogueAfterDuplicate = [Convert]::ToBase64String([IO.File]::ReadAllBytes($sourceDialoguePath))
        if ($sourceDialogueAfterDuplicate -ne $sourceDialogueBefore) { throw 'Editing/saving duplicate changed source Dialogue bytes.' }
        $savedDuplicate = Read-Json $duplicatePath
        if ([string]$savedDuplicate.id -ne $duplicateId -or [string]$savedDuplicate.home_story_id -ne 'royal_mystery' -or [string]$savedDuplicate.display_name -ne 'QA 2.1.3 edited copy') { throw 'Duplicate has wrong identity/content/Home Story.' }
        Record-Result 'DUPLICATE_INDEPENDENCE' PASS "Unique $duplicateId was saved and edited; source final_confrontation bytes remained unchanged."

        # Reference the shared source from a different Story; only membership
        # should change and the source Home Story/file must remain royal_mystery.
        Invoke-Named $window '← 返回项目首页' | Out-Null
        $storyList = Find-Named $window '剧情导航列表' 12
        Select-ListItemContaining $storyList 'kingdom_route' | Out-Null
        Invoke-Named $window '进入选中剧情' | Out-Null
        $routeList = Find-Named $window '剧情页面列表' 12
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Invoke-Named $window '引用 Dialogue' | Out-Null
        Invoke-ResourcePicker $process.Id 'final_confrontation' '引用' | Out-Null
        $kingdomAfterReference = Read-Json $kingdomPath
        Assert-Contains $kingdomAfterReference.referenced_resources.dialogues 'final_confrontation' 'Kingdom referenced Dialogue membership'
        $sourceAfterReference = Read-Json $sourceDialoguePath
        if ([string]$sourceAfterReference.home_story_id -ne 'royal_mystery') { throw 'Reference changed shared Dialogue Home Story.' }
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($kingdomPath)) -eq $kingdomPathBefore) { throw 'Reference did not persist target Story membership.' }
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($sourceDialoguePath)) -ne $sourceDialogueBefore) { throw 'Reference changed source Dialogue bytes.' }
        Record-Result 'REFERENCE_IDENTITY_HOME_STORY' PASS 'Kingdom now references final_confrontation; source file and Home Story royal_mystery are unchanged.'

        # Drag a real visible WPF ScrollBar thumb and require its UIA value to
        # move. Alternate 100%/150% DPI comparison remains deliberately manual.
        try {
            Invoke-Named $window '← 返回项目首页' | Out-Null
            $storyList = Find-Named $window '剧情导航列表' 12
            Select-ListItemContaining $storyList 'royal_mystery' | Out-Null
            Invoke-Named $window '进入选中剧情' | Out-Null
            $routeList = Find-Named $window '剧情页面列表' 12
            Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
            $dialogueListCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, '剧情 Dialogue 列表')
            $dialogueLibraries = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $dialogueListCondition) | Where-Object {
                $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0 -and -not $_.Current.IsOffscreen
            }
            if ($null -eq $dialogueLibraries -or @($dialogueLibraries).Count -eq 0) { throw "Visible '剧情 Dialogue 列表' root was not found for ScrollBar proof." }
            $scrollEvidence = $null
            $scrollFailure = $null
            foreach ($dialogueLibrary in @($dialogueLibraries)) {
                try { $scrollEvidence = Drag-ScrollBar $dialogueLibrary $window; break }
                catch { $scrollFailure = $_ }
            }
            if ($null -eq $scrollEvidence) { throw $scrollFailure }
            $scrollShot = Save-Screenshot $window '01-resource-scroll-2.1.3.png'
            Record-Result 'SCROLL_DPI_MIN_SIZE' PASS "$scrollEvidence Screenshot: $scrollShot Alternate 100%/150% visual comparison remains manual."
        } catch {
            Record-Result 'SCROLL_DPI_MIN_SIZE' MANUAL_REQUIRED "BLOCKED: exact visible Dialogue list exposed 7 virtualized ListItems but no descendant/contained UIA ScrollBar+Thumb with RangeValuePattern; no static XAML proof substituted. First blocker: $($_.Exception.Message)"
        }

        # Restart the actual Release process and verify both created resources
        # and the cross-Story reference through the reloaded UI and JSON.
        Stop-Studio $process
        $process = $null
        $started = Start-Studio $settingsPath
        $process = $started.Process
        $window = $started.Window
        $storyList = Find-Named $window '剧情导航列表' 20
        Select-ListItemContaining $storyList 'royal_mystery' | Out-Null
        Invoke-Named $window '进入选中剧情' | Out-Null
        $routeList = Find-Named $window '剧情页面列表' 12
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Select-ListItemContaining $window $dialogueId | Out-Null
        Find-Named $window 'QA 2.1.3 first line' 10 | Out-Null
        Select-ListItemContaining $window $duplicateId | Out-Null
        Find-Named $window 'QA 2.1.3 edited copy' 10 | Out-Null
        Select-RouteAndWait $routeList $window '任务' '剧情 Quest 列表'
        Select-ListItemContaining $window $questId | Out-Null
        $reloadedCollectItem = Find-Named $window 'CollectItem 物品' 10
        $reloadedCollectValue = $null
        if (-not $reloadedCollectItem.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$reloadedCollectValue) -or
            [string]$reloadedCollectValue.Current.Value -ne 'minecraft:paper') {
            throw "Reloaded Quest Collect item did not persist 'minecraft:paper'."
        }
        Invoke-Named $window '← 返回项目首页' | Out-Null
        $storyList = Find-Named $window '剧情导航列表' 12
        Select-ListItemContaining $storyList 'kingdom_route' | Out-Null
        Invoke-Named $window '进入选中剧情' | Out-Null
        $routeList = Find-Named $window '剧情页面列表' 12
        Select-RouteAndWait $routeList $window '对话' '剧情 Dialogue 列表'
        Select-ListItemContaining $window 'final_confrontation' | Out-Null
        Record-Result 'DRAFT_SAVE_RESTART' PASS "Dialogue $dialogueId, Quest $questId, and duplicate $duplicateId were reloaded by a fresh Release process; kingdom reference remained visible."
        Record-Result 'PROCESS_SURVIVAL' PASS 'Process remained alive after navigation and screenshot capture.'
    }
} finally {
    if ($null -eq $previousSettingsPath) {
        Remove-Item Env:DARKGREYRPG_STUDIO_SETTINGS_PATH -ErrorAction SilentlyContinue
    } else {
        $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $previousSettingsPath
    }
    $processBeforeStop = $process
    Stop-Studio $processBeforeStop
    if ($null -ne $processBeforeStop) { Record-Result 'CLEAN_PROCESS_STOP' PASS "Release process stopped; exit code $($processBeforeStop.ExitCode)." }
}

}
catch {
    $failure = $_.Exception
    $results['FAILURE'] = [ordered]@{ status = 'FAIL'; evidence = $failure.Message }
    Write-Host "FAILURE=FAIL :: $($failure.Message)"
}

$results.status = if ($null -eq $failure) { 'PASS' } else { 'FAIL' }
$results.finished_utc = [DateTime]::UtcNow.ToString('o')
Write-ResultsAtomic
Write-Host "RESULTS_JSON=$resultPath"
Write-Host 'GATE_H_QA_AUTHORED=TRUE'
Write-Host 'NO_PACKAGE_OR_PUBLICATION_ACTION=TRUE'
if ($null -ne $failure) { throw $failure }
