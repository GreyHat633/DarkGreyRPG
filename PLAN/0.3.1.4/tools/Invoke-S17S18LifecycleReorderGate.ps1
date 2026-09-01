[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s17-s18-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s17-s18-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\final-live-post-choice-layout\s17-s18-lifecycle-reorder'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class S17S18Native {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
}
'@

$repo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repo '.tooling')).TrimEnd('\')
$planEvidence = [IO.Path]::GetFullPath((Join-Path $repo 'PLAN\0.3.1.4\evidence')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory).TrimEnd('\')
$settings = [IO.Path]::GetFullPath($SettingsPath)
$metadataPath = Join-Path $project 'fixture.json'
$resultPath = Join-Path $evidence 's17-s18-lifecycle-reorder-gate.json'
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must remain below .tooling.' }
if (-not $settings.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Settings must remain below .tooling.' }
$evidenceInTooling = $evidence.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)
$evidenceInPlan = $evidence.StartsWith($planEvidence + '\', [StringComparison]::OrdinalIgnoreCase)
if (-not ($evidenceInTooling -or $evidenceInPlan)) {
    throw 'Evidence must remain below .tooling or PLAN/0.3.1.4/evidence.'
}

$desktop = [Windows.Automation.AutomationElement]::RootElement
$rawWalker = [Windows.Automation.TreeWalker]::RawViewWalker
$process = $null
$failure = $null
$results = [ordered]@{
    schema_version = 1
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = [IO.Path]::GetFullPath($ExePath)
    project = $project
    screenshots = [Collections.Generic.List[string]]::new()
    operations = [Collections.Generic.List[object]]::new()
    disk_assertions = [Collections.Generic.List[object]]::new()
    status = 'FAIL'
}

function Find-Window([int] $ProcessId, [int] $TimeoutSeconds = 25) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($window in $desktop.FindAll([Windows.Automation.TreeScope]::Children, $condition)) {
            if ($window.Current.AutomationId -eq 'RootWindow') { return $window }
        }
        Start-Sleep -Milliseconds 120
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio window for process $ProcessId was not found."
}

function Find-ByName($Root, [string] $Name, [int] $TimeoutSeconds = 10) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            foreach ($item in $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)) {
                $rect = $item.Current.BoundingRectangle
                if (-not $item.Current.IsOffscreen -and $rect.Width -gt 0 -and $rect.Height -gt 0) { return $item }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Visible element named '$Name' was not found."
}

function Find-ById($Root, [string] $AutomationId, [int] $TimeoutSeconds = 10) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            foreach ($item in $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)) {
                $rect = $item.Current.BoundingRectangle
                if (-not $item.Current.IsOffscreen -and $rect.Width -gt 0 -and $rect.Height -gt 0) { return $item }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Visible element '$AutomationId' was not found."
}

function Find-Ancestor($Element, [scriptblock] $Predicate) {
    for ($depth = 0; $depth -lt 20 -and $null -ne $Element; $depth++) {
        if (& $Predicate $Element) { return $Element }
        $Element = $rawWalker.GetParent($Element)
    }
    return $null
}

function Click-Point([double] $X, [double] $Y) {
    [void][S17S18Native]::SetCursorPos([int]$X, [int]$Y)
    [S17S18Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [S17S18Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Click-Element($Element) {
    $rect = $Element.Current.BoundingRectangle
    Click-Point ($rect.Left + $rect.Width / 2) ($rect.Top + $rect.Height / 2)
}

function Invoke-Element($Element) {
    $pattern = $null
    if ($Element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
    else { Click-Element $Element }
    Start-Sleep -Milliseconds 300
}

function Save-Screenshot($Window, [string] $Name) {
    $rect = $Window.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size) }
        finally { $graphics.Dispose() }
        $path = Join-Path $evidence $Name
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        $results.screenshots.Add([IO.Path]::GetFullPath($path))
        return [IO.Path]::GetFullPath($path)
    } finally { $bitmap.Dispose() }
}

function Send-Key([byte] $VirtualKey) {
    [S17S18Native]::keybd_event($VirtualKey, 0, 0, [UIntPtr]::Zero)
    [S17S18Native]::keybd_event($VirtualKey, 0, 2, [UIntPtr]::Zero)
}

function Send-SaveAll($Root) {
    Invoke-Element (Find-ByName $Root '文件(F)')
    $save = Find-ByName $Root '保存全部'
    if (-not $save.Current.IsEnabled) { throw 'Save All remained disabled after a live mutation.' }
    Invoke-Element $save
    $results.operations.Add([ordered]@{ operation = 'save_all'; status = 'PASS'; utc = [DateTime]::UtcNow.ToString('o') })
}

function Find-ResourceButton($Root, [string] $DisplayName, [int] $TimeoutSeconds = 10) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $DisplayName)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            foreach ($label in $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)) {
                $button = Find-Ancestor $label { param($candidate)
                    $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                    $candidate.Current.BoundingRectangle.Left -lt 380
                }
                if ($null -ne $button -and -not $button.Current.IsOffscreen) { return $button }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Resource row '$DisplayName' has no visible left-library Button ancestor."
}

