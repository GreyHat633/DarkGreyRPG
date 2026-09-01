[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-choice-layout-live-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-choice-layout-live-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\final-live-post-choice-layout\choice',
    [switch] $ReviewOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ChoiceLayoutNative {
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
    throw 'The Choice layout fixture must remain below the repository .tooling directory.'
}
foreach ($required in @($ExePath, $SettingsPath, (Join-Path $project 'project.json'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required input not found: $required" }
}
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

$desktop = [Windows.Automation.AutomationElement]::RootElement
$rawWalker = [Windows.Automation.TreeWalker]::RawViewWalker
$sessionPath = Join-Path $project 'resources\canonical\sessions\test_session.json'
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
$choice = @($session.graph.nodes | Where-Object type -eq 'choice')
if ($choice.Count -ne 1) { throw "Expected one Choice node, found $($choice.Count)." }
$choice = $choice[0]
$options = @($choice.properties.options)
if ($options.Count -ne 10) { throw "Expected 10 Choice options, found $($options.Count)." }
$results = [ordered]@{
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = [IO.Path]::GetFullPath($ExePath)
    exe_sha256 = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash
    project = $project
    choice_node_id = $choice.id
    option_count = $options.Count
    original_connection_count = @($session.graph.connections).Count
    themes = [ordered]@{}
    reconnect = $null
    restart = $null
    screenshots = [Collections.Generic.List[string]]::new()
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
    throw "Visible element '$AutomationId' was not found."
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

function Find-Ancestor($Element, [scriptblock] $Predicate) {
    for ($depth = 0; $depth -lt 18 -and $null -ne $Element; $depth++) {
        if (& $Predicate $Element) { return $Element }
        $Element = $rawWalker.GetParent($Element)
    }
    return $null
}

function Click-Point([double] $X, [double] $Y) {
    [void][ChoiceLayoutNative]::SetCursorPos([int]$X, [int]$Y)
    [ChoiceLayoutNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [ChoiceLayoutNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
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
    Start-Sleep -Milliseconds 300
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
                Click-Element $button
                Start-Sleep -Milliseconds 220
                $liveLabel = Find-ByName $Root $DisplayName
                $liveButton = Find-Ancestor $liveLabel { param($candidate)
                    $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                    $candidate.Current.BoundingRectangle.Left -lt 360
                }
                if ($null -eq $liveButton) { throw "Live resource row '$DisplayName' was not found after selection." }
                Click-Element $liveButton 2
                Start-Sleep -Milliseconds 700
                Invoke-Named $Root '适应全部图节点'
                return
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Resource '$DisplayName' could not be opened."
}

function Save-Screenshot($Window, [string] $Name) {
    [void][ChoiceLayoutNative]::SetForegroundWindow($Window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 160
    $rect = $Window.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size) }
        finally { $graphics.Dispose() }
        $path = [IO.Path]::GetFullPath((Join-Path $EvidenceDirectory $Name))
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        $results.screenshots.Add($path)
        return $path
    } finally { $bitmap.Dispose() }
}

function Set-Theme([string] $Theme) {
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
    $settings.theme = $Theme
    $settings.last_project = $project
    $settings.recent_projects = @($project)
    [IO.File]::WriteAllText($SettingsPath, ($settings | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
}

function Start-Studio([string] $Theme) {
    Set-Theme $Theme
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = [IO.Path]::GetFullPath($ExePath)
    $start.WorkingDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ExePath)
    )
    $start.UseShellExecute = $false
    $start.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH'] = [IO.Path]::GetFullPath($SettingsPath)
    $instance = [Diagnostics.Process]::Start($start)
    $window = Find-Window $instance.Id
    [void][ChoiceLayoutNative]::MoveWindow($window.Current.NativeWindowHandle, 20, 20, 1540, 900, $true)
    [void][ChoiceLayoutNative]::SetForegroundWindow($window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 900
    return [pscustomobject]@{ Process = $instance; Window = $window }
}

function Ensure-ChoiceGraph($Root) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            [void](Find-ById $Root "CanonicalGraphNode_$($choice.id)" 4)
            Invoke-Named $Root '适应全部图节点'
            return
        } catch {
            if ($attempt -ge 3) { throw }
            Open-Resource $Root '测试会话'
        }
    }
}

function Enter-Session($Root) {
    try {
        $existingWorkspace = Find-ById $Root 'CanonicalStoryWorkspace' 2
        if ($null -ne $existingWorkspace) {
            Open-Resource $Root '测试会话'
            Ensure-ChoiceGraph $Root
            return
        }
    } catch { }

    $story = Find-ByName $Root '测试故事'
    $listItem = Find-Ancestor $story { param($candidate) $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::ListItem }
    if ($null -eq $listItem) { throw 'Story list item ancestor was not found.' }
    Click-Element $listItem
    Invoke-Named $Root '进入选中故事'
    [void](Find-ById $Root 'CanonicalStoryWorkspace' 12)
    Open-Resource $Root '测试会话'
    Ensure-ChoiceGraph $Root
}

function Get-OptionLabel($Node, [string] $Text, [double] $FlowTop, [double] $LogicTop) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $Text)
    $labels = $Node.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($label in $labels) {
        $rect = $label.Current.BoundingRectangle
        if (-not $label.Current.IsOffscreen -and $rect.Width -gt 2 -and
            $rect.Top -gt $FlowTop -and $rect.Top -lt $LogicTop) { return $label }
    }
    throw "Visible option label '$Text' was not found between its two endpoints."
}

