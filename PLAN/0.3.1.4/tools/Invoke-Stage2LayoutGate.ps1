[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ExePath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage2-publish\DarkGreyRPGStudio.exe',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage2-layout-project',
    [string] $SettingsPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage2-settings.json',
    [string] $EvidenceDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\0.3.1.4\evidence\stage2\ui'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Stage2LayoutNative {
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
    throw 'The live fixture must remain below the repository .tooling directory.'
}
foreach ($required in @($ExePath, $SettingsPath, (Join-Path $project 'project.json'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required Stage 2 input not found: $required" }
}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null

$desktop = [Windows.Automation.AutomationElement]::RootElement
$rawWalker = [Windows.Automation.TreeWalker]::RawViewWalker
$process = $null
$results = [ordered]@{
    started_utc = [DateTime]::UtcNow.ToString('o')
    exe = [IO.Path]::GetFullPath($ExePath)
    exe_sha256 = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash
    exe_product_version = (Get-Item -LiteralPath $ExePath).VersionInfo.ProductVersion
    project = $project
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
    throw "Visible element '$AutomationId' was not found."
}

function Click-Point([double] $X, [double] $Y, [int] $Count = 1) {
    [void][Stage2LayoutNative]::SetCursorPos([int]$X, [int]$Y)
    for ($index = 0; $index -lt $Count; $index++) {
        [Stage2LayoutNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [Stage2LayoutNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        if ($Count -gt 1) { Start-Sleep -Milliseconds 80 }
    }
}

function Click-Element($Element, [int] $Count = 1) {
    $rect = $Element.Current.BoundingRectangle
    Click-Point ($rect.Left + $rect.Width / 2) ($rect.Top + $rect.Height / 2) $Count
}

function Find-Ancestor($Element, [scriptblock] $Predicate) {
    for ($depth = 0; $depth -lt 18 -and $null -ne $Element; $depth++) {
        if (& $Predicate $Element) { return $Element }
        $Element = $rawWalker.GetParent($Element)
    }
    return $null
}

function Find-NodeBorder($Root, [string] $DisplayName) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $DisplayName)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        try {
            $labels = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
            foreach ($label in $labels) {
                $border = Find-Ancestor $label { param($candidate)
                    $candidate.Current.AutomationId -like 'CanonicalGraphNode_*'
                }
                if ($null -ne $border -and -not $border.Current.IsOffscreen) { return $border }
            }
        } catch { }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $diagnostic = @($Root.FindAll(
        [Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.Condition]::TrueCondition) | Where-Object {
            -not $_.Current.IsOffscreen -and $_.Current.Name -like '*布局*'
        } | Select-Object -First 30 | ForEach-Object {
            "$($_.Current.ControlType.ProgrammaticName)|$($_.Current.AutomationId)|$($_.Current.Name)"
        }) -join '; '
    throw "Node '$DisplayName' was not found in the active graph. Visible layout peers: $diagnostic"
}

function Drag-Node($Root, [string] $DisplayName, [int] $Dx, [int] $Dy) {
    $node = Find-NodeBorder $Root $DisplayName
    $rect = $node.Current.BoundingRectangle
    $fromX = [int]($rect.Left + $rect.Width / 2)
    $fromY = [int]($rect.Top + 12)
    [void][Stage2LayoutNative]::SetCursorPos($fromX, $fromY)
    [Stage2LayoutNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 12; $step++) {
        [void][Stage2LayoutNative]::SetCursorPos(
            [int]($fromX + $Dx * $step / 12),
            [int]($fromY + $Dy * $step / 12))
        Start-Sleep -Milliseconds 18
    }
    [Stage2LayoutNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 220
}

function Pan-Viewport($Root, [int] $Dx, [int] $Dy) {
    $viewport = Find-ById $Root 'CanonicalStoryWorkspaceGraph'
    $rect = $viewport.Current.BoundingRectangle
    $fromX = [int]($rect.Left + $rect.Width * 0.72)
    $fromY = [int]($rect.Top + $rect.Height * 0.76)
    [void][Stage2LayoutNative]::SetCursorPos($fromX, $fromY)
    [Stage2LayoutNative]::mouse_event(0x20, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 10; $step++) {
        [void][Stage2LayoutNative]::SetCursorPos(
            [int]($fromX + $Dx * $step / 10),
            [int]($fromY + $Dy * $step / 10))
        Start-Sleep -Milliseconds 18
    }
    [Stage2LayoutNative]::mouse_event(0x40, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 200
}

function Get-NodePoint($Root, [string] $DisplayName) {
    $rect = (Find-NodeBorder $Root $DisplayName).Current.BoundingRectangle
    return [ordered]@{ x = [Math]::Round($rect.Left, 3); y = [Math]::Round($rect.Top, 3) }
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
                Click-Element $button 2
                Start-Sleep -Milliseconds 350
                return
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Resource '$DisplayName' could not be opened from the canonical tree."
}

function Invoke-Breadcrumb($Root, [string] $DisplayName) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty, $DisplayName)
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $labels = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($label in $labels) {
            $button = Find-Ancestor $label { param($candidate)
                $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                $candidate.Current.BoundingRectangle.Left -gt 350
            }
            if ($null -ne $button -and -not $button.Current.IsOffscreen) {
                Click-Element $button
                Start-Sleep -Milliseconds 300
                return
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Breadcrumb '$DisplayName' was not found."
}

function Invoke-Named($Root, [string] $Name) {
    $element = Find-ByName $Root $Name
    $pattern = $null
    if ($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    } else {
        Click-Element $element
    }
    Start-Sleep -Milliseconds 250
}

function Reset-Graph($Root) {
    Invoke-Named $Root '图编辑器缩放到百分之百'
    Invoke-Named $Root '重置图视图'
}

function Save-Screenshot($Window, [string] $Name) {
    $rect = $Window.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size) }
        finally { $graphics.Dispose() }
        $path = Join-Path $EvidenceDirectory $Name
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        return [IO.Path]::GetFullPath($path)
    } finally { $bitmap.Dispose() }
}

function Send-SaveAll {
    [Stage2LayoutNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    [Stage2LayoutNative]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    [Stage2LayoutNative]::keybd_event(0x53, 0, 0, [UIntPtr]::Zero)
    [Stage2LayoutNative]::keybd_event(0x53, 0, 2, [UIntPtr]::Zero)
    [Stage2LayoutNative]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
    [Stage2LayoutNative]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 650
}

function Start-Studio([string] $Theme) {
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
    $settings.theme = $Theme
    $settings.last_project = $project
    [IO.File]::WriteAllText($SettingsPath, ($settings | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = [IO.Path]::GetFullPath($ExePath)
    $start.WorkingDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ExePath))
    $start.UseShellExecute = $false
    $start.Environment['DARKGREYRPG_STUDIO_SETTINGS_PATH'] = [IO.Path]::GetFullPath($SettingsPath)
    $instance = [Diagnostics.Process]::Start($start)
    $window = Find-Window $instance.Id
    [void][Stage2LayoutNative]::MoveWindow($window.Current.NativeWindowHandle, 20, 20, 1540, 900, $true)
    [void][Stage2LayoutNative]::SetForegroundWindow($window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 700
    return [pscustomobject]@{ Process = $instance; Window = $window }
}

function Enter-Story($Root) {
    $story = Find-ByName $Root '0314 布局门'
    $listItem = Find-Ancestor $story { param($candidate) $candidate.Current.ControlType -eq [Windows.Automation.ControlType]::ListItem }
    if ($null -eq $listItem) { throw 'Story list item ancestor was not found.' }
    Click-Element $listItem
    Invoke-Named $Root '进入选中故事'
    [void](Find-ById $Root 'CanonicalStoryWorkspace' 12)
}

function Close-Studio($Instance) {
    if ($null -eq $Instance -or $Instance.HasExited) { return }
    $Instance.CloseMainWindow() | Out-Null
    if (-not $Instance.WaitForExit(8000)) {
        throw 'Studio did not close cleanly; a dirty-layout prompt or hang remained.'
    }
}

$graphs = @(
    [ordered]@{ key='story'; resource=$null; node='布局开始'; dx=95; dy=55 },
    [ordered]@{ key='session_a'; resource='布局会话 A'; node='布局会话 A 台词'; dx=130; dy=35 },
    [ordered]@{ key='session_b'; resource='布局会话 B'; node='布局会话 B 台词'; dx=70; dy=110 },
    [ordered]@{ key='task_a'; resource='布局任务 A'; node='布局任务 A 目标'; dx=155; dy=75 },
    [ordered]@{ key='task_b'; resource='布局任务 B'; node='布局任务 B 目标'; dx=45; dy=145 }
)

try {
    $launch = Start-Studio 'Dark'
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window

    foreach ($graph in $graphs) {
        if ($null -ne $graph.resource) { Open-Resource $window $graph.resource }
        Drag-Node $window $graph.node $graph.dx $graph.dy
    }

    Open-Resource $window '布局会话 A'
    Reset-Graph $window
    Pan-Viewport $window 72 38
    $sessionPan = Get-NodePoint $window '布局会话 A 台词'
    Invoke-Breadcrumb $window '0314 布局门'
    Reset-Graph $window
    Pan-Viewport $window -64 51
    $storyPan = Get-NodePoint $window '布局开始'
    Open-Resource $window '布局会话 A'
    $sessionReturn = Get-NodePoint $window '布局会话 A 台词'
    Invoke-Breadcrumb $window '0314 布局门'
    $storyReturn = Get-NodePoint $window '布局开始'
    if ([Math]::Abs($sessionPan.x - $sessionReturn.x) -gt 1 -or [Math]::Abs($sessionPan.y - $sessionReturn.y) -gt 1 -or
        [Math]::Abs($storyPan.x - $storyReturn.x) -gt 1 -or [Math]::Abs($storyPan.y - $storyReturn.y) -gt 1) {
        throw 'Per-graph viewport state crossed or failed to restore when switching graphs.'
    }
    $results.viewport = [ordered]@{ status='PASS'; session=$sessionPan; story=$storyPan }

    $startNode = Find-NodeBorder $window '布局开始'
    Click-Element $startNode
    $nameBox = Find-ById $window 'StoryStartTriggerName'
    $valuePattern = $null
    if (-not $nameBox.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern, [ref]$valuePattern)) {
        throw 'Story Start trigger name does not expose ValuePattern.'
    }
    $valuePattern.SetValue('布局触发器（已重命名）')
    Invoke-Named $window '图编辑器缩放到百分之百'

    Send-SaveAll
    $layoutPath = Join-Path $project 'resources\editor\studio_layout.json'
    if (-not (Test-Path -LiteralPath $layoutPath)) { throw 'Save All did not create studio_layout.json.' }
    $layoutBeforeRestart = Get-Content -LiteralPath $layoutPath -Raw
    $layoutDocument = $layoutBeforeRestart | ConvertFrom-Json -AsHashtable
    $expectedKeys = @('story:layout_gate','session:layout_session_a','session:layout_session_b','task:layout_task_a','task:layout_task_b')
    foreach ($key in $expectedKeys) {
        if (-not $layoutDocument.graphs.ContainsKey($key)) { throw "Sidecar is missing graph key '$key'." }
    }

    $before = [ordered]@{}
    foreach ($graph in $graphs) {
        if ($null -eq $graph.resource) { Invoke-Breadcrumb $window '0314 布局门' } else { Open-Resource $window $graph.resource }
        Reset-Graph $window
        $before[$graph.key] = Get-NodePoint $window $graph.node
    }
    Invoke-Breadcrumb $window '0314 布局门'
    $darkBeforeShot = Save-Screenshot $window 'stage2-dark-before-restart.png'
    $sidecarHash = (Get-FileHash -LiteralPath $layoutPath -Algorithm SHA256).Hash
    Close-Studio $process
    $process = $null

    $launch = Start-Studio 'Dark'
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window
    $after = [ordered]@{}
    foreach ($graph in $graphs) {
        if ($null -eq $graph.resource) { Invoke-Breadcrumb $window '0314 布局门' } else { Open-Resource $window $graph.resource }
        Reset-Graph $window
        $after[$graph.key] = Get-NodePoint $window $graph.node
        $dx = [Math]::Abs($before[$graph.key].x - $after[$graph.key].x)
        $dy = [Math]::Abs($before[$graph.key].y - $after[$graph.key].y)
        if ($dx -gt 1 -or $dy -gt 1) { throw "Restart position drift for $($graph.key): dx=$dx dy=$dy" }
    }
    Invoke-Breadcrumb $window '0314 布局门'
    [void](Find-ByName $window '布局触发器（已重命名）' 8)
    $darkAfterShot = Save-Screenshot $window 'stage2-dark-after-restart.png'
    if ((Get-FileHash -LiteralPath $layoutPath -Algorithm SHA256).Hash -ne $sidecarHash) {
        throw 'Dark restart unexpectedly changed the Studio layout sidecar.'
    }
    Close-Studio $process
    $process = $null

    $launch = Start-Studio 'Light'
    $process = $launch.Process
    $window = $launch.Window
    Enter-Story $window
    Invoke-Breadcrumb $window '0314 布局门'
    Reset-Graph $window
    $lightPoint = Get-NodePoint $window '布局开始'
    $lightShot = Save-Screenshot $window 'stage2-light-after-restart.png'
    if ((Get-FileHash -LiteralPath $layoutPath -Algorithm SHA256).Hash -ne $sidecarHash) {
        throw 'Theme change modified the Studio layout sidecar.'
    }
    if ([Math]::Abs($after.story.x - $lightPoint.x) -gt 1 -or [Math]::Abs($after.story.y - $lightPoint.y) -gt 1) {
        throw 'Light theme changed the restored Story node position by more than 1 DIP.'
    }

    $results.status = 'PASS'
    $results.dpi = [Stage2LayoutNative]::GetDpiForWindow($window.Current.NativeWindowHandle)
    $results.layout_sha256 = $sidecarHash
    $results.graph_keys = $expectedKeys
    $results.positions_before = $before
    $results.positions_after_dark_restart = $after
    $results.story_position_light = $lightPoint
    $results.rename = '布局触发器（已重命名）'
    $results.screenshots = @($darkBeforeShot, $darkAfterShot, $lightShot)
    $results.completed_utc = [DateTime]::UtcNow.ToString('o')
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        try { Close-Studio $process } catch { $process.Kill(); $process.WaitForExit(5000) }
    }
    $resultPath = Join-Path (Split-Path -Parent $EvidenceDirectory) 'stage2-layout-gate.json'
    [IO.File]::WriteAllText($resultPath, ($results | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "RESULT_JSON=$resultPath"
}

$results | ConvertTo-Json -Depth 12
