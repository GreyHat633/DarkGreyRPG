param([string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M6NativeInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$settingsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\settings.json"
$studioDll = Join-Path $RepositoryRoot "Studio\src\DarkGreyRPG.Studio\bin\Debug\net10.0-windows\DarkGreyRPGStudio.dll"
$dotnetPath = "E:\Java\dotnet-sdk-10\dotnet.exe"
$screenshotsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\screenshots"
$storyPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance\stories\royal_mystery.json"
New-Item -ItemType Directory -Force -Path $screenshotsPath | Out-Null

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

function Assert-NamedElementAbsent {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutMilliseconds = 2500)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -eq $element) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' should be absent."
}

function Find-StudioWindow {
    param([int]$ProcessId, [int]$TimeoutMilliseconds = 12000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $windows.Count; $index++) {
            $window = $windows.Item($index)
            if ($window.Current.ProcessId -eq $ProcessId -and $window.Current.Name -eq "DarkGrey RPG Studio 2.1") { return $window }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio main window was not found."
}

function Select-ListItemContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName)
    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 12 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 300
            return
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable list item contains '$ChildName'."
}

function Invoke-NamedButton {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $button = Find-NamedElement $Root $Name
    if (-not $button.Current.IsEnabled) { throw "Button '$Name' is disabled." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 350
}

function Save-WindowScreenshot {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$FileName)
    $bounds = $Window.Current.BoundingRectangle
    $width = [Math]::Max(1, [int][Math]::Ceiling($bounds.Width))
    $height = [Math]::Max(1, [int][Math]::Ceiling($bounds.Height))
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int][Math]::Floor($bounds.X), [int][Math]::Floor($bounds.Y), 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save((Join-Path $screenshotsPath $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Move-MouseDrag {
    param([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [switch]$RightButton)
    [void][M6NativeInput]::SetCursorPos($FromX, $FromY)
    Start-Sleep -Milliseconds 120
    if ($RightButton) { [M6NativeInput]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero) }
    else { [M6NativeInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero) }
    for ($step = 1; $step -le 12; $step++) {
        $x = [int]($FromX + (($ToX - $FromX) * $step / 12))
        $y = [int]($FromY + (($ToY - $FromY) * $step / 12))
        [void][M6NativeInput]::SetCursorPos($x, $y)
        Start-Sleep -Milliseconds 20
    }
    if ($RightButton) { [M6NativeInput]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero) }
    else { [M6NativeInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 450
}

function Start-Studio {
    $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
    $process = Start-Process -FilePath $dotnetPath -ArgumentList @($studioDll) -PassThru
    $window = Find-StudioWindow $process.Id
    [void][M6NativeInput]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
    return @{ Process = $process; Window = $window }
}

function Enter-RoyalMysteryFlow {
    param([System.Windows.Automation.AutomationElement]$Window)
    [void](Find-NamedElement $Window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    Select-ListItemContaining $Window "最终验收主剧情。"
    Invoke-NamedButton $Window "进入剧情"
    Select-ListItemContaining $Window "流程"
    Save-WindowScreenshot $Window "00-after-flow-navigation.png"
    [void](Find-NamedElement $Window "Story Flow 自由节点画布")
}

function Close-Studio {
    param($Studio)
    if (-not $Studio.Process.HasExited) { $Studio.Process.Kill(); [void]$Studio.Process.WaitForExit(5000) }
}

$before = Get-Content -LiteralPath $storyPath -Raw | ConvertFrom-Json
$beforeStart = $before.nodes | Where-Object id -eq "start"
$dragDeltaX = if ($beforeStart.position.x -lt 80) { 40 } else { -40 }
$first = Start-Studio
try {
    Enter-RoyalMysteryFlow $first.Window
    foreach ($name in @(
        "Story Flow 自由节点画布", "流程缩小", "流程放大", "保存 Story Flow",
        "Flow 节点 剧情开始 start", "start 输出端口 next", "start_evidence 输入端口",
        "Flow 节点 按 Dialogue Exit 分支 exit_branch", "exit_branch 输出端口 hand_over", "exit_branch 输出端口 conceal")) {
        [void](Find-NamedElement $first.Window $name)
    }

    Invoke-NamedButton $first.Window "流程放大"
    Invoke-NamedButton $first.Window "流程缩小"
    Invoke-NamedButton $first.Window "start 输出端口 next"
    Invoke-NamedButton $first.Window "start_evidence 输入端口"

    $start = Find-NamedElement $first.Window "Flow 节点 剧情开始 start"
    $startBounds = $start.Current.BoundingRectangle
    Move-MouseDrag ([int]($startBounds.X + 80)) ([int]($startBounds.Y + 18)) ([int]($startBounds.X + 80 + $dragDeltaX)) ([int]($startBounds.Y + 48))

    $start = Find-NamedElement $first.Window "Flow 节点 剧情开始 start"
    $startEvidence = Find-NamedElement $first.Window "Flow 节点 开始任务 start_evidence"
    $a = $start.Current.BoundingRectangle
    $b = $startEvidence.Current.BoundingRectangle
    Move-MouseDrag ([int]([Math]::Min($a.X, $b.X) - 8)) ([int]([Math]::Min($a.Y, $b.Y) - 8)) ([int]([Math]::Max($a.Right, $b.Right) + 8)) ([int]([Math]::Max($a.Bottom, $b.Bottom) + 8))
    [void][M6NativeInput]::SetForegroundWindow([IntPtr]$first.Window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 250
    [System.Windows.Forms.SendKeys]::SendWait("^c")
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    [void](Find-NamedElement $first.Window "Flow 节点 剧情开始 start_copy")
    [void](Find-NamedElement $first.Window "Flow 节点 开始任务 start_evidence_copy")
    [System.Windows.Forms.SendKeys]::SendWait("^z")
    Assert-NamedElementAbsent $first.Window "Flow 节点 剧情开始 start_copy"
    [System.Windows.Forms.SendKeys]::SendWait("^y")
    [void](Find-NamedElement $first.Window "Flow 节点 剧情开始 start_copy")
    [System.Windows.Forms.SendKeys]::SendWait("{DELETE}")
    Assert-NamedElementAbsent $first.Window "Flow 节点 剧情开始 start_copy"

    Invoke-NamedButton $first.Window "保存 Story Flow"
    Save-WindowScreenshot $first.Window "01-m6-free-flow-canvas.png"
    $windowBounds = $first.Window.Current.BoundingRectangle
    for ($pan = 0; $pan -lt 4; $pan++) {
        Move-MouseDrag ([int]($windowBounds.X + 980)) ([int]($windowBounds.Y + 710)) ([int]($windowBounds.X + 630)) ([int]($windowBounds.Y + 710)) -RightButton
    }
    Move-MouseDrag ([int]($windowBounds.X + 980)) ([int]($windowBounds.Y + 710)) ([int]($windowBounds.X + 750)) ([int]($windowBounds.Y + 710)) -RightButton
    Save-WindowScreenshot $first.Window "02-m6-named-exit-branches.png"
}
finally { Close-Studio $first }

$saved = Get-Content -LiteralPath $storyPath -Raw | ConvertFrom-Json
$savedStart = $saved.nodes | Where-Object id -eq "start"
if ([Math]::Abs($savedStart.position.x - $beforeStart.position.x) -lt 30 -or $savedStart.position.y -lt ($beforeStart.position.y + 15)) { throw "Dragged node position was not persisted." }
if ($saved.nodes.Count -ne 8 -or $saved.connections.Count -ne 7) { throw "Copy/paste/undo/delete did not restore the original graph." }
if (-not ($saved.connections | Where-Object { $_.from -eq "exit_branch" -and $_.output -eq "hand_over" -and $_.to -eq "enter_kingdom" })) { throw "Kingdom named exit connection is missing." }
if (-not ($saved.connections | Where-Object { $_.from -eq "exit_branch" -and $_.output -eq "conceal" -and $_.to -eq "enter_empire" })) { throw "Empire named exit connection is missing." }

$restart = Start-Studio
try {
    Enter-RoyalMysteryFlow $restart.Window
    [void](Find-NamedElement $restart.Window "Flow 节点 剧情开始 start")
    [void](Find-NamedElement $restart.Window "exit_branch 输出端口 hand_over")
    [void](Find-NamedElement $restart.Window "exit_branch 输出端口 conceal")
    Save-WindowScreenshot $restart.Window "03-m6-restart-persistence.png"
}
finally { Close-Studio $restart }

Write-Output "M6_UI_FREE_NODE_DRAG=PASS"
Write-Output "M6_UI_PORT_CONNECTION=PASS"
Write-Output "M6_UI_BOX_COPY_PASTE_UNDO_REDO_DELETE=PASS"
Write-Output "M6_UI_RESTART_PERSISTENCE=PASS"
Write-Output "M6_UI_AUTOMATION_TREE=PASS"
