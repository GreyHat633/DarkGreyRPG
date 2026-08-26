param([string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M7NativeInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$settingsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\settings.json"
$studioDll = Join-Path $RepositoryRoot "Studio\src\DarkGreyRPG.Studio\bin\Debug\net10.0-windows\DarkGreyRPGStudio.dll"
$dotnetPath = "E:\Java\dotnet-sdk-10\dotnet.exe"
$screenshotsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\screenshots"
$layoutPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance\resources\editor\story-graph-layout.json"
$storiesPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance\stories"
$storyHashesBefore = Get-ChildItem -LiteralPath $storiesPath -File -Filter "*.json" | Sort-Object Name | ForEach-Object { "$($_.Name):$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
New-Item -ItemType Directory -Force -Path $screenshotsPath | Out-Null

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
        if ($null -eq $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' should be absent."
}

function Find-StudioWindow {
    param([int]$ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds(12)
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

function Invoke-NamedButton {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $button = Find-NamedElement $Root $Name
    if (-not $button.Current.IsEnabled) { throw "Button '$Name' is disabled." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
}

function Set-NamedValue {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [string]$Value)
    $element = Find-NamedElement $Root $Name
    $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
    Start-Sleep -Milliseconds 350
}

function Set-ComboSelection {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ComboName, [string]$ItemName)
    $combo = Find-NamedElement $Root $ComboName
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand(); Start-Sleep -Milliseconds 250
    $item = Find-NamedElement $Root $ItemName
    for ($depth = 0; $depth -lt 8 -and $null -ne $item; $depth++) {
        if ($item.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { break }
        $item = $walker.GetParent($item)
    }
    if ($null -eq $item -or $item.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) { throw "Combo item '$ItemName' is not selectable." }
    $itemBounds = $item.Current.BoundingRectangle
    [void][M7NativeInput]::SetForegroundWindow([IntPtr]$Root.Current.NativeWindowHandle)
    [void][M7NativeInput]::SetCursorPos([int]($itemBounds.X + $itemBounds.Width / 2), [int]($itemBounds.Y + $itemBounds.Height / 2))
    [M7NativeInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [M7NativeInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 350
}

function Save-WindowScreenshot {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$FileName)
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int][Math]::Ceiling($bounds.Width), [int][Math]::Ceiling($bounds.Height))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $screenshotsPath $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Move-MouseDrag {
    param([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY)
    [void][M7NativeInput]::SetCursorPos($FromX, $FromY); Start-Sleep -Milliseconds 120
    [M7NativeInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 12; $step++) {
        [void][M7NativeInput]::SetCursorPos([int]($FromX + ($ToX - $FromX) * $step / 12), [int]($FromY + ($ToY - $FromY) * $step / 12))
        Start-Sleep -Milliseconds 20
    }
    [M7NativeInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 500
}

function Start-Studio {
    $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
    $process = Start-Process -FilePath $dotnetPath -ArgumentList @($studioDll) -PassThru
    $window = Find-StudioWindow $process.Id
    [void][M7NativeInput]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
    [void](Find-NamedElement $window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    return @{ Process = $process; Window = $window }
}

function Close-Studio {
    param($Studio)
    if (-not $Studio.Process.HasExited) { $Studio.Process.Kill(); [void]$Studio.Process.WaitForExit(5000) }
}

function Open-Graph {
    param([System.Windows.Automation.AutomationElement]$Window)
    Invoke-NamedButton $Window "剧情图谱"
    [void](Find-NamedElement $Window "剧情图谱自由布局画布")
}

$first = Start-Studio
try {
    Open-Graph $first.Window
    foreach ($name in @(
        "剧情图谱节点 王城迷案 royal_mystery", "剧情图谱节点 王国线 kingdom_route", "剧情图谱节点 帝国线 empire_route", "剧情图谱节点 未分类 uncategorized",
        "搜索剧情图谱", "筛选剧情图谱", "剧情图谱自动布局", "剧情图谱缩小", "剧情图谱放大", "剧情图谱诊断列表",
        "Story 'uncategorized' 未连接到任何 EnterStory 转场。")) {
        [void](Find-NamedElement $first.Window $name)
    }

    Invoke-NamedButton $first.Window "剧情图谱自动布局"
    Invoke-NamedButton $first.Window "剧情图谱放大"
    Invoke-NamedButton $first.Window "剧情图谱缩小"
    Save-WindowScreenshot $first.Window "04-m7-derived-project-graph.png"

    Set-NamedValue $first.Window "搜索剧情图谱" "帝国"
    [void](Find-NamedElement $first.Window "剧情图谱节点 帝国线 empire_route")
    Assert-NamedElementAbsent $first.Window "剧情图谱节点 王城迷案 royal_mystery"
    Set-NamedValue $first.Window "搜索剧情图谱" ""
    Set-ComboSelection $first.Window "筛选剧情图谱" "孤立"
    [void](Find-NamedElement $first.Window "剧情图谱节点 未分类 uncategorized")
    Assert-NamedElementAbsent $first.Window "剧情图谱节点 王国线 kingdom_route"
    Set-ComboSelection $first.Window "筛选剧情图谱" "全部"

    $kingdom = Find-NamedElement $first.Window "剧情图谱节点 王国线 kingdom_route"
    $bounds = $kingdom.Current.BoundingRectangle
    Move-MouseDrag ([int]($bounds.X + 80)) ([int]($bounds.Y + 18)) ([int]($bounds.X + 30)) ([int]($bounds.Y + 54))
    if (-not (Test-Path -LiteralPath $layoutPath)) { throw "Project Graph layout file was not created." }
    $layout = Get-Content -LiteralPath $layoutPath -Raw | ConvertFrom-Json
    if ($null -eq $layout.nodes.kingdom_route) { throw "Dragged kingdom_route position was not persisted." }
    Save-WindowScreenshot $first.Window "05-m7-layout-search-diagnostics.png"

    Invoke-NamedButton $first.Window "kingdom_route 打开 Flow"
    [void](Find-NamedElement $first.Window "Story Flow 自由节点画布")
    [void](Find-NamedElement $first.Window "王国线")
    Save-WindowScreenshot $first.Window "06-m7-direct-open-story-flow.png"
}
finally { Close-Studio $first }

$storyHashesAfter = Get-ChildItem -LiteralPath $storiesPath -File -Filter "*.json" | Sort-Object Name | ForEach-Object { "$($_.Name):$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
if (($storyHashesBefore -join "|") -ne ($storyHashesAfter -join "|")) { throw "Project Graph interactions modified Runtime Story JSON." }

$restart = Start-Studio
try {
    Open-Graph $restart.Window
    [void](Find-NamedElement $restart.Window "剧情图谱节点 王国线 kingdom_route")
    $restoredLayout = Get-Content -LiteralPath $layoutPath -Raw | ConvertFrom-Json
    if ($null -eq $restoredLayout.nodes.kingdom_route) { throw "Project Graph layout was not restored after restart." }
    Save-WindowScreenshot $restart.Window "07-m7-restart-layout-persistence.png"
}
finally { Close-Studio $restart }

Write-Output "M7_UI_DERIVED_READ_ONLY_GRAPH=PASS"
Write-Output "M7_UI_SEARCH_FILTER_AUTO_LAYOUT=PASS"
Write-Output "M7_UI_LAYOUT_PERSISTENCE=PASS"
Write-Output "M7_UI_RUNTIME_STORY_JSON_UNCHANGED=PASS"
Write-Output "M7_UI_ISOLATED_DIAGNOSTICS=PASS"
Write-Output "M7_UI_DIRECT_OPEN_STORY_FLOW=PASS"
