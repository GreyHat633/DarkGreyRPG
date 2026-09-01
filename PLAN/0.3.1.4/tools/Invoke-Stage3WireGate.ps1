[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage3-publish\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage3-wire-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage3-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\stage3\ui'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Stage3WireNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
}
'@

$repo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repo '.tooling')).TrimEnd('\')
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Stage 3 fixture must remain below the repository .tooling directory.'
}
foreach ($required in @($ExePath, $SettingsPath, (Join-Path $project 'project.json'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required Stage 3 input not found: $required" }
}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null

$desktop = [Windows.Automation.AutomationElement]::RootElement
$rawWalker = [Windows.Automation.TreeWalker]::RawViewWalker
$process = $null
$appDllPath = Join-Path (Split-Path -Parent $ExePath) 'DarkGreyRPGStudio.dll'
$results = [ordered]@{
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = [IO.Path]::GetFullPath($ExePath)
    exe_sha256 = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash
    app_dll_sha256 = if (Test-Path -LiteralPath $appDllPath) {
        (Get-FileHash -LiteralPath $appDllPath -Algorithm SHA256).Hash
    } else {
        $null
    }
    package_kind = if (Test-Path -LiteralPath $appDllPath) { 'multi-file' } else { 'single-file' }
    project = $project
    frames = [Collections.Generic.List[string]]::new()
}

function Find-Window([int] $ProcessId, [int] $TimeoutSeconds = 20) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $windows = $desktop.FindAll([Windows.Automation.TreeScope]::Children, $condition)
        foreach ($window in $windows) {
            if ($window.Current.AutomationId -eq 'RootWindow') { return $window }
        }
        Start-Sleep -Milliseconds 120
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio window for process $ProcessId was not found."
}

function Find-ByName($Root, [string] $Name, [int] $TimeoutSeconds = 8) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $items = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
            foreach ($item in $items) {
                $rect = $item.Current.BoundingRectangle
                if (-not $item.Current.IsOffscreen -and $rect.Width -gt 0 -and $rect.Height -gt 0) { return $item }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Visible element named '$Name' was not found."
}

function Find-ById($Root, [string] $AutomationId, [int] $TimeoutSeconds = 8) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $items = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
            foreach ($item in $items) {
                $rect = $item.Current.BoundingRectangle
                if (-not $item.Current.IsOffscreen -and $rect.Width -gt 0 -and $rect.Height -gt 0) { return $item }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $peers = @($Root.FindAll(
        [Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.Condition]::TrueCondition) | Where-Object {
            $_.Current.AutomationId -like 'CanonicalGraphPort_*'
        } | Select-Object -First 40 | ForEach-Object {
            "$($_.Current.AutomationId)|offscreen=$($_.Current.IsOffscreen)|$($_.Current.BoundingRectangle)"
        }) -join '; '
    throw "Visible element '$AutomationId' was not found. Port peers: $peers"
}

function Find-Ancestor($Element, [scriptblock] $Predicate) {
    for ($depth = 0; $depth -lt 18 -and $null -ne $Element; $depth++) {
        if (& $Predicate $Element) { return $Element }
        $Element = $rawWalker.GetParent($Element)
    }
    return $null
}

function Click-Point([double] $X, [double] $Y) {
    [void][Stage3WireNative]::SetCursorPos([int]$X, [int]$Y)
    [Stage3WireNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Stage3WireNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Click-Element($Element, [int] $Count = 1) {
    $rect = $Element.Current.BoundingRectangle
    for ($index = 0; $index -lt $Count; $index++) {
        Click-Point ($rect.Left + $rect.Width / 2) ($rect.Top + $rect.Height / 2)
        if ($Count -gt 1) { Start-Sleep -Milliseconds 90 }
    }
}

function Invoke-Named($Root, [string] $Name) {
    $element = Find-ByName $Root $Name
    $pattern = $null
    if ($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
    else { Click-Element $element }
    Start-Sleep -Milliseconds 250
}

function Open-Resource($Root, [string] $DisplayName) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $DisplayName)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $labels = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($label in $labels) {
            $button = Find-Ancestor $label { param($candidate)
                $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                $candidate.Current.BoundingRectangle.Left -lt 360
            }
            if ($null -ne $button -and -not $button.Current.IsOffscreen) {
                # The first click changes selection and can recycle the WPF row.
                # Re-resolve the live row before the opening double click so a
                # stale peer cannot leave the Inspector on the new resource
                # while the graph remains on the previous resource.
                Click-Element $button
                Start-Sleep -Milliseconds 220
                $liveLabel = Find-ByName $Root $DisplayName
                $liveButton = Find-Ancestor $liveLabel { param($candidate)
                    $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                    $candidate.Current.BoundingRectangle.Left -lt 360
                }
                if ($null -eq $liveButton) { throw "Live resource row '$DisplayName' was not found after selection." }
                Click-Element $liveButton 2
                Start-Sleep -Milliseconds 650
                Invoke-Named $Root '适应全部图节点'
                return
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Resource '$DisplayName' could not be opened."
}

function Get-PortPoint($Root, [string] $NodeId, [string] $PortId, [ValidateSet('Input','Output')] [string] $Direction) {
    $port = Find-ById $Root "CanonicalGraphPort_${NodeId}_${PortId}"
    $rect = $port.Current.BoundingRectangle
    # UIA reports the rendered port bounds (label plus 9-DIP anchor), not the
    # full transparent 20-DIP WPF hit surface. Aim at the rendered anchor edge
    # so the real mouse source is the anchor/hit grid rather than its label.
    $x = if ($Direction -eq 'Input') { $rect.Left + 4 } else { $rect.Right - 4 }
    $point = [Drawing.Point]::new(
        [int]$x,
        [int]($rect.Top + $rect.Height / 2))
    $hit = [Windows.Automation.AutomationElement]::FromPoint(
        [Windows.Point]::new([double]$point.X, [double]$point.Y))
    $hitId = if ($null -eq $hit) { '<null>' } else { $hit.Current.AutomationId }
    $hitName = if ($null -eq $hit) { '<null>' } else { $hit.Current.Name }
    Write-Host ("PORT_POINT id={0} direction={1} rect={2:F1},{3:F1},{4:F1},{5:F1} point={6},{7} hit_id={8} hit_name={9}" -f
        "CanonicalGraphPort_${NodeId}_${PortId}", $Direction,
        $rect.Left, $rect.Top, $rect.Width, $rect.Height,
        $point.X, $point.Y, $hitId, $hitName)
    return $point
}

function Move-Pointer([Drawing.Point] $From, [Drawing.Point] $To, [int] $Steps = 12) {
    for ($step = 1; $step -le $Steps; $step++) {
        [void][Stage3WireNative]::SetCursorPos(
            [int]($From.X + ($To.X - $From.X) * $step / $Steps),
            [int]($From.Y + ($To.Y - $From.Y) * $step / $Steps))
        Start-Sleep -Milliseconds 22
    }
}

function Save-Screenshot($Window, [string] $Name) {
    $rect = $Window.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size) }
        finally { $graphics.Dispose() }
        $path = [IO.Path]::GetFullPath((Join-Path $EvidenceDirectory $Name))
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        $results.frames.Add($path)
        return $path
    } finally { $bitmap.Dispose() }
}

function Send-Key([byte] $VirtualKey) {
    [Stage3WireNative]::keybd_event($VirtualKey, 0, 0, [UIntPtr]::Zero)
    [Stage3WireNative]::keybd_event($VirtualKey, 0, 2, [UIntPtr]::Zero)
}

function Send-Save($Root) {
    # Use the real visible File menu for this Gate so command dispatch is
    # independently observable rather than inferred from synthetic key state.
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, '保存全部')
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        Send-Key 0x1B
        [void][Stage3WireNative]::SetForegroundWindow($Root.Current.NativeWindowHandle)
        Click-Element (Find-ByName $Root '文件(F)')
        Start-Sleep -Milliseconds 300
        $items = $desktop.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($saveAll in $items) {
            $rect = $saveAll.Current.BoundingRectangle
            if ($saveAll.Current.IsOffscreen -or $rect.Width -le 0 -or $rect.Height -le 0) { continue }
            if (-not $saveAll.Current.IsEnabled) { throw 'The live Save All menu item remained disabled after a wire mutation.' }
            $pattern = $null
            if ($saveAll.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
            else { Click-Element $saveAll }
            Start-Sleep -Milliseconds 650
            return
        }
    }
    throw 'The visible Save All menu item was not found after three real File-menu attempts.'
}

