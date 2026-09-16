param([string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGreyRPG")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Studio211NativeInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$dotnet = "E:\Java\dotnet-sdk-10\dotnet.exe"
$studioDll = Join-Path $RepositoryRoot "studio\src\DarkGreyRPG.Studio\bin\Release\net10.0-windows\DarkGreyRPGStudio.dll"
$sourceSettings = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\settings.json"
$sourceProject = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance"
$sourceStories = Join-Path $sourceProject "stories"
$toolingRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".tooling"))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $toolingRoot "2.1.1-ui-work"))
if (-not $qaRoot.StartsWith($toolingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe QA working directory: $qaRoot"
}
if (Test-Path -LiteralPath $qaRoot) { Remove-Item -LiteralPath $qaRoot -Recurse -Force }
New-Item -ItemType Directory -Path $qaRoot | Out-Null
$qaProject = Join-Path $qaRoot "DarkGrey-2.1-Acceptance"
Copy-Item -LiteralPath $sourceProject -Destination $qaProject -Recurse
$qaRecoveryDirectory = Join-Path $qaProject "resources\editor\recovery"
if (Test-Path -LiteralPath $qaRecoveryDirectory) { Remove-Item -LiteralPath $qaRecoveryDirectory -Recurse -Force }
$settings = Join-Path $qaRoot "settings.json"
$settingsModel = Get-Content -LiteralPath $sourceSettings -Raw | ConvertFrom-Json
$settingsModel.last_project = $qaProject
$settingsModel | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settings -Encoding utf8
$screenshots = Join-Path $RepositoryRoot ".tooling\2.1.1-acceptance\screenshots"
$stories = Join-Path $qaProject "stories"
New-Item -ItemType Directory -Force -Path $screenshots | Out-Null
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

function Find-NamedElement {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutMilliseconds = 12000)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' was not found."
}

function Find-NamedElementContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Fragment, [int]$TimeoutMilliseconds = 12000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $element = $matches | Where-Object { $_.Current.Name -like "*$Fragment*" } | Select-Object -First 1
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element containing '$Fragment' was not found."
}

function Find-LargestNamedElement {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($matches.Count -eq 0) { throw "UI element '$Name' was not found." }
    return $matches | Sort-Object { $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height } -Descending | Select-Object -First 1
}

function Find-AutomationId {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$AutomationId, [int]$TimeoutMilliseconds = 12000)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element with AutomationId '$AutomationId' was not found."
}

function Assert-NamedElementAbsent {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    if ($null -ne $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)) { throw "UI element '$Name' should be absent." }
}

function Find-StudioWindow {
    param([int]$ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $windows.Count; $index++) {
            $window = $windows.Item($index)
            if ($window.Current.ProcessId -eq $ProcessId -and $window.Current.Name -like "DarkGrey RPG Studio*") { return $window }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio main window was not found."
}

function Invoke-NamedButton {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $button = Find-NamedElement $Root $Name
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
}

function Select-ListItemContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName)
    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 12 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 350
            return
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable list item contains '$ChildName'."
}

function Mouse-Drag {
    param([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [ValidateSet('Left','Middle','Right')]$Button)
    $down = @{ Left = 0x0002; Middle = 0x0020; Right = 0x0008 }[$Button]
    $up = @{ Left = 0x0004; Middle = 0x0040; Right = 0x0010 }[$Button]
    [void][Studio211NativeInput]::SetCursorPos($FromX, $FromY)
    Start-Sleep -Milliseconds 100
    [Studio211NativeInput]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 12; $step++) {
        [void][Studio211NativeInput]::SetCursorPos([int]($FromX + ($ToX - $FromX) * $step / 12), [int]($FromY + ($ToY - $FromY) * $step / 12))
        Start-Sleep -Milliseconds 20
    }
    [Studio211NativeInput]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}

function Right-Click {
    param([int]$X, [int]$Y)
    [void][Studio211NativeInput]::SetCursorPos($X, $Y)
    [Studio211NativeInput]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
    [Studio211NativeInput]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Find-BlankFlowCanvasPoint {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [System.Windows.Automation.AutomationElement]$Canvas)
    $bounds = $Canvas.Current.BoundingRectangle
    $nodes = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.Name -like "Flow 节点 *" -and $_.Current.BoundingRectangle.Width -gt 0 }
    foreach ($yRatio in @(0.88, 0.72, 0.28, 0.5)) {
        foreach ($xRatio in @(0.88, 0.12, 0.68, 0.32, 0.5)) {
            $candidate = [System.Drawing.Point]::new(
                [int]($bounds.X + $bounds.Width * $xRatio),
                [int]($bounds.Y + $bounds.Height * $yRatio))
            $occupied = $false
            foreach ($node in $nodes) {
                $rect = $node.Current.BoundingRectangle
                if ($candidate.X -ge ($rect.Left - 12) -and $candidate.X -le ($rect.Right + 12) -and
                    $candidate.Y -ge ($rect.Top - 12) -and $candidate.Y -le ($rect.Bottom + 12)) {
                    $occupied = $true
                    break
                }
            }
            if (-not $occupied) { return $candidate }
        }
    }
    throw "No blank Flow canvas point was available for the context-menu acceptance."
}