function Find-NodeBorder($Root, [string] $DisplayName, [int] $TimeoutSeconds = 10) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $DisplayName)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            foreach ($label in $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)) {
                $node = Find-Ancestor $label { param($candidate) $candidate.Current.AutomationId -like 'CanonicalGraphNode_*' }
                if ($null -ne $node -and -not $node.Current.IsOffscreen) { return $node }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Graph node '$DisplayName' was not found."
}

function Test-VisibleName($Root, [string] $Name) {
    try { $null = Find-ByName $Root $Name 1; return $true } catch { return $false }
}

function Test-VisibleNode($Root, [string] $DisplayName) {
    try { $null = Find-NodeBorder $Root $DisplayName 1; return $true } catch { return $false }
}

function Enter-Story($Root, [string] $StoryName) {
    $label = Find-ByName $Root $StoryName
    $listItem = Find-Ancestor $label { param($candidate) $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::ListItem }
    if ($null -eq $listItem) { throw 'Story ListItem ancestor was not found.' }
    Click-Element $listItem
    Invoke-Element (Find-ByName $Root '进入选中故事')
    $null = Find-ById $Root 'CanonicalStoryWorkspace' 15
}

function Configure-Settings {
    $settings = [ordered]@{
        schema_version = 1
        theme = 'Dark'
        window_width = 1540
        window_height = 900
        window_maximized = $false
        resource_browser_width = 280
        story_resource_library_width = 320
        bottom_panel_height = 180
        last_project = $project
        recent_projects = @($project)
    }
    [IO.File]::WriteAllText($SettingsPath, ($settings | ConvertTo-Json -Depth 6) + "`n", [Text.UTF8Encoding]::new($false))
}

function Start-Studio {
    Configure-Settings
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = [IO.Path]::GetFullPath($ExePath)
    $start.WorkingDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ExePath))
    $start.UseShellExecute = $false
    $start.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH'] = [IO.Path]::GetFullPath($SettingsPath)
    $instance = [Diagnostics.Process]::Start($start)
    $window = Find-Window $instance.Id
    [void][S17S18Native]::MoveWindow($window.Current.NativeWindowHandle, 20, 20, 1540, 900, $true)
    [void][S17S18Native]::SetForegroundWindow($window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 900
    return [pscustomobject]@{ Process = $instance; Window = $window }
}

function Stop-Studio($Instance) {
    if ($null -eq $Instance -or $Instance.HasExited) { return }
    $Instance.CloseMainWindow() | Out-Null
    if (-not $Instance.WaitForExit(10000)) { $Instance.Kill(); $Instance.WaitForExit(5000) }
}

function Read-DiskState {
    $membershipPath = Join-Path $project 'resources\canonical\memberships\s17_s18_gate.json'
    $storyPath = Join-Path $project 'resources\canonical\stories\s17_s18_gate.json'
    $membership = Get-Content -LiteralPath $membershipPath -Raw | ConvertFrom-Json
    $story = Get-Content -LiteralPath $storyPath -Raw | ConvertFrom-Json
    [pscustomobject]@{
        membership_owned_sessions = @($membership.owned_resources.sessions)
        membership_display_order_sessions = @($membership.display_order.sessions)
        session_a_file = Test-Path -LiteralPath (Join-Path $project 'resources\canonical\sessions\session_a.json')
        session_b_file = Test-Path -LiteralPath (Join-Path $project 'resources\canonical\sessions\session_b.json')
        session_c_file = Test-Path -LiteralPath (Join-Path $project 'resources\canonical\sessions\session_c.json')
        story_node_ids = @($story.graph.nodes | ForEach-Object { $_.id })
        story_connections = @($story.graph.connections | ForEach-Object { "{0}->{1}" -f $_.from_node_id, $_.to_node_id })
    }
}