function Measure-Layout($Root) {
    $node = Find-ById $Root "CanonicalGraphNode_$($choice.id)"
    $nodeRect = $node.Current.BoundingRectangle
    $input = Find-ById $Root "CanonicalGraphPort_$($choice.id)_flow_in"
    $inputRect = $input.Current.BoundingRectangle
    $dpi = [ChoiceLayoutNative]::GetDpiForWindow($Root.Current.NativeWindowHandle)
    $scale = $dpi / 96.0
    if (($inputRect.Left - $nodeRect.Left) -gt (16 * $scale)) {
        throw "Choice input is not on the left edge: nodeLeft=$($nodeRect.Left), inputLeft=$($inputRect.Left)."
    }
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($option in $options) {
        $flow = Find-ById $Root "CanonicalGraphPort_$($choice.id)_$($option.flow_port_id)"
        $logic = Find-ById $Root "CanonicalGraphPort_$($choice.id)_$($option.option_id)"
        $flowRect = $flow.Current.BoundingRectangle
        $logicRect = $logic.Current.BoundingRectangle
        $label = Get-OptionLabel $node $option.display_text $flowRect.Top $logicRect.Top
        $labelRect = $label.Current.BoundingRectangle
        if ($flowRect.Top -ge $labelRect.Top -or $labelRect.Top -ge $logicRect.Top) {
            throw "Choice vertical order failed for option '$($option.option_id)'."
        }
        if ([Math]::Abs($flowRect.Right - $logicRect.Right) -gt (2 * $scale)) {
            throw "Choice output X alignment failed for option '$($option.option_id)'."
        }
        if (($nodeRect.Right - $flowRect.Right) -gt (16 * $scale)) {
            throw "Choice outputs are not on the right edge for option '$($option.option_id)'."
        }
        $rows.Add([ordered]@{
            option_id = $option.option_id
            flow_port_id = $option.flow_port_id
            display_text = $option.display_text
            flow = [ordered]@{ left=$flowRect.Left; top=$flowRect.Top; right=$flowRect.Right; bottom=$flowRect.Bottom }
            label = [ordered]@{ left=$labelRect.Left; top=$labelRect.Top; right=$labelRect.Right; bottom=$labelRect.Bottom }
            logic = [ordered]@{ left=$logicRect.Left; top=$logicRect.Top; right=$logicRect.Right; bottom=$logicRect.Bottom }
        })
    }
    return [ordered]@{
        status = 'PASS'
        dpi = $dpi
        scale = $scale
        node = [ordered]@{ left=$nodeRect.Left; top=$nodeRect.Top; right=$nodeRect.Right; bottom=$nodeRect.Bottom }
        input = [ordered]@{ left=$inputRect.Left; top=$inputRect.Top; right=$inputRect.Right; bottom=$inputRect.Bottom }
        rows = @($rows)
    }
}