function Save-Screenshot {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$Name)
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int][Math]::Ceiling($bounds.Width), [int][Math]::Ceiling($bounds.Height))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $screenshots $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

$hashesBefore = Get-ChildItem -LiteralPath $sourceStories -Filter '*.json' -File | Sort-Object Name | ForEach-Object { "$($_.Name):$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
$env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settings
$process = Start-Process -FilePath $dotnet -ArgumentList @($studioDll) -PassThru
try {
    $window = Find-StudioWindow $process.Id
    $handle = [IntPtr]$window.Current.NativeWindowHandle
    [void][Studio211NativeInput]::MoveWindow($handle, 40, 30, 1700, 980, $true)
    [void][Studio211NativeInput]::SetForegroundWindow($handle)
    [void](Find-NamedElement $window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")

    $storyNavigation = Find-NamedElement $window "剧情导航列表"
    Select-ListItemContaining $storyNavigation "royal_mystery"
    Invoke-NamedButton $window "进入选中剧情"
    Select-ListItemContaining $window "流程"
    [void](Find-NamedElement $window "节点内资源选择器")
    [void](Find-NamedElementContaining $window "wait_evidence 参数：参数（1）")
    [void](Find-NamedElement $window "start 输出端口 next")
    [void](Find-NamedElement $window "exit_branch 输出端口 hand_over")
    Assert-NamedElementAbsent $window "Flow 资源选择器"

    $start = Find-NamedElement $window "Flow 节点 剧情开始 start"
    $beforePan = $start.Current.BoundingRectangle
    [void][Studio211NativeInput]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 150
    Mouse-Drag ([int]($beforePan.X + 80)) ([int]($beforePan.Y + 18)) ([int]($beforePan.X + 200)) ([int]($beforePan.Y + 18)) Middle
    $afterPan = (Find-NamedElement $window "Flow 节点 剧情开始 start").Current.BoundingRectangle
    if ([Math]::Abs($afterPan.X - $beforePan.X) -lt 60) { throw "Middle-button Flow pan did not move the viewport." }

    [void][Studio211NativeInput]::SetCursorPos([int]($afterPan.X + 80), [int]($afterPan.Y + 30))
    [void][Studio211NativeInput]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 150
    [Studio211NativeInput]::mouse_event(0x0800, 0, 0, 360, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 350
    $afterZoom = (Find-NamedElement $window "Flow 节点 剧情开始 start").Current.BoundingRectangle
    Write-Output "FLOW_ZOOM_BOUNDS_BEFORE=$($afterPan.X),$($afterPan.Y),$($afterPan.Width),$($afterPan.Height)"
    Write-Output "FLOW_ZOOM_BOUNDS_AFTER=$($afterZoom.X),$($afterZoom.Y),$($afterZoom.Width),$($afterZoom.Height)"
    if ($afterZoom.Width -le ($afterPan.Width + 5)) { throw "Cursor wheel zoom did not enlarge the Flow node." }

    $outputPort = Find-NamedElement $window "start 输出端口 next"
    $inputPort = Find-NamedElement $window "wait_evidence 输入端口"
    $outputBounds = $outputPort.Current.BoundingRectangle
    $inputBounds = $inputPort.Current.BoundingRectangle
    Mouse-Drag ([int]($outputBounds.X + $outputBounds.Width / 2)) ([int]($outputBounds.Y + $outputBounds.Height / 2)) ([int]($inputBounds.X + $inputBounds.Width / 2)) ([int]($inputBounds.Y + $inputBounds.Height / 2)) Left
    [void](Find-NamedElement $window "未保存")
    Save-Screenshot $window "01b-flow-drag-connect-dirty.png"
    Write-Output "FLOW_DRAG_CONNECT_DIRTY=PASS"

    $flowCanvas = Find-AutomationId $window "StoryFlowCanvasViewport"
    $flowBounds = $flowCanvas.Current.BoundingRectangle
    for ($iteration = 0; $iteration -lt 10; $iteration++) {
        $beforeRecoveryPan = (Find-NamedElement $window "Flow 节点 剧情开始 start").Current.BoundingRectangle
        Mouse-Drag ([int]($beforeRecoveryPan.Right - 7)) ([int]($beforeRecoveryPan.Y + $beforeRecoveryPan.Height * 0.49)) ([int]($flowBounds.Right - 70)) ([int]($flowBounds.Bottom - 70)) Left
        $panDelta = if (($iteration % 2) -eq 0) { 50 } else { -50 }
        Mouse-Drag ([int]($flowBounds.Right - 140)) ([int]($flowBounds.Bottom - 110)) ([int]($flowBounds.Right - 140 + $panDelta)) ([int]($flowBounds.Bottom - 110)) Middle
        $afterRecoveryPan = (Find-NamedElement $window "Flow 节点 剧情开始 start").Current.BoundingRectangle
        if ([Math]::Abs($afterRecoveryPan.X - $beforeRecoveryPan.X) -lt 20) { throw "Middle-button Flow pan stayed blocked after port cancellation at iteration $iteration." }
    }
    Write-Output "FLOW_PORT_CANCEL_MIDDLE_PAN_10X=PASS"

    $blankContextPoint = Find-BlankFlowCanvasPoint $window $flowCanvas
    [void][Studio211NativeInput]::SetForegroundWindow($handle)
    Right-Click $blankContextPoint.X $blankContextPoint.Y
    [void](Find-NamedElement $desktop "添加节点")
    Save-Screenshot $window "01-flow-inline-pan-zoom-context.png"

    $process.Kill()
    [void]$process.WaitForExit(5000)
    $process = Start-Process -FilePath $dotnet -ArgumentList @($studioDll) -PassThru
    $window = Find-StudioWindow $process.Id
    $handle = [IntPtr]$window.Current.NativeWindowHandle
    [void][Studio211NativeInput]::MoveWindow($handle, 40, 30, 1700, 980, $true)
    [void][Studio211NativeInput]::SetForegroundWindow($handle)
    [void](Find-NamedElement $window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    Invoke-NamedButton $window "剧情图谱"
    [void](Find-NamedElement $window "剧情图谱自由布局画布")
    Assert-NamedElementAbsent $window "剧情图谱诊断列表"
    $graphNode = Find-NamedElement $window "剧情图谱节点 王城迷案 royal_mystery"
    $graphNodeBounds = $graphNode.Current.BoundingRectangle
    Write-Output "GRAPH_NODE_BOUNDS=$($graphNodeBounds.Width),$($graphNodeBounds.Height)"
    if (($graphNodeBounds.Height / $graphNodeBounds.Width) -ge 0.5) { throw "Project Graph node density regression: node is not compact." }
    $graphCanvas = Find-AutomationId $window "ProjectGraphCanvasViewport"
    $graphBounds = $graphCanvas.Current.BoundingRectangle
    Mouse-Drag ([int]($graphBounds.Right - 140)) ([int]($graphBounds.Bottom - 110)) ([int]($graphBounds.Right - 80)) ([int]($graphBounds.Bottom - 110)) Middle
    $graphNodeAfterPan = (Find-NamedElement $window "剧情图谱节点 王城迷案 royal_mystery").Current.BoundingRectangle
    if ([Math]::Abs($graphNodeAfterPan.X - $graphNodeBounds.X) -lt 25) { throw "Middle-button Project Graph pan did not move the viewport." }
    Write-Output "PROJECT_GRAPH_MIDDLE_PAN=PASS"
    Right-Click ([int]($graphBounds.Right - 45)) ([int]($graphBounds.Bottom - 45))
    [void](Find-NamedElement $desktop "自动布局")
    Save-Screenshot $window "02-project-graph-read-only-context.png"
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")

    [void][Studio211NativeInput]::MoveWindow($handle, 80, 80, 1100, 700, $true)
    Start-Sleep -Milliseconds 400
    $graphCanvas = Find-AutomationId $window "ProjectGraphCanvasViewport"
    $minimumWindowBounds = $window.Current.BoundingRectangle
    $minimumCanvasBounds = $graphCanvas.Current.BoundingRectangle
    Write-Output "MINIMUM_WINDOW_AND_CANVAS_WIDTH=$($minimumWindowBounds.Width),$($minimumCanvasBounds.Width)"
    if (($minimumCanvasBounds.Width / $minimumWindowBounds.Width) -lt 0.45) { throw "Project Graph canvas is not the primary workspace at 1100x700." }
    Save-Screenshot $window "03-minimum-window-1100x700.png"
}
finally {
    if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) }
    if (Test-Path -LiteralPath $qaRoot) { Remove-Item -LiteralPath $qaRoot -Recurse -Force }
}

$hashesAfter = Get-ChildItem -LiteralPath $sourceStories -Filter '*.json' -File | Sort-Object Name | ForEach-Object { "$($_.Name):$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
if (($hashesBefore -join '|') -ne ($hashesAfter -join '|')) { throw "2.1.1 read-only UI acceptance modified Runtime Story JSON." }

Write-Output "STUDIO_211_FLOW_INLINE_UI=PASS"
Write-Output "STUDIO_211_MIDDLE_PAN=PASS"
Write-Output "STUDIO_211_CURSOR_ZOOM=PASS"
Write-Output "STUDIO_211_DRAG_CONNECT=PASS"
Write-Output "STUDIO_211_CONTEXT_MENUS=PASS"
Write-Output "STUDIO_211_GRAPH_DENSITY=PASS"
Write-Output "STUDIO_211_MINIMUM_WINDOW=PASS"
Write-Output "STUDIO_211_STORY_JSON_UNCHANGED=PASS"
Write-Output "STUDIO_212_PORT_CANCEL_MIDDLE_PAN=PASS"
Write-Output "STUDIO_212_PROJECT_GRAPH_MIDDLE_PAN=PASS"