function Assert-Disk($State, [string] $Phase, [bool] $ExpectA, [bool] $ExpectB, [bool] $ExpectC, [string[]] $ExpectedOrder, [string[]] $ForbiddenNodes) {
    $ownedSessionIds = @($State.membership_owned_sessions)
    $activeDisplayOrder = @($State.membership_display_order_sessions | Where-Object {
        $ownedSessionIds -contains $_
    })
    $checks = [ordered]@{
        session_a_file = $State.session_a_file -eq $ExpectA
        session_b_file = $State.session_b_file -eq $ExpectB
        session_c_file = $State.session_c_file -eq $ExpectC
        owned_sessions = (@($State.membership_owned_sessions) -join '|') -eq 'session_a|session_c'
        active_display_order_sessions = ($activeDisplayOrder -join '|') -eq ($ExpectedOrder -join '|')
    }
    foreach ($node in $ForbiddenNodes) { $checks["node_absent_$node"] = -not @($State.story_node_ids).Contains($node) }
    foreach ($check in $checks.GetEnumerator()) {
        if (-not $check.Value) { throw "$Phase disk assertion failed: $($check.Key)." }
    }
    $results.disk_assertions.Add([ordered]@{ phase = $Phase; status = 'PASS'; checks = $checks; state = $State })
}

function Get-VisibleSessionOrder($Root, [string[]] $DisplayNames) {
    $rows = foreach ($name in $DisplayNames) {
        $button = Find-ResourceButton $Root $name
        [pscustomobject]@{ name = $name; top = $button.Current.BoundingRectangle.Top }
    }
    return @($rows | Sort-Object top | ForEach-Object { $_.name })
}