function Get-PortPoint($Root, [string] $NodeId, [string] $PortId) {
    $port = Find-ById $Root "CanonicalGraphPort_${NodeId}_${PortId}"
    $rect = $port.Current.BoundingRectangle
    # Choice endpoint controls contain only the 20-DIP hit target, so their
    # center is the most reliable point after Fit All scales a tall node down.
    # Generic output controls also contain a label and keep their anchor at
    # the right edge.
    $x = if ($NodeId -eq $choice.id) { $rect.Left + $rect.Width / 2 } else { $rect.Right - 4 }
    return [Drawing.Point]::new([int]$x, [int]($rect.Top + $rect.Height / 2))
}

function Move-Pointer([Drawing.Point] $From, [Drawing.Point] $To, [int] $Steps = 15) {
    for ($step = 1; $step -le $Steps; $step++) {
        [void][ChoiceLayoutNative]::SetCursorPos(
            [int]($From.X + ($To.X - $From.X) * $step / $Steps),
            [int]($From.Y + ($To.Y - $From.Y) * $step / $Steps))
        Start-Sleep -Milliseconds 24
    }
}

function Drag-Port($Root, $Window,
    [string] $FromNodeId, [string] $FromPortId,
    [string] $ToNodeId, [string] $ToPortId,
    [string] $Prefix) {
    $from = Get-PortPoint $Root $FromNodeId $FromPortId
    $to = Get-PortPoint $Root $ToNodeId $ToPortId
    [void][ChoiceLayoutNative]::SetForegroundWindow($Root.Current.NativeWindowHandle)
    [void][ChoiceLayoutNative]::SetCursorPos($from.X, $from.Y)
    [ChoiceLayoutNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 140
    [void](Save-Screenshot $Window "$Prefix-frame1-down.png")
    Move-Pointer $from $to
    Start-Sleep -Milliseconds 180
    [void](Save-Screenshot $Window "$Prefix-frame2-hover.png")
    [ChoiceLayoutNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450
    [void](Save-Screenshot $Window "$Prefix-frame3-commit.png")
}

function Send-Save {
    [ChoiceLayoutNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    [ChoiceLayoutNative]::keybd_event(0x53, 0, 0, [UIntPtr]::Zero)
    [ChoiceLayoutNative]::keybd_event(0x53, 0, 2, [UIntPtr]::Zero)
    [ChoiceLayoutNative]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700
}

function Stop-Studio($Launch) {
    if ($null -ne $Launch -and $null -ne $Launch.Process -and -not $Launch.Process.HasExited) {
        $Launch.Process.CloseMainWindow() | Out-Null
        if (-not $Launch.Process.WaitForExit(5000)) { $Launch.Process.Kill(); $Launch.Process.WaitForExit() }
    }
}

$launch = $null
try {
    $launch = Start-Studio 'Dark'
    Enter-Session $launch.Window
    [void](Save-Screenshot $launch.Window 'choice-dark-pre-measure.png')
    if ($ReviewOnly) {
        Write-Host "CHOICE_REVIEW_READY=YES"
        Write-Host "LIVE_PROCESS_ID=$($launch.Process.Id)"
        Write-Host "SCREENSHOT=$(Join-Path $EvidenceDirectory 'choice-dark-pre-measure.png')"
        return
    }
    $results.themes.dark = Measure-Layout $launch.Window
    [void](Save-Screenshot $launch.Window 'choice-dark-10-options.png')

    $first = $options[0]
    $startNode = @($session.graph.nodes | Where-Object id -eq 'start')
    if ($startNode.Count -ne 1) { throw 'The live Choice fixture requires the Start node.' }
    $startFlowOutput = @($startNode[0].ports | Where-Object { $_.kind -eq 'flow' -and $_.direction -eq 'output' })
    if ($startFlowOutput.Count -ne 1) { throw 'The live Choice fixture requires one Start Flow output.' }
    $startFlowOutput = $startFlowOutput[0]
    $before = @((Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json).graph.connections)
    Drag-Port $launch.Window $launch.Window $choice.id $first.flow_port_id 'start' $startFlowOutput.port_id 'choice-flow-reconnect-forward'
    Send-Save
    $moved = @((Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json).graph.connections)
    if (@($moved | Where-Object { $_.from_node_id -eq 'start' -and $_.from_port_id -eq $startFlowOutput.port_id }).Count -ne 1 -or
        @($moved | Where-Object from_port_id -eq $first.flow_port_id).Count -ne 0) {
        throw 'Physical reconnect did not move the Choice wire to the Start output.'
    }
    Drag-Port $launch.Window $launch.Window 'start' $startFlowOutput.port_id $choice.id $first.flow_port_id 'choice-flow-reconnect-restore'
    Send-Save
    $restored = @((Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json).graph.connections)
    if ($restored.Count -ne $before.Count -or
        @($restored | Where-Object from_port_id -eq $first.flow_port_id).Count -ne 1 -or
        @($restored | Where-Object { $_.from_node_id -eq 'start' -and $_.from_port_id -eq $startFlowOutput.port_id }).Count -ne 0) {
        throw 'Physical reconnect restore did not restore the original Choice source port.'
    }

    $logicPoint = Get-PortPoint $launch.Window $choice.id $first.option_id
    $preLogicHash = (Get-FileHash -LiteralPath $sessionPath -Algorithm SHA256).Hash
    for ($click = 1; $click -le 20; $click++) { Click-Point $logicPoint.X $logicPoint.Y; Start-Sleep -Milliseconds 35 }
    Send-Save
    $postLogicHash = (Get-FileHash -LiteralPath $sessionPath -Algorithm SHA256).Hash
    if ($preLogicHash -ne $postLogicHash) { throw 'Twenty light clicks on the Choice Logic anchor mutated the saved graph.' }
    $results.reconnect = [ordered]@{
        status = 'PASS'
        moved_to_option_6 = $true
        restored_to_option_1 = $true
        connection_count = $restored.Count
        logic_light_clicks = 20
        logic_click_hash_stable = $true
    }

    Stop-Studio $launch
    $launch = Start-Studio 'Light'
    Enter-Session $launch.Window
    $results.themes.light = Measure-Layout $launch.Window
    [void](Save-Screenshot $launch.Window 'choice-light-10-options.png')

    Stop-Studio $launch
    $launch = Start-Studio 'Dark'
    Enter-Session $launch.Window
    $restartLayout = Measure-Layout $launch.Window
    $restartConnections = @((Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json).graph.connections)
    if ($restartConnections.Count -ne $results.original_connection_count) {
        throw 'Connection count changed after process restart.'
    }
    $results.restart = [ordered]@{
        status = 'PASS'
        connection_count = $restartConnections.Count
        layout = $restartLayout
        live_process_id = $launch.Process.Id
    }
    [void](Save-Screenshot $launch.Window 'choice-dark-after-process-restart.png')

    $results.status = 'PASS'
    $results.finished_utc = [DateTime]::UtcNow.ToString('o')
    $resultPath = Join-Path $EvidenceDirectory 'choice-port-layout-gate.json'
    [IO.File]::WriteAllText($resultPath, ($results | ConvertTo-Json -Depth 14) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "CHOICE_PORT_LAYOUT_GATE=PASS"
    Write-Host "RESULT=$resultPath"
    Write-Host "LIVE_PROCESS_ID=$($launch.Process.Id)"
} catch {
    $results.status = 'FAIL'
    $results.failure = $_.Exception.Message
    $results.finished_utc = [DateTime]::UtcNow.ToString('o')
    $resultPath = Join-Path $EvidenceDirectory 'choice-port-layout-gate.json'
    [IO.File]::WriteAllText($resultPath, ($results | ConvertTo-Json -Depth 14) + "`n", [Text.UTF8Encoding]::new($false))
    Stop-Studio $launch
    throw
}