function Invoke-LightClicks($Root, [string] $NodeId, [string] $PortId, [string] $Direction) {
    $point = Get-PortPoint $Root $NodeId $PortId $Direction
    for ($click = 1; $click -le 20; $click++) {
        Click-Point $point.X $point.Y
        Start-Sleep -Milliseconds 35
    }
}

function Invoke-DragSequence(
    $Root, $Window, [string] $Prefix,
    [string] $FromNode, [string] $FromPort, [string] $FromDirection,
    [string] $ToNode, [string] $ToPort, [string] $ToDirection,
    [switch] $Control, [switch] $FiveFrames) {
    $from = Get-PortPoint $Root $FromNode $FromPort $FromDirection
    $to = Get-PortPoint $Root $ToNode $ToPort $ToDirection
    $mid = [Drawing.Point]::new([int](($from.X + $to.X) / 2), [int](($from.Y + $to.Y) / 2 - 35))
    if ($FiveFrames) { [void](Save-Screenshot $Window "${Prefix}-frame1-before.png") }
    [void][Stage3WireNative]::SetCursorPos($from.X, $from.Y)
    if ($Control) { [Stage3WireNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero) }
    [Stage3WireNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    if ($FiveFrames) { [void](Save-Screenshot $Window "${Prefix}-frame2-down.png") }
    Move-Pointer $from $mid
    Start-Sleep -Milliseconds 120
    [void](Save-Screenshot $Window "${Prefix}-frame3-moving.png")
    Move-Pointer $mid $to
    Start-Sleep -Milliseconds 160
    [void](Save-Screenshot $Window "${Prefix}-frame4-hover.png")
    [Stage3WireNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    if ($Control) { [Stage3WireNative]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 300
    [void](Save-Screenshot $Window "${Prefix}-frame5-commit.png")
}

function Invoke-GlowLeave($Root, $Window) {
    $from = Get-PortPoint $Root 'flow_replacement' 'flow_out' 'Output'
    $valid = Get-PortPoint $Root 'flow_source' 'flow_out' 'Output'
    $outside = [Drawing.Point]::new($valid.X, [int]($valid.Y + 75))
    [void][Stage3WireNative]::SetCursorPos($from.X, $from.Y)
    [Stage3WireNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Move-Pointer $from $valid
    Start-Sleep -Milliseconds 150
    [void](Save-Screenshot $Window 'glow-frame1-valid.png')
    Move-Pointer $valid $outside 5
    Start-Sleep -Milliseconds 150
    [void](Save-Screenshot $Window 'glow-frame2-left.png')
    Send-Key 0x1B
    Start-Sleep -Milliseconds 150
}

function Start-Studio {
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
    $settings.theme = 'Dark'
    $settings.last_project = $project
    [IO.File]::WriteAllText($SettingsPath, ($settings | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = [IO.Path]::GetFullPath($ExePath)
    $start.WorkingDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ExePath))
    $start.UseShellExecute = $false
    $start.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH'] = [IO.Path]::GetFullPath($SettingsPath)
    $instance = [Diagnostics.Process]::Start($start)
    $window = Find-Window $instance.Id
    [void][Stage3WireNative]::MoveWindow($window.Current.NativeWindowHandle, 20, 20, 1540, 900, $true)
    [void][Stage3WireNative]::SetForegroundWindow($window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 800
    return [pscustomobject]@{ Process = $instance; Window = $window }
}

function Enter-Story($Root) {
    $story = Find-ByName $Root '0314 线手势门'
    $listItem = Find-Ancestor $story { param($candidate) $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::ListItem }
    if ($null -eq $listItem) { throw 'Story list item ancestor was not found.' }
    Click-Element $listItem
    Invoke-Named $Root '进入选中故事'
    [void](Find-ById $Root 'CanonicalStoryWorkspace' 12)
}

function Read-Connections([string] $RelativePath) {
    return @((Get-Content -LiteralPath (Join-Path $project $RelativePath) -Raw | ConvertFrom-Json).graph.connections)
}

try {
    $launch = Start-Studio
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window

    Open-Resource $window '线手势：Flow 单线'
    $flowSingleFile = 'resources\canonical\sessions\wire_flow_single.json'
    $preClickHash = (Get-FileHash -LiteralPath (Join-Path $project $flowSingleFile) -Algorithm SHA256).Hash
    Invoke-LightClicks $window 'flow_source' 'flow_out' 'Output'
    [void](Save-Screenshot $window 'light-click-20-no-phantom.png')
    if ((Get-FileHash -LiteralPath (Join-Path $project $flowSingleFile) -Algorithm SHA256).Hash -ne $preClickHash) {
        throw 'Twenty light clicks changed the Flow single resource on disk.'
    }
    $results.light_click_20 = [ordered]@{ status = 'PASS'; graph_sha256 = $preClickHash }

    Invoke-DragSequence $window $window 'flow-single' 'flow_source' 'flow_out' 'Output' 'flow_replacement' 'flow_out' 'Output' -FiveFrames
    Send-Save $window
    [void](Save-Screenshot $window 'flow-single-after-save-all.png')
    $flowSingleConnections = Read-Connections $flowSingleFile
    if ($flowSingleConnections.Count -ne 1 -or $flowSingleConnections[0].from_node_id -ne 'flow_replacement' -or
        $flowSingleConnections[0].to_node_id -ne 'flow_fixed') { throw 'Flow single same-direction reconnect did not persist.' }
    $results.flow_single = [ordered]@{ status = 'PASS'; connection = $flowSingleConnections[0] }
    Invoke-GlowLeave $window $window
    $results.glow_leave = [ordered]@{ status = 'PASS'; evidence = @('glow-frame1-valid.png','glow-frame2-left.png') }

    Open-Resource $window '线手势：Logic 单线'
    [void](Save-Screenshot $window 'logic-single-open.png')
    Invoke-DragSequence $window $window 'logic-single' 'logic_fixed' 'logic_in' 'Input' 'logic_replacement' 'logic_in' 'Input' -FiveFrames
    Send-Save $window
    $logicSingleFile = 'resources\canonical\tasks\wire_logic_single.json'
    $logicSingleConnections = Read-Connections $logicSingleFile
    if ($logicSingleConnections.Count -ne 1 -or $logicSingleConnections[0].from_node_id -ne 'logic_source' -or
        $logicSingleConnections[0].to_node_id -ne 'logic_replacement') { throw 'Logic single same-direction reconnect did not persist.' }
    $results.logic_single = [ordered]@{ status = 'PASS'; connection = $logicSingleConnections[0] }

    Open-Resource $window '线手势：Flow 多线'
    Invoke-DragSequence $window $window 'flow-multi-ordinary' 'flow_multi_target' 'flow_in' 'Input' 'flow_multi_extra' 'flow_out' 'Output'
    Send-Save $window
    $flowMultiFile = 'resources\canonical\sessions\wire_flow_multi.json'
    $flowAfterOrdinary = Read-Connections $flowMultiFile
    if ($flowAfterOrdinary.Count -ne 4 -or @($flowAfterOrdinary | Where-Object to_node_id -eq 'flow_multi_target').Count -ne 4) {
        throw 'Ordinary Flow Input drag did not add exactly one incident connection.'
    }
    Invoke-DragSequence $window $window 'flow-multi-ctrl' 'flow_multi_target' 'flow_in' 'Input' 'flow_multi_replacement' 'flow_in' 'Input' -Control
    Send-Save $window
    $flowAfterCtrl = Read-Connections $flowMultiFile
    if ($flowAfterCtrl.Count -ne 4 -or @($flowAfterCtrl | Where-Object to_node_id -eq 'flow_multi_replacement').Count -ne 4) {
        throw 'Ctrl Flow Input drag did not move the four-wire bundle.'
    }
    $results.flow_multi = [ordered]@{ status = 'PASS'; ordinary_count = $flowAfterOrdinary.Count; ctrl_bundle_count = $flowAfterCtrl.Count }

    Open-Resource $window '线手势：Logic 多线'
    Invoke-DragSequence $window $window 'logic-multi-ordinary' 'logic_multi_source' 'logic_out' 'Output' 'logic_multi_extra' 'logic_in' 'Input'
    Send-Save $window
    $logicMultiFile = 'resources\canonical\tasks\wire_logic_multi.json'
    $logicAfterOrdinary = Read-Connections $logicMultiFile
    if ($logicAfterOrdinary.Count -ne 4 -or @($logicAfterOrdinary | Where-Object from_node_id -eq 'logic_multi_source').Count -ne 4) {
        throw 'Ordinary Logic Output drag did not add exactly one incident connection.'
    }
    Invoke-DragSequence $window $window 'logic-multi-ctrl' 'logic_multi_source' 'logic_out' 'Output' 'logic_multi_replacement' 'logic_out' 'Output' -Control
    Send-Save $window
    $logicAfterCtrl = Read-Connections $logicMultiFile
    if ($logicAfterCtrl.Count -ne 4 -or @($logicAfterCtrl | Where-Object from_node_id -eq 'logic_multi_replacement').Count -ne 4) {
        throw 'Ctrl Logic Output drag did not move the four-wire bundle.'
    }
    $results.logic_multi = [ordered]@{ status = 'PASS'; ordinary_count = $logicAfterOrdinary.Count; ctrl_bundle_count = $logicAfterCtrl.Count }

    $results.dpi = [Stage3WireNative]::GetDpiForWindow($window.Current.NativeWindowHandle)
    $results.status = 'PASS'
    $results.completed_utc = [DateTime]::UtcNow.ToString('o')
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(8000)) { $process.Kill(); $process.WaitForExit(5000) }
    }
    $resultPath = Join-Path (Split-Path -Parent $EvidenceDirectory) 'stage3-wire-gate.json'
    [IO.File]::WriteAllText($resultPath, ($results | ConvertTo-Json -Depth 14) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "RESULT_JSON=$resultPath"
}

$results | ConvertTo-Json -Depth 14