try {
    if (-not (Test-Path -LiteralPath $ExePath)) { throw "Authoritative EXE not found: $ExePath" }
    foreach ($required in @($metadataPath, (Join-Path $project 'project.json'))) {
        if (-not (Test-Path -LiteralPath $required)) { throw "Required fixture input not found: $required" }
    }
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    $fixture = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $results.exe_sha256 = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash
    $results.exe_product_version = (Get-Item -LiteralPath $ExePath).VersionInfo.ProductVersion
    $results.fixture = $fixture

    $launch = Start-Studio
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window $fixture.story_display_name

    # S17a: remove only the first aggregate placement; the Session file and membership must remain.
    $placementA = Find-NodeBorder $window '会话 A'
    Click-Element $placementA
    [void][S17S18Native]::SetForegroundWindow($window.Current.NativeWindowHandle)
    Send-Key 0x2E
    Start-Sleep -Milliseconds 650
    if (Test-VisibleNode $window '会话 A') { throw 'Placement A remained visible after graph Delete.' }
    $null = Find-ResourceButton $window '会话 A'
    [void](Save-Screenshot $window 's17-placement-delete.png')
    Send-SaveAll $window
    $afterPlacement = Read-DiskState
    if (-not $afterPlacement.session_a_file -or -not @($afterPlacement.membership_owned_sessions).Contains('session_a') -or
        @($afterPlacement.story_node_ids).Contains('placement_a')) { throw 'Placement deletion changed the Session A resource or membership.' }
    $results.operations.Add([ordered]@{ operation = 'delete_placement'; target = 'placement_a'; status = 'PASS'; disk = $afterPlacement })

    # S17b: delete a different owned Session from the live resource library and accept the warning.
    $rowB = Find-ResourceButton $window '会话 B（删除）'
    Click-Element $rowB
    Invoke-Element (Find-ById $window 'CanonicalDeleteResource')
    $yes = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $buttons = $desktop.FindAll(
            [Windows.Automation.TreeScope]::Descendants,
            [Windows.Automation.PropertyCondition]::new(
                [Windows.Automation.AutomationElement]::ControlTypeProperty,
                [Windows.Automation.ControlType]::Button))
        $yes = $buttons | Where-Object {
            -not $_.Current.IsOffscreen -and
            ($_.Current.Name -like '是*' -or $_.Current.Name -like 'Yes*')
        } | Select-Object -First 1
        if ($null -eq $yes) { Start-Sleep -Milliseconds 100 }
    } while ($null -eq $yes -and [DateTime]::UtcNow -lt $deadline)
    if ($null -eq $yes) { throw 'Owned Session delete warning did not expose a Yes button.' }
    Invoke-Element $yes
    Start-Sleep -Milliseconds 1000
    if (Test-Path -LiteralPath (Join-Path $project 'resources\canonical\sessions\session_b.json')) { throw 'Session B file remained after confirmed delete.' }
    if (Test-VisibleName $window '会话 B（删除）') { throw 'Session B remained visible after confirmed delete.' }
    if (Test-VisibleNode $window '会话 B（删除）') { throw 'Session B placement remained visible after confirmed delete.' }
    [void](Save-Screenshot $window 's17-resource-delete.png')
    $afterResource = Read-DiskState
    Assert-Disk $afterResource 'after-resource-delete' $true $false $true @('session_a', 'session_c') @('placement_a', 'placement_b')
    if (@($afterResource.story_connections | Where-Object { $_ -like '*placement_b' -or $_ -like 'placement_b*' }).Count -ne 0) { throw 'Session B incident wire remained after resource deletion.' }
    $results.operations.Add([ordered]@{ operation = 'delete_owned_resource'; target = 'session_b'; warning_accepted = $true; status = 'PASS'; disk = $afterResource })

    # S18: physically drag C over the upper half of A, capture the live insertion preview, then drop.
    $rowC = Find-ResourceButton $window '会话 C'
    $rowA = Find-ResourceButton $window '会话 A'
    $sourceRect = $rowC.Current.BoundingRectangle
    $targetRect = $rowA.Current.BoundingRectangle
    $sourcePoint = [Drawing.Point]::new([int]($sourceRect.Left + $sourceRect.Width / 2), [int]($sourceRect.Top + $sourceRect.Height / 2))
    $targetPoint = [Drawing.Point]::new([int]($targetRect.Left + $targetRect.Width / 2), [int]($targetRect.Top + 3))
    [void][S17S18Native]::SetForegroundWindow($window.Current.NativeWindowHandle)
    [void][S17S18Native]::SetCursorPos($sourcePoint.X, $sourcePoint.Y)
    [S17S18Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    for ($step = 1; $step -le 16; $step++) {
        [void][S17S18Native]::SetCursorPos(
            [int]($sourcePoint.X + ($targetPoint.X - $sourcePoint.X) * $step / 16),
            [int]($sourcePoint.Y + ($targetPoint.Y - $sourcePoint.Y) * $step / 16))
        Start-Sleep -Milliseconds 35
    }
    Start-Sleep -Milliseconds 350
    [void](Save-Screenshot $window 's18-insertion-preview-before-mouse-up.png')
    [S17S18Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 800
    $orderBeforeRestart = Get-VisibleSessionOrder $window @('会话 A', '会话 C')
    if (($orderBeforeRestart -join '|') -ne '会话 C|会话 A') { throw "Visible S18 order before restart was $($orderBeforeRestart -join ', ')." }
    Send-SaveAll $window
    $afterReorder = Read-DiskState
    Assert-Disk $afterReorder 'after-reorder-save' $true $false $true @('session_c', 'session_a') @('placement_a', 'placement_b')
    $results.operations.Add([ordered]@{ operation = 'reorder'; source = 'session_c'; target = 'session_a'; placement = 'before'; preview_captured = $true; visible_order = $orderBeforeRestart; status = 'PASS' })

    Stop-Studio $process
    $process = $null
    $launch = Start-Studio
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window $fixture.story_display_name
    $orderAfterRestart = Get-VisibleSessionOrder $window @('会话 A', '会话 C')
    if (($orderAfterRestart -join '|') -ne '会话 C|会话 A') { throw "Visible S18 order after restart was $($orderAfterRestart -join ', ')." }
    $afterRestart = Read-DiskState
    Assert-Disk $afterRestart 'after-restart' $true $false $true @('session_c', 'session_a') @('placement_a', 'placement_b')
    $placementAReturned = Test-VisibleNode $window '会话 A'
    $placementBReturned = Test-VisibleNode $window '会话 B（删除）'
    if ($placementAReturned -or $placementBReturned) {
        throw 'A deleted lifecycle placement reappeared after restart.'
    }
    [void](Save-Screenshot $window 's18-after-restart.png')
    $results.visible_order_before_restart = $orderBeforeRestart
    $results.visible_order_after_restart = $orderAfterRestart
    $results.dpi = [S17S18Native]::GetDpiForWindow($window.Current.NativeWindowHandle)
    $results.status = 'PASS'
    $results.completed_utc = [DateTime]::UtcNow.ToString('o')
}
catch {
    $failure = $_
    $results.status = 'FAIL'
    $results.error = $_.Exception.Message
}
finally {
    try { Stop-Studio $process } catch { }
    $results.finished_utc = [DateTime]::UtcNow.ToString('o')
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    [IO.File]::WriteAllText($resultPath, ($results | ConvertTo-Json -Depth 16) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "RESULT_JSON=$resultPath"
}

$results | ConvertTo-Json -Depth 16
if ($null -ne $failure) { exit 1 }
