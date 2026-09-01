[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s20-authoring-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s20-authoring-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\s20-canonical-authoring-smoke',
    [switch] $SkipProjectCreation
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class S20Native {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@

$repo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repo '.tooling')).TrimEnd('\')
$planEvidence = [IO.Path]::GetFullPath((Join-Path $repo 'PLAN\0.3.1.4\evidence')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$exe = [IO.Path]::GetFullPath($ExePath)
$settings = [IO.Path]::GetFullPath($SettingsPath)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory).TrimEnd('\')
$resultPath = Join-Path $evidence 's20-canonical-authoring-smoke.json'
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'S20 project must remain below .tooling.' }
if (-not $settings.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'S20 settings must remain below .tooling.' }
if (-not ($evidence.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase) -or $evidence.StartsWith($planEvidence + '\', [StringComparison]::OrdinalIgnoreCase))) { throw 'S20 evidence must remain below .tooling or PLAN/0.3.1.4/evidence.' }
if (-not (Test-Path -LiteralPath $exe)) { throw "Authoritative Release EXE not found: $exe" }
if (-not (Test-Path -LiteralPath $project)) { throw "S20 project not found: $project (run New-S20AuthoringSmokeProject.ps1 first)" }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

$desktop = [Windows.Automation.AutomationElement]::RootElement
$walker = [Windows.Automation.TreeWalker]::RawViewWalker
$state = [ordered]@{
    schema_version = 1
    status = 'FAIL'
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = $exe
    exe_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash
    exe_product_version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    project = $project
    evidence_directory = $evidence
    screenshots = [Collections.Generic.List[string]]::new()
    steps = [Collections.Generic.List[object]]::new()
    disk_assertions = [Collections.Generic.List[object]]::new()
    known_risks = @()
}
$process = $null
$script:knownCreatedNodeIds = @()

function Visible($element) {
    if ($null -eq $element) { return $false }
    try { $r = $element.Current.BoundingRectangle; return (-not $element.Current.IsOffscreen -and $r.Width -gt 0 -and $r.Height -gt 0) } catch { return $false }
}
function Find-Id($root, [string] $id, [int] $timeout = 12) {
    $c = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do { try { foreach ($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants, $c)) { $r=$x.Current.BoundingRectangle; if ((Visible $x) -or ($id -eq 'CanonicalGraphViewport' -and $r.Width -gt 0 -and $r.Height -gt 0)) { return $x } } } catch { }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $until)
    throw "Visible AutomationId '$id' was not found."
}
function Find-Name($root, [string] $name, [int] $timeout = 12) {
    $c = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, $name)
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do { try { foreach ($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants, $c)) { if (Visible $x) { return $x } } } catch { }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $until)
    throw "Visible element named '$name' was not found."
}
function Find-NameLike($root, [string] $text, [int] $timeout = 12) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        try { foreach ($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) { if ((Visible $x) -and $x.Current.Name -like "*$text*") { return $x } } } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    throw "Visible element containing '$text' was not found."
}
function Find-MenuItem($root, [string] $name, [int] $timeout = 8) {
    $nameCondition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, $name)
    $typeCondition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::MenuItem)
    $condition = [Windows.Automation.AndCondition]::new($nameCondition, $typeCondition)
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        try { foreach ($x in $root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)) { if (Visible $x) { return $x } } } catch { }
        Start-Sleep -Milliseconds 80
    } while ([DateTime]::UtcNow -lt $until)
    throw "Visible menu item '$name' was not found."
}
function Find-RightmostName($root, [string] $name, [int] $timeout = 8) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, $name)
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        try {
            $visible = @($root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition) | Where-Object { Visible $_ } | Sort-Object { $_.Current.BoundingRectangle.Left } -Descending)
            if ($visible.Count -gt 0) { return $visible[0] }
        } catch { }
        Start-Sleep -Milliseconds 80
    } while ([DateTime]::UtcNow -lt $until)
    throw "Visible element named '$name' was not found."
}
function Find-Prefix($root, [string] $property, [string] $prefix) {
    @($root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) | ? {
        (Visible $_) -and (($property -eq 'AutomationId' -and $_.Current.AutomationId.StartsWith($prefix, [StringComparison]::Ordinal)) -or ($property -eq 'Name' -and $_.Current.Name.StartsWith($prefix, [StringComparison]::Ordinal)))
    })
}
function Ancestor($element, [Windows.Automation.ControlType] $type) {
    for ($i = 0; $i -lt 20 -and $null -ne $element; $i++) { if ($element.Current.ControlType -eq $type) { return $element }; $element = $walker.GetParent($element) }
    return $null
}
function Invoke-Ui($element) {
    $pattern = $null
    if ($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
    else { $r = $element.Current.BoundingRectangle; [S20Native]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null; [S20Native]::mouse_event(2,0,0,0,[UIntPtr]::Zero); [S20Native]::mouse_event(4,0,0,0,[UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 250
}
function Click-ElementPhysical($element) {
    $r = $element.Current.BoundingRectangle
    if ($r.Width -le 0 -or $r.Height -le 0) { throw "Element '$($element.Current.Name)' has no physical click target." }
    Click-Point ($r.Left + $r.Width / 2) ($r.Top + $r.Height / 2)
}
function Hover-Element($element, [int] $settleMilliseconds = 450) {
    $r = $element.Current.BoundingRectangle
    if ($r.Width -le 0 -or $r.Height -le 0) { throw "Element '$($element.Current.Name)' has no physical hover target." }
    [S20Native]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds $settleMilliseconds
}
function Send-Key([byte] $virtualKey, [int] $settleMilliseconds = 350) {
    [S20Native]::keybd_event($virtualKey, 0, 0, [UIntPtr]::Zero)
    [S20Native]::keybd_event($virtualKey, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $settleMilliseconds
}
function DoubleClick-ElementPhysical($element) {
    $r = $element.Current.BoundingRectangle
    if ($r.Width -le 0 -or $r.Height -le 0) { throw "Element '$($element.Current.Name)' has no physical double-click target." }
    [S20Native]::SetCursorPos([int]($r.Left + $r.Width / 2),[int]($r.Top + $r.Height / 2)) | Out-Null
    1..2 | ForEach-Object {
        [S20Native]::mouse_event(2,0,0,0,[UIntPtr]::Zero); [S20Native]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
    }
    Start-Sleep -Milliseconds 500
}
function Begin-FolderCreate($main, [string] $folder) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$folder)
    $element = $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition) | Where-Object { Visible $_ } | Sort-Object { $_.Current.BoundingRectangle.Height }, { $_.Current.BoundingRectangle.Width } | Select-Object -First 1
    if ($null -eq $element) { throw "Visible folder '$folder' was not found." }
    Click-ElementPhysical $element
    $until = [DateTime]::UtcNow.AddSeconds(4); $create = $null
    do {
        try { $create = Find-Id $main 'CanonicalCreateResource' 1; if (-not $create.Current.IsEnabled) { $create = $null } } catch { $create = $null }
        if ($null -eq $create) { Start-Sleep -Milliseconds 80 }
    } while ($null -eq $create -and [DateTime]::UtcNow -lt $until)
    if ($null -eq $create) { throw "Folder '$folder' did not enable the canonical create button after a physical click." }
    Click-ElementPhysical $create
}
function Expand-Folder($main, [string] $folder) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$folder)
    foreach ($candidate in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)) {
        $pattern = $null
        if ($candidate.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$pattern)) {
            if ($pattern.Current.ExpandCollapseState -eq [Windows.Automation.ExpandCollapseState]::Collapsed) { $pattern.Expand(); Start-Sleep -Milliseconds 180 }
            return
        }
    }
    throw "Folder expander '$folder' was not found."
}
function Open-Resource($main, [string] $folder, [string] $displayName) {
    Expand-Folder $main $folder
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$displayName)
    $mainLeft = $main.Current.BoundingRectangle.Left
    $until = [DateTime]::UtcNow.AddSeconds(8); $button = $null
    do {
        foreach ($label in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)) {
            if (-not (Visible $label)) { continue }
            $candidate = Ancestor $label ([Windows.Automation.ControlType]::Button)
            if ($null -ne $candidate -and $candidate.Current.BoundingRectangle.Left -lt ($mainLeft + 400)) { $button = $candidate; break }
        }
        if ($null -eq $button) { Start-Sleep -Milliseconds 80 }
    } while ($null -eq $button -and [DateTime]::UtcNow -lt $until)
    if ($null -eq $button) { throw "Left resource row '$displayName' was not found." }
    $r = $button.Current.BoundingRectangle
    Click-Point ($r.Left + $r.Width / 2) ($r.Top + $r.Height / 2) -right
    Click-ElementPhysical (Find-Name $desktop '编辑' 5)
    Start-Sleep -Milliseconds 500
    Find-Id $main 'CanonicalStoryWorkspaceGraph' 12 | Out-Null
    Start-Sleep -Milliseconds 350
}
function Open-StoryFlow($main, [string] $displayName) {
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$displayName)
    $mainLeft = $main.Current.BoundingRectangle.Left
    $button = $null
    foreach ($label in $main.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)) {
        if (-not (Visible $label)) { continue }
        $candidate = Ancestor $label ([Windows.Automation.ControlType]::Button)
        if ($null -ne $candidate -and $candidate.Current.BoundingRectangle.Left -ge ($mainLeft + 400)) { $button = $candidate; break }
    }
    if ($null -eq $button) { throw "Story breadcrumb '$displayName' was not found." }
    Click-ElementPhysical $button; Find-Id $main 'CanonicalStoryWorkspaceGraph' 12 | Out-Null; Start-Sleep -Milliseconds 350
}
function Find-ButtonLike($root, [string] $text, [int] $timeout = 8) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        try {
            $buttons = @($root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) | ? { (Visible $_) -and $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and $_.Current.Name -like "*$text*" } | Sort-Object { $_.Current.BoundingRectangle.Top })
            if ($buttons.Count -gt 0) { return $buttons[-1] }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    throw "Visible dialog button containing '$text' was not found."
}
function Set-UiValue($element, [string] $value) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { throw "Element '$($element.Current.Name)' has no ValuePattern." }
    if ($pattern.Current.IsReadOnly) { throw "Element '$($element.Current.Name)' is read-only." }
    $element.SetFocus(); Start-Sleep -Milliseconds 80
    $pattern.SetValue($value); Start-Sleep -Milliseconds 80
    [S20Native]::keybd_event(0x09,0,0,[UIntPtr]::Zero); [S20Native]::keybd_event(0x09,0,2,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 180
}
function Set-ById($root, [string] $id, [string] $value) { Set-UiValue (Find-Id $root $id) $value }
function Get-ProcessWindows([int] $processId) {
    $c = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
    @($desktop.FindAll([Windows.Automation.TreeScope]::Descendants, $c) | ? { Visible $_ })
}
function Find-Main([int] $processId, [int] $timeout = 25) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do { foreach ($w in (Get-ProcessWindows $processId)) { if ($w.Current.AutomationId -eq 'RootWindow') { return $w } }; Start-Sleep -Milliseconds 120 } while ([DateTime]::UtcNow -lt $until)
    throw "Studio RootWindow for process $processId was not found."
}
function Find-Dialog([int] $processId, [int] $timeout = 15) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do { foreach ($w in (Get-ProcessWindows $processId)) { if ($w.Current.AutomationId -ne 'RootWindow' -and $w.Current.ControlType -eq [Windows.Automation.ControlType]::Window) { return $w } }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $until)
    throw "Studio modal dialog for process $processId was not found."
}
function VisibleTextBoxes($root) {
    @($root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Edit)) | ? { Visible $_ } | Sort-Object { $_.Current.BoundingRectangle.Top }, { $_.Current.BoundingRectangle.Left })
}
function Find-IdentityDialog([int] $processId, [int] $timeout = 12) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        foreach ($window in (Get-ProcessWindows $processId)) {
            if ($window.Current.AutomationId -eq 'RootWindow' -or $window.Current.ControlType -ne [Windows.Automation.ControlType]::Window) { continue }
            if (@(VisibleTextBoxes $window).Count -ge 2) { return $window }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    throw "Identity dialog with at least two text boxes was not found for process $processId."
}
function Drag-Point([double] $x1, [double] $y1, [double] $x2, [double] $y2, [switch] $right) {
    [S20Native]::SetCursorPos([int]$x1,[int]$y1) | Out-Null; if ($right) { [S20Native]::mouse_event(8,0,0,0,[UIntPtr]::Zero) } else { [S20Native]::mouse_event(2,0,0,0,[UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 120
    if ([math]::Abs($x2-$x1) -gt 1 -or [math]::Abs($y2-$y1) -gt 1) {
        for ($step = 1; $step -le 8; $step++) {
            $ratio = $step / 8.0
            [S20Native]::SetCursorPos([int]($x1 + (($x2-$x1)*$ratio)),[int]($y1 + (($y2-$y1)*$ratio))) | Out-Null
            Start-Sleep -Milliseconds 35
        }
    }
    Start-Sleep -Milliseconds 180
    if ($right) { [S20Native]::mouse_event(16,0,0,0,[UIntPtr]::Zero) } else { [S20Native]::mouse_event(4,0,0,0,[UIntPtr]::Zero) }; Start-Sleep -Milliseconds 300
}
function Click-Point([double] $x, [double] $y, [switch] $right) { Drag-Point $x $y $x $y -right:$right }
function Screenshot($window, [string] $name) {
    $r = $window.Current.BoundingRectangle; $bmp = [Drawing.Bitmap]::new([int]$r.Width,[int]$r.Height)
    try { $g = [Drawing.Graphics]::FromImage($bmp); try { $g.CopyFromScreen([int]$r.Left,[int]$r.Top,0,0,$bmp.Size) } finally { $g.Dispose() }; $path = Join-Path $evidence $name; $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png); $full = [IO.Path]::GetFullPath($path); $state.screenshots.Add($full); return $full } finally { $bmp.Dispose() }
}
function Step([int] $number, [string] $description, [scriptblock] $action) {
    $entry = [ordered]@{ step = $number; description = $description; status = 'FAIL'; started_utc = [DateTime]::UtcNow.ToString('o') }
    try { & $action; $entry.status = 'PASS' } catch { $entry.error = $_.Exception.Message; $state.steps.Add([pscustomobject]$entry); throw "S20 step $number failed ($description): $($_.Exception.Message)" }
    $entry.completed_utc = [DateTime]::UtcNow.ToString('o'); $state.steps.Add([pscustomobject]$entry)
}
function Configure-Settings {
    $cfg = [ordered]@{ schema_version = 1; theme = 'Dark'; window_width = 1540; window_height = 900; window_maximized = $false; last_project = $project; recent_projects = @($project) }
    [IO.File]::WriteAllText($settings, ($cfg | ConvertTo-Json -Depth 5) + "`n", [Text.UTF8Encoding]::new($false))
}
function Start-Studio {
    Configure-Settings
    $si = [Diagnostics.ProcessStartInfo]::new(); $si.FileName = $exe; $si.WorkingDirectory = Split-Path -Parent $exe; $si.UseShellExecute = $false; $si.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH'] = $settings
    $script:process = [Diagnostics.Process]::Start($si); $w = Find-Main $process.Id; [S20Native]::MoveWindow($w.Current.NativeWindowHandle,20,20,1540,900,$true) | Out-Null; [S20Native]::SetForegroundWindow($w.Current.NativeWindowHandle) | Out-Null; Start-Sleep -Milliseconds 900; return $w
}
function Stop-Studio {
    if ($null -eq $process -or $process.HasExited) { return }
    $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit(5000) }; $script:process = $null
}
function Enter-Story($main) {
    $story = Find-Name $main '史莱姆清理委托'; $item = Ancestor $story ([Windows.Automation.ControlType]::ListItem); if ($null -eq $item) { throw 'Story ListItem was not found.' }; Invoke-Ui $item; Invoke-Ui (Find-Name $main '进入选中故事'); Find-Id $main 'CanonicalStoryWorkspace' 15 | Out-Null
}
function Create-Identity([string] $id, [string] $display, [switch] $canonical) {
    $dialog = Find-IdentityDialog $process.Id; $boxes = @(VisibleTextBoxes $dialog)
    if ($boxes.Count -lt 2) { throw "Identity dialog exposed $($boxes.Count) text boxes; expected ID and display name." }
    Set-UiValue $boxes[0] $id; Set-UiValue $boxes[1] $display; Invoke-Ui (Find-ButtonLike $dialog '创建' 4)
}
function Create-Actor($main, [string] $id, [string] $display, [switch] $collective) {
    Begin-FolderCreate $main '角色'; $choice = Find-Dialog $process.Id
    if ($collective) { Invoke-Ui (Find-Name $choice '集体') }; Click-ElementPhysical (Find-ButtonLike $choice '继续' 5)
    $dialog = Find-IdentityDialog $process.Id; $boxes = @(VisibleTextBoxes $dialog); if ($boxes.Count -lt 2) { throw 'Actor identity dialog did not expose ID and display name fields.' }; Set-UiValue $boxes[0] $id; Set-UiValue $boxes[1] $display; Invoke-Ui (Find-ButtonLike $dialog '创建' 5)
}
function Create-Item($main, [string] $id, [string] $display) {
    Begin-FolderCreate $main '物品'; $choice = Find-Dialog $process.Id; Invoke-Ui (Find-Name $choice '物品'); Click-ElementPhysical (Find-ButtonLike $choice '继续' 5)
    $dialog = Find-IdentityDialog $process.Id; $boxes = @(VisibleTextBoxes $dialog); if ($boxes.Count -lt 2) { throw 'Item identity dialog did not expose ID and display name fields.' }; Set-UiValue $boxes[0] $id; Set-UiValue $boxes[1] $display; Invoke-Ui (Find-ButtonLike $dialog '创建' 5)
}
function Create-GraphResource($main, [string] $folder, [string] $id, [string] $display) {
    Begin-FolderCreate $main $folder; $dialog = Find-IdentityDialog $process.Id; $boxes = @(VisibleTextBoxes $dialog); if ($boxes.Count -lt 2) { throw 'Canonical resource dialog did not expose ID and display name fields.' }; Set-UiValue $boxes[0] $id; Set-UiValue $boxes[1] $display; Invoke-Ui (Find-ButtonLike $dialog '创建' 5)
}
function Graph-ViewportBounds($main) {
    $graph = Find-Id $main 'CanonicalStoryWorkspaceGraph'
    $outer = $graph.Current.BoundingRectangle
    $toolbarButton = Find-NameLike $graph '图编辑器缩放到百分之百' 5
    $toolbar = $toolbarButton.Current.BoundingRectangle
    $top = $toolbar.Bottom + 8
    return [ordered]@{ Left = $outer.Left; Top = $top; Width = $outer.Width; Height = $outer.Bottom - $top; Right = $outer.Right; Bottom = $outer.Bottom }
}
function Add-GraphNode($main, [string] $nodeLabel, [double] $x = 650, [double] $y = 420) {
    $before = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_' | ForEach-Object { $_.Current.AutomationId })
    $beforePorts = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphPort_' | ForEach-Object { $_.Current.AutomationId } | Select-Object -Unique)
    $r = Graph-ViewportBounds $main
    if ($x -lt 20 -or $y -lt 20 -or $x -gt ($r.Width - 20) -or $y -gt ($r.Height - 20)) { throw "Requested node point ($x,$y) is outside the live viewport." }
    Click-Point ($r.Left + $x) ($r.Top + $y) -right
    $menuPath = switch ($nodeLabel) {
        '台词' { @{ categoryDown = 0; nodeDown = 0; type = 'line'; scope = 'session' }; break }
        '选择' { @{ categoryDown = 0; nodeDown = 1; type = 'choice'; scope = 'session' }; break }
        '旁白' { @{ categoryDown = 0; nodeDown = 2; type = 'narration'; scope = 'session' }; break }
        '结束' { @{ categoryDown = 2; nodeDown = 0; type = 'end'; scope = 'session' }; break }
        '目标' { @{ categoryDown = 0; nodeDown = 0; type = 'objective'; scope = 'task' }; break }
        default { throw "S20 has no canonical authoring category mapping for node '$nodeLabel'." }
    }
    Send-Key 0x28
    Send-Key 0x27
    1..$menuPath.categoryDown | ForEach-Object { if ($menuPath.categoryDown -gt 0) { Send-Key 0x28 120 } }
    Send-Key 0x27
    1..$menuPath.nodeDown | ForEach-Object { if ($menuPath.nodeDown -gt 0) { Send-Key 0x28 120 } }
    Send-Key 0x0D 350
    $until = [DateTime]::UtcNow.AddSeconds(5); $newNodeIds = @()
    do {
        $newNodeIds = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_' | ForEach-Object { $_.Current.AutomationId } | Where-Object { $_ -notin $before } | Select-Object -Unique)
        if ($newNodeIds.Count -ne 1) { Start-Sleep -Milliseconds 100 }
    } while ($newNodeIds.Count -ne 1 -and [DateTime]::UtcNow -lt $until)
    if ($newNodeIds.Count -eq 1) {
        $resolved = $newNodeIds[0].Substring('CanonicalGraphNode_'.Length)
        $script:knownCreatedNodeIds += $resolved
        return $resolved
    }
    $newPortIds = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphPort_' | ForEach-Object { $_.Current.AutomationId } | Where-Object { $_ -notin $beforePorts } | Select-Object -Unique)
    $portNodeIds = @($newPortIds | ForEach-Object { if ($_ -match '^CanonicalGraphPort_(node_[0-9a-f]{32})_') { $matches[1] } } | Select-Object -Unique)
    if ($portNodeIds.Count -eq 1) {
        $script:knownCreatedNodeIds += $portNodeIds[0]
        return $portNodeIds[0]
    }
    Send-SaveAll
    $resourcePath = if ($menuPath.scope -eq 'session') {
        Join-Path $project 'resources\canonical\sessions\slime_cleanup_session.json'
    } else {
        Join-Path $project 'resources\canonical\tasks\slime_cleanup_task.json'
    }
    if (-not (Test-Path -LiteralPath $resourcePath)) { throw "Graph node '$nodeLabel' was visible but its persisted graph was not written: $resourcePath" }
    $persisted = Get-Content -LiteralPath $resourcePath -Raw | ConvertFrom-Json
    $expectedType = [string]$menuPath['type']
    $knownNodeIds = @($script:knownCreatedNodeIds)
    $persistedCandidates = @($persisted.graph.nodes | Where-Object { ([string]$_.type -eq $expectedType) -and ($knownNodeIds -notcontains [string]$_.id) })
    if ($persistedCandidates.Count -eq 1) {
        $resolved = [string]$persistedCandidates[0].id
        $script:knownCreatedNodeIds += $resolved
        return $resolved
    }
    $persistedSummary = @($persisted.graph.nodes | ForEach-Object { "{0}:{1}" -f $_.type,$_.id }) -join ', '
    throw "Graph node '$nodeLabel' creation exposed $($newNodeIds.Count) new node IDs, $($portNodeIds.Count) unique port owner IDs, and $($persistedCandidates.Count) unclaimed persisted candidates; expected type '$expectedType'; known '$($knownNodeIds -join ',')'; persisted '$persistedSummary'."
}
function Node($main, [string] $id) { Find-Id $main ("CanonicalGraphNode_{0}" -f $id) }
function Port($main, [string] $nodeId, [string] $portId) { Find-Id $main ("CanonicalGraphPort_{0}_{1}" -f $nodeId,$portId) }
function Port-Point($main, [string] $nodeId, [string] $portId, [ValidateSet('Input','Output')] [string] $direction) {
    $port = Port $main $nodeId $portId
    $r = $port.Current.BoundingRectangle
    $isChoiceOutput = $direction -eq 'Output' -and (Node $main $nodeId).Current.Name -like '*选择*'
    $x = if ($isChoiceOutput) { $r.Left + $r.Width / 2 } elseif ($direction -eq 'Input') { $r.Left + 4 } else { $r.Right - 4 }
    return [Drawing.Point]::new([int]$x,[int]($r.Top+$r.Height/2))
}
function Connect($main, [string] $fromNode, [string] $fromPort, [string] $toNode, [string] $toPort) {
    $a = Port-Point $main $fromNode $fromPort 'Output'; $b = Port-Point $main $toNode $toPort 'Input'; Drag-Point $a.X $a.Y $b.X $b.Y
}
function Move-Node($main, [string] $id, [double] $dx, [double] $dy) { $n = Node $main $id; $r = $n.Current.BoundingRectangle; Drag-Point ($r.Left+80) ($r.Top+20) ($r.Left+80+$dx) ($r.Top+20+$dy) }
function Select-Node($main, [string] $id) { Invoke-Ui (Node $main $id); Start-Sleep -Milliseconds 250 }
function Collapse-NodeParameters($main, [string] $id) {
    $summary = Find-Id (Node $main $id) 'CanonicalNodeParameterSummary' 3
    $pattern = $null
    if (-not $summary.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$pattern)) { throw "Node $id parameter expander did not expose ExpandCollapsePattern." }
    if ($pattern.Current.ExpandCollapseState -eq [Windows.Automation.ExpandCollapseState]::Expanded) { $pattern.Collapse(); Start-Sleep -Milliseconds 180 }
}
function Port-IdBy([object] $main, [string] $nodeId, [string] $direction, [string] $kind) {
    $prefix = "CanonicalGraphPort_${nodeId}_"
    $port = @(Find-Prefix $main 'AutomationId' $prefix | Where-Object {
        $_.Current.Name -like "*$direction*" -and $_.Current.Name -like "*($kind)*"
    } | Select-Object -First 1)
    if ($null -eq $port) { throw "$nodeId did not expose a $direction $kind port." }
    return $port.Current.AutomationId.Substring($prefix.Length)
}
function Node-Position([object] $main, [string] $nodeId) {
    $viewport = Graph-ViewportBounds $main
    $node = (Node $main $nodeId).Current.BoundingRectangle
    return [ordered]@{ x = [math]::Round($node.Left - $viewport.Left, 1); y = [math]::Round($node.Top - $viewport.Top, 1) }
}
function Assert-NodePosition([object] $main, [string] $nodeId, [object] $expected, [double] $tolerance = 3) {
    $actual = Node-Position $main $nodeId
    if ([math]::Abs($actual.x - $expected.x) -gt $tolerance -or [math]::Abs($actual.y - $expected.y) -gt $tolerance) {
        throw "Node $nodeId position changed across restart: expected ($($expected.x),$($expected.y)), actual ($($actual.x),$($actual.y))."
    }
}
function Assert-NodeDelta([object] $main, [string] $firstId, [string] $secondId, [object] $expectedFirst, [object] $expectedSecond, [double] $tolerance = 3) {
    $first = Node-Position $main $firstId; $second = Node-Position $main $secondId
    $expectedDx = $expectedSecond.x - $expectedFirst.x; $expectedDy = $expectedSecond.y - $expectedFirst.y
    $actualDx = $second.x - $first.x; $actualDy = $second.y - $first.y
    if ([math]::Abs($actualDx-$expectedDx) -gt $tolerance -or [math]::Abs($actualDy-$expectedDy) -gt $tolerance) {
        throw "Relative node layout changed across restart for $firstId -> ${secondId}: expected delta ($expectedDx,$expectedDy), actual ($actualDx,$actualDy)."
    }
}
function Select-ComboValue($root, [string] $automationId, [string] $value) {
    $combo = Find-Id $root $automationId
    $comboBounds = $combo.Current.BoundingRectangle
    $expand = $null
    if ($combo.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$expand)) { $expand.Expand() } else { Invoke-Ui $combo }
    $until = [DateTime]::UtcNow.AddSeconds(5); $item = $null
    do {
        try {
            $items = @($desktop.FindAll([Windows.Automation.TreeScope]::Descendants,
                [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)) |
                Where-Object { (Visible $_) -and $_.Current.Name -like "*$value*" -and $_.Current.BoundingRectangle.Width -ge ($comboBounds.Width * 0.7) })
            if ($items.Count -gt 0) { $item = $items | Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Left - $comboBounds.Left) } | Select-Object -First 1 }
        } catch { }
        if ($null -eq $item) { Start-Sleep -Milliseconds 50 }
    } while ($null -eq $item -and [DateTime]::UtcNow -lt $until)
    if ($null -eq $item) { throw "Popup item containing '$value' was not found for $automationId." }
    $r = $item.Current.BoundingRectangle
    Click-Point ($r.Left + $r.Width / 2) ($r.Top + $r.Height / 2)
    $until = [DateTime]::UtcNow.AddSeconds(5)
    do {
        try {
            $current = Find-Id $root $automationId 1; $selection = $null
            if ($current.TryGetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern,[ref]$selection)) {
                $selected = @($selection.Current.GetSelection())
                if ($selected.Count -eq 1 -and $selected[0].Current.Name -like "*$value*") { return }
            }
        } catch { }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $until)
    throw "$automationId did not retain popup selection '$value'."
}
function Send-SaveAll {
    foreach ($key in @(0x11,0x10,0x53)) { [S20Native]::keybd_event([byte]$key,0,0,[UIntPtr]::Zero) }
    foreach ($key in @(0x53,0x10,0x11)) { [S20Native]::keybd_event([byte]$key,0,2,[UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 1000
}
function Read-Json([string] $path) { if (-not (Test-Path -LiteralPath $path)) { throw "Expected persisted file missing: $path" }; Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
function Disk-Verify {
    $storyPath = Join-Path $project 'resources\canonical\stories\slime_cleanup_request.json'; $membershipPath = Join-Path $project 'resources\canonical\memberships\slime_cleanup_request.json'; $sessionPath = Join-Path $project 'resources\canonical\sessions\slime_cleanup_session.json'; $taskPath = Join-Path $project 'resources\canonical\tasks\slime_cleanup_task.json'; $layoutPath = Join-Path $project 'resources\editor\studio_layout.json'
    $story = Read-Json $storyPath; $membership = Read-Json $membershipPath; $session = Read-Json $sessionPath; $task = Read-Json $taskPath; $layout = Read-Json $layoutPath
    $storyNodes = @($story.graph.nodes); $storyEdges = @($story.graph.connections); $sessionNodes = @($session.graph.nodes); $sessionEdges = @($session.graph.connections); $taskNodes = @($task.graph.nodes); $taskEdges = @($task.graph.connections)
    $objective = $taskNodes | ? type -eq 'objective' | Select-Object -First 1; $choice = $sessionNodes | ? type -eq 'choice' | Select-Object -First 1; $sessionAggregate = $storyNodes | ? { $_.properties.resource_id -eq 'slime_cleanup_session' } | Select-Object -First 1; $taskAggregate = $storyNodes | ? { $_.properties.resource_id -eq 'slime_cleanup_task' } | Select-Object -First 1
    $assertions = [ordered]@{
        story_name = ($story.display_name -eq '史莱姆清理委托')
        actor_group_file = (Test-Path -LiteralPath (Join-Path $project 'actors\slimes.json'))
        actor_file = (Test-Path -LiteralPath (Join-Path $project 'actors\tavern_boss.json'))
        item_file = (Test-Path -LiteralPath (Join-Path $project 'items\copper_coin.json'))
        membership_actor_group = @($membership.owned_resources.actors) -contains 'slimes'
        membership_actor = @($membership.owned_resources.actors) -contains 'tavern_boss'
        membership_item = @($membership.owned_resources.items) -contains 'copper_coin'
        session_has_line = ($sessionNodes | ? type -eq 'line').Count -ge 1
        session_has_choice = ($null -ne $choice)
        choice_has_three_options = ($null -ne $choice -and @($choice.properties.options).Count -eq 3)
        choice_ports_are_mapped = ($null -ne $choice -and @($choice.properties.options | Where-Object { $_.option_id -and $_.flow_port_id }).Count -eq 3 -and @($choice.properties.options.option_id | Select-Object -Unique).Count -eq 3 -and @($choice.properties.options.flow_port_id | Select-Object -Unique).Count -eq 3)
        choice_options_are_separately_connected = ($null -ne $choice -and @($choice.properties.options | Where-Object { $option = $_; @($sessionEdges | Where-Object { $_.from_node_id -eq $choice.id -and $_.from_port_id -eq $option.flow_port_id }).Count -eq 1 }).Count -eq 3)
        task_objective_is_kill = ($null -ne $objective -and $objective.properties.objective_type -eq 'kill_entity' -and $objective.properties.entity -eq 'slimes' -and [int]$objective.properties.required -eq 10)
        task_has_settlement = ($taskNodes | ? type -eq 'settle').Count -eq 1
        task_objective_reaches_settlement = ($null -ne $objective -and @($taskEdges | Where-Object { $_.from_node_id -eq $objective.id -and $_.from_port_id -eq 'logic_status' }).Count -eq 1)
        story_flow_chain = ($null -ne $sessionAggregate -and $null -ne $taskAggregate -and ($storyEdges | ? { $_.from_node_id -eq 'start' -and $_.to_node_id -eq $sessionAggregate.id }).Count -eq 1 -and ($storyEdges | ? { $_.from_node_id -eq $sessionAggregate.id -and $_.to_node_id -eq $taskAggregate.id }).Count -eq 1)
        layout_has_graphs = (@($layout.graphs.PSObject.Properties).Count -ge 3)
        resource_order_present = ($null -ne $membership.display_order -and (@($membership.owned_resources.sessions) -join ',') -eq 'slime_cleanup_session' -and (@($membership.owned_resources.tasks) -join ',') -eq 'slime_cleanup_task' -and (@($membership.display_order.sessions).Count -eq 0 -or (@($membership.display_order.sessions) -join ',') -eq 'slime_cleanup_session') -and (@($membership.display_order.tasks).Count -eq 0 -or (@($membership.display_order.tasks) -join ',') -eq 'slime_cleanup_task'))
    }
    foreach ($key in $assertions.Keys) { $state.disk_assertions.Add([ordered]@{ assertion = $key; status = if ($assertions[$key]) { 'PASS' } else { 'FAIL' } }) }
    if (@($assertions.Values | ? { -not $_ }).Count -gt 0) { throw 'Persisted S20 verification had one or more failed assertions.' }
}

try {
    if (-not $SkipProjectCreation) {
        $creator = Join-Path $repo 'PLAN\0.3.1.4\tools\New-S20AuthoringSmokeProject.ps1'; if (-not (Test-Path -LiteralPath $creator)) { throw "S20 project creator missing: $creator" }; & $creator -RepositoryRoot $repo -ProjectDirectory $project | Out-Null
    }
    $main = Start-Studio
    Step 1 'Create Story 史莱姆清理委托' { Click-ElementPhysical (Find-Name $main '新建故事' 6); Create-Identity 'slime_cleanup_request' '史莱姆清理委托'; Enter-Story $main }
    Step 2 'Create collective actor group slimes' { Create-Actor $main 'slimes' '史莱姆' -collective }
    Step 3 'Create individual actor tavern_boss' { Create-Actor $main 'tavern_boss' '酒馆老板' }
    Step 4 'Create individual item copper_coin' { Create-Item $main 'copper_coin' '铜币' }
    Step 5 'Create Session slime_cleanup_session' { Create-GraphResource $main '会话' 'slime_cleanup_session' '史莱姆清理会话' }
    Step 6 'Add Session Line and Choice nodes' {
        Open-Resource $main '会话' '史莱姆清理会话'
        $line = Add-GraphNode $main '台词' 380 100
        $choice = Add-GraphNode $main '选择' 250 260
        $ends = @(
            (Add-GraphNode $main '台词' 20 400),
            (Add-GraphNode $main '台词' 480 400),
            (Add-GraphNode $main '台词' 20 260))
        foreach ($targetId in $ends) { Collapse-NodeParameters $main $targetId }
        $script:sessionExitId = Add-GraphNode $main '结束' 650 20
        $script:sessionNodeIds = @($line,$choice) + $ends
        $startNode = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_' | Where-Object { $_.Current.Name -like '*起始*' } | Select-Object -First 1)
        if ($null -eq $startNode) { throw 'Session Start node was not visible.' }
        $startId = $startNode.Current.AutomationId.Substring('CanonicalGraphNode_'.Length)
        Connect $main $startId 'flow_out' $line 'flow_in'; Connect $main $line 'flow_out' $choice 'flow_in'
        Select-Node $main $line
        $box = VisibleTextBoxes (Find-Id $main 'CanonicalInspectorScrollViewer') | Select-Object -First 1
        if ($null -eq $box) { throw 'Session Line text editor was not found.' }
        Set-UiValue $box '史莱姆正在骚扰酒馆，去清理它们。'
        Select-ComboValue $main 'SessionLineActorSelector' '酒馆老板'
    }
    Step 7 'Add three Choice options and separately connect each option' {
        Select-Node $main $sessionNodeIds[1]
        $choiceInspector = Find-Id $main 'CanonicalInspectorScrollViewer'
        1..2 | ForEach-Object { Invoke-Ui (Find-Name $choiceInspector '+' 5) }
        Start-Sleep -Milliseconds 350
        $labels = @('接受委托','询问报酬','稍后再说')
        for ($i=0; $i -lt 3; $i++) {
            $choiceBoxes = @(VisibleTextBoxes (Find-Id $main 'CanonicalInspectorScrollViewer') | Select-Object -Last 3)
            if ($choiceBoxes.Count -ne 3) { throw "Choice exposed $($choiceBoxes.Count) option editors; expected exactly three." }
            Set-UiValue $choiceBoxes[$i] $labels[$i]
        }
        Select-Node $main $sessionNodeIds[1]
        Send-SaveAll
        $sessionDocument = Get-Content -LiteralPath (Join-Path $project 'resources\canonical\sessions\slime_cleanup_session.json') -Raw | ConvertFrom-Json
        $choiceDocument = @($sessionDocument.graph.nodes | Where-Object { $_.id -eq $sessionNodeIds[1] }) | Select-Object -First 1
        $flowPortIds = @($choiceDocument.properties.options | ForEach-Object { [string]$_.flow_port_id })
        if ($flowPortIds.Count -ne 3 -or @($flowPortIds | Select-Object -Unique).Count -ne 3) { throw "Choice persisted $($flowPortIds.Count) Flow option mappings; expected three unique mappings." }
        Connect $main $sessionNodeIds[1] $flowPortIds[1] $sessionNodeIds[3] 'flow_in'
        Move-Node $main $sessionNodeIds[1] 180 0
        Invoke-Ui (Find-Name $main '适应全部图节点' 5)
        Connect $main $sessionNodeIds[1] $flowPortIds[0] $sessionNodeIds[2] 'flow_in'
        Move-Node $main $sessionNodeIds[1] 0 -150
        Invoke-Ui (Find-Name $main '适应全部图节点' 5)
        Connect $main $sessionNodeIds[1] $flowPortIds[2] $sessionNodeIds[4] 'flow_in'
    }
    Step 8 'Create Task slime_cleanup_task' { Create-GraphResource $main '任务' 'slime_cleanup_task' '清理史莱姆' }
    Step 9 'Configure Objective kill_entity slimes 10 and use the required Settlement' { Open-Resource $main '任务' '清理史莱姆'; $objective = Add-GraphNode $main '目标' 300 220; $settles = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_' | Where-Object { $_.Current.Name -like '*结算*' }); if ($settles.Count -ne 1) { throw "Task exposed $($settles.Count) required Settlement nodes; expected one." }; Select-Node $main $objective; Select-ComboValue $main 'TaskObjectiveTypeSelector' '实体击杀'; Set-ById $main 'TaskObjectiveDescription' '实体击杀'; Set-ById $main 'TaskObjectiveRequired' '10'; Select-ComboValue $main 'TaskObjectiveActorSelector' '史莱姆'; $script:objectiveNodeId = $objective }
    Step 10 'Connect Objective to Task Settlement' { $settle = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_' | ? { $_.Current.Name -like '*结算*' } | Select-Object -First 1); if ($null -eq $settle) { throw 'Task Settlement node was not visible.' }; $settleId = $settle.Current.AutomationId.Substring('CanonicalGraphNode_'.Length); $script:settleNodeId = $settleId; $settlePort = @(Find-Prefix $main 'AutomationId' ("CanonicalGraphPort_{0}_" -f $settleId) | ? { $_.Current.Name -like '*输入端口*' } | Select-Object -First 1); if ($null -eq $settlePort) { throw 'Task Settlement input port was not visible.' }; $portPrefix = "CanonicalGraphPort_{0}_" -f $settleId; $settlePortId = $settlePort.Current.AutomationId.Substring($portPrefix.Length); Connect $main $objectiveNodeId 'logic_status' $settleId $settlePortId }
    Step 11 'Drag Session and Task resources into Story Flow' { Open-StoryFlow $main '史莱姆清理委托'; Expand-Folder $main '会话'; Expand-Folder $main '任务'; $sessionItem = Ancestor (Find-Name $main '史莱姆清理会话') ([Windows.Automation.ControlType]::Button); $taskItem = Ancestor (Find-Name $main '清理史莱姆') ([Windows.Automation.ControlType]::Button); if ($null -eq $sessionItem -or $null -eq $taskItem) { throw 'Session or Task resource row did not expose a draggable Button ancestor.' }; $r = Graph-ViewportBounds $main; $targets = @([pscustomobject]@{ x=([double]$r.Left)+330; y=([double]$r.Top)+220 }, [pscustomobject]@{ x=([double]$r.Left)+520; y=([double]$r.Top)+380 }); $i=0; foreach ($item in @($sessionItem,$taskItem)) { $q=$item.Current.BoundingRectangle; Drag-Point ($q.Left+$q.Width/2) ($q.Top+$q.Height/2) ([double]$targets[$i].x) ([double]$targets[$i].y); $i++ }; Start-Sleep -Milliseconds 500 }
    Step 12 'Connect Story Start to Session to Task' { $nodes = @(Find-Prefix $main 'AutomationId' 'CanonicalGraphNode_'); $sessionAggregate = $nodes | Where-Object { $_.Current.Name -like '*史莱姆清理会话*' } | Select-Object -First 1; $taskAggregate = $nodes | Where-Object { $_.Current.Name -like '*清理史莱姆*' } | Select-Object -First 1; $start = $nodes | Where-Object { $_.Current.Name -like '*开始*' } | Select-Object -First 1; if ($null -eq $sessionAggregate -or $null -eq $taskAggregate -or $null -eq $start) { throw 'Story Flow Start/Session/Task nodes were not all visible.' }; $sid=$sessionAggregate.Current.AutomationId.Substring('CanonicalGraphNode_'.Length); $tid=$taskAggregate.Current.AutomationId.Substring('CanonicalGraphNode_'.Length); $stid=$start.Current.AutomationId.Substring('CanonicalGraphNode_'.Length); $startFlowPort = Port-IdBy $main $stid '输出端口' 'Flow'; $sessionFlowPort = Port-IdBy $main $sid '输出端口' 'Flow'; Connect $main $stid $startFlowPort $sid 'flow_in'; Connect $main $sid $sessionFlowPort $tid 'flow_in'; $script:storyNodeIds=@($sid,$tid) }
    Step 13 'Keep optional Give Item reward constrained to copper_coin' { # No reward action is added in this smoke; any future reward must be selected in the live UI and constrained to copper_coin.
    }
    Step 14 'Move multiple nodes through the graph UI' { $script:positionExpectations=[ordered]@{}; Open-Resource $main '会话' '史莱姆清理会话'; Invoke-Ui (Find-Name $main '图编辑器缩放到百分之百' 5); Move-Node $main $sessionNodeIds[0] 70 35; Move-Node $main $sessionNodeIds[1] -45 60; $positionExpectations.session_line=Node-Position $main $sessionNodeIds[0]; $positionExpectations.session_choice=Node-Position $main $sessionNodeIds[1]; Open-Resource $main '任务' '清理史莱姆'; Move-Node $main $objectiveNodeId 85 -30; $positionExpectations.task_objective=Node-Position $main $objectiveNodeId; $positionExpectations.task_settle=Node-Position $main $settleNodeId; Open-StoryFlow $main '史莱姆清理委托'; Move-Node $main $storyNodeIds[0] 55 45; Move-Node $main $storyNodeIds[1] -35 -40; $positionExpectations.story_session=Node-Position $main $storyNodeIds[0]; $positionExpectations.story_task=Node-Position $main $storyNodeIds[1] }
    Step 15 'Save All' { Send-SaveAll; Screenshot $main 'pre-close.png' | Out-Null }
    Step 16 'Close Studio cleanly' { Stop-Studio }
    Step 17 'Restart Studio and verify live resources and graph layout' { $main = Start-Studio; Enter-Story $main; foreach ($folder in @('角色','物品','会话','任务')) { Expand-Folder $main $folder }; foreach ($label in @('史莱姆','酒馆老板','铜币','史莱姆清理会话','清理史莱姆')) { Find-Name $main $label 8 | Out-Null }; Assert-NodeDelta $main $storyNodeIds[0] $storyNodeIds[1] $positionExpectations.story_session $positionExpectations.story_task; Open-Resource $main '会话' '史莱姆清理会话'; Assert-NodeDelta $main $sessionNodeIds[0] $sessionNodeIds[1] $positionExpectations.session_line $positionExpectations.session_choice; Open-Resource $main '任务' '清理史莱姆'; Assert-NodeDelta $main $objectiveNodeId $settleNodeId $positionExpectations.task_objective $positionExpectations.task_settle; Open-StoryFlow $main '史莱姆清理委托'; Screenshot $main 'post-restart.png' | Out-Null }
    Step 18 'Verify resources, connections, option mapping, layout, and order from disk' { Disk-Verify }
    Stop-Studio; $state.status = 'PASS'
} catch {
    $state.error = $_.Exception.Message
    try { if ($null -ne $process -and -not $process.HasExited) { $mainForFailure = Find-Main $process.Id 2; Screenshot $mainForFailure 'failure.png' | Out-Null } } catch { }
    try { Stop-Studio } catch { }
} finally {
    $state.completed_utc = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($resultPath, ($state | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
}

$state | ConvertTo-Json -Depth 20
if ($state.status -ne 'PASS') { exit 1 }
