param([string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Final21NativeInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$settingsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\settings.json"
$studioDll = Join-Path $RepositoryRoot "Studio\src\DarkGreyRPG.Studio\bin\Release\net10.0-windows\DarkGreyRPGStudio.dll"
$dotnetPath = "E:\Java\dotnet-sdk-10\dotnet.exe"
$projectPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance"
$actorsPath = Join-Path $projectPath "actors"
$storiesPath = Join-Path $projectPath "stories"
$screenshotsPath = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\screenshots"
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
New-Item -ItemType Directory -Force -Path $screenshotsPath | Out-Null

function Find-NamedElement {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutMilliseconds = 30000)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' was not found."
}

function Find-StudioWindow {
    param([int]$ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $windows = $desktop.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $windows.Count; $index++) {
            $window = $windows.Item($index)
            if ($window.Current.ProcessId -eq $ProcessId -and
                $window.Current.Name -eq "DarkGrey RPG Studio 2.1") {
                return $window
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio main window was not found."
}

function Find-NamedElementOfType {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$TimeoutMilliseconds = 30000)
    $condition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name),
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            $ControlType))
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' with type '$ControlType' was not found."
}

function Find-AutomationId {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId,
        [int]$TimeoutMilliseconds = 30000)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element with AutomationId '$AutomationId' was not found."
}

function Start-Studio {
    $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
    $process = Start-Process -FilePath $dotnetPath -ArgumentList @($studioDll) -PassThru
    $window = Find-StudioWindow $process.Id
    [void][Final21NativeInput]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
    [void](Find-NamedElement $window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    return @{ Process = $process; Window = $window }
}

function Close-Studio {
    param($Studio)
    if ($Studio.Process.HasExited) { return }
    $windowPattern = $Studio.Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $windowPattern.Close()
    if (-not $Studio.Process.WaitForExit(8000)) {
        $Studio.Process.Kill()
        throw "Studio did not close gracefully."
    }
}

function Select-ListItemContaining {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$ChildName)
    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 14 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 400
            return
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable list item contains '$ChildName'."
}

function Select-StoryPage {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$Page)
    $routes = Find-NamedElement $Window "剧情页面列表"
    Select-ListItemContaining $routes $Page
}

function Invoke-NamedButton {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $button = Find-NamedElement $Root $Name
    if (-not $button.Current.IsEnabled) { throw "Button '$Name' is disabled." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 450
}

function Set-NamedValue {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [string]$Value)
    $element = Find-NamedElement $Root $Name
    $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
    Start-Sleep -Milliseconds 350
}

function Get-NamedValue {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $element = Find-NamedElement $Root $Name
    return $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}

function Open-Story {
    param(
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Description)
    $list = Find-NamedElementOfType $Window "剧情列表" ([System.Windows.Automation.ControlType]::List)
    Select-ListItemContaining $list $Description
    Invoke-NamedButton $Window "进入剧情"
    [void](Find-NamedElement $Window "剧情页面列表")
}

function Return-Home {
    param([System.Windows.Automation.AutomationElement]$Window)
    Invoke-NamedButton $Window "返回项目首页"
    [void](Find-NamedElementOfType $Window "剧情列表" ([System.Windows.Automation.ControlType]::List))
}

function Open-Actor {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$ActorId)
    Select-StoryPage $Window "角色"
    $search = Find-NamedElement $Window "搜索剧情角色"
    $actorListRoot = $walker.GetParent($search)
    Select-ListItemContaining $actorListRoot $ActorId
    [void](Find-NamedElement $Window "Actor 显示名称")
}

function Save-WindowScreenshot {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$FileName)
    $bounds = $Window.Current.BoundingRectangle
    $width = [Math]::Max(1, [int][Math]::Ceiling($bounds.Width))
    $height = [Math]::Max(1, [int][Math]::Ceiling($bounds.Height))
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen(
            [int][Math]::Floor($bounds.X),
            [int][Math]::Floor($bounds.Y),
            0,
            0,
            [System.Drawing.Size]::new($width, $height))
        $bitmap.Save((Join-Path $screenshotsPath $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Click-Element {
    param([System.Windows.Automation.AutomationElement]$Element)
    $bounds = $Element.Current.BoundingRectangle
    [void][Final21NativeInput]::SetCursorPos(
        [int]($bounds.X + $bounds.Width / 2),
        [int]($bounds.Y + $bounds.Height / 2))
    [Final21NativeInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Final21NativeInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
}

function Drag-Mouse {
    param([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY)
    [void][Final21NativeInput]::SetCursorPos($FromX, $FromY)
    Start-Sleep -Milliseconds 100
    [Final21NativeInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 12; $step++) {
        [void][Final21NativeInput]::SetCursorPos(
            [int]($FromX + (($ToX - $FromX) * $step / 12)),
            [int]($FromY + (($ToY - $FromY) * $step / 12)))
        Start-Sleep -Milliseconds 20
    }
    [Final21NativeInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 600
}

function Assert-ElementInsideWindow {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$Name)
    $element = Find-NamedElement $Window $Name
    $windowBounds = $Window.Current.BoundingRectangle
    $bounds = $element.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0 -or
        $bounds.Left -lt $windowBounds.Left -or $bounds.Top -lt $windowBounds.Top -or
        $bounds.Right -gt ($windowBounds.Right + 1) -or $bounds.Bottom -gt ($windowBounds.Bottom + 1)) {
        throw "UI element '$Name' is clipped or outside the main window."
    }
}

$detectivePath = Join-Path $actorsPath "detective.json"
$empireDetectivePath = Join-Path $actorsPath "empire_detective.json"
$detectiveBefore = Get-Content -LiteralPath $detectivePath -Raw | ConvertFrom-Json
$empireBefore = Get-Content -LiteralPath $empireDetectivePath -Raw | ConvertFrom-Json
$kingdom = Get-Content -LiteralPath (Join-Path $storiesPath "kingdom_route.json") -Raw | ConvertFrom-Json
$empire = Get-Content -LiteralPath (Join-Path $storiesPath "empire_route.json") -Raw | ConvertFrom-Json
if ($kingdom.referenced_resources.actors -notcontains "detective") { throw "Kingdom Story does not reference detective." }
if ($kingdom.owned_resources.actors -contains "detective") { throw "Kingdom Story incorrectly owns detective." }
if ($empire.owned_resources.actors -notcontains "empire_detective") { throw "Empire Story does not own the imported Actor." }
if ($empire.referenced_resources.actors -contains "detective") { throw "Empire Story still references the source Actor." }

$first = Start-Studio
try {
    foreach ($name in @("搜索剧情", "剧情列表", "剧情图谱", "输出", "问题", "调试器", "Minecraft")) {
        Assert-ElementInsideWindow $first.Window $name
    }

    Open-Story $first.Window "最终验收主剧情。"
    Select-StoryPage $first.Window "概览"
    [void](Find-NamedElement $first.Window "资源归属")
    Select-StoryPage $first.Window "对话"
    [void](Find-NamedElement $first.Window "剧情 Dialogue 列表")
    Select-StoryPage $first.Window "任务"
    [void](Find-NamedElement $first.Window "剧情 Quest 列表")
    Select-StoryPage $first.Window "流程"
    [void](Find-NamedElement $first.Window "Story Flow 自由节点画布")
    Return-Home $first.Window

    Open-Story $first.Window "交出证物后的剧情。"
    Open-Actor $first.Window "detective"
    if ((Get-NamedValue $first.Window "Actor 显示名称") -ne $detectiveBefore.display_name) {
        throw "Kingdom reference did not open the shared detective resource."
    }
    Set-NamedValue $first.Window "Actor 显示名称" "侦探（共享验收）"
    Invoke-NamedButton $first.Window "保存 Actor"
    Save-WindowScreenshot $first.Window "08-final-shared-reference-edit.png"
    Return-Home $first.Window

    Open-Story $first.Window "最终验收主剧情。"
    Open-Actor $first.Window "detective"
    if ((Get-NamedValue $first.Window "Actor 显示名称") -ne "侦探（共享验收）") {
        throw "The Royal Mystery Story did not observe the Kingdom shared-resource edit."
    }
    Return-Home $first.Window

    Open-Story $first.Window "隐瞒证物后的剧情。"
    Open-Actor $first.Window "empire_detective"
    Set-NamedValue $first.Window "Actor 显示名称" "帝国线侦探（独立验收）"
    Invoke-NamedButton $first.Window "保存 Actor"
    Save-WindowScreenshot $first.Window "09-final-independent-import.png"
    Return-Home $first.Window

    Open-Story $first.Window "最终验收主剧情。"
    Open-Actor $first.Window "detective"
    if ((Get-NamedValue $first.Window "Actor 显示名称") -ne "侦探（共享验收）") {
        throw "Editing the imported Empire Actor changed the original detective."
    }
    Set-NamedValue $first.Window "Actor 显示名称" $detectiveBefore.display_name
    Invoke-NamedButton $first.Window "保存 Actor"
    Return-Home $first.Window

    Open-Story $first.Window "隐瞒证物后的剧情。"
    Open-Actor $first.Window "empire_detective"
    Set-NamedValue $first.Window "Actor 显示名称" $empireBefore.display_name
    Invoke-NamedButton $first.Window "保存 Actor"
    Return-Home $first.Window

    $outputTab = Find-NamedElement $first.Window "输出"
    Click-Element $outputTab
    $splitter = Find-AutomationId $first.Window "BottomPanelSplitter"
    $splitterBounds = $splitter.Current.BoundingRectangle
    $splitterX = [int]($splitterBounds.X + $splitterBounds.Width * 0.62)
    $splitterY = [int]($splitterBounds.Y + $splitterBounds.Height / 2)
    Drag-Mouse $splitterX $splitterY $splitterX ($splitterY - 100)
    Click-Element $outputTab
    Save-WindowScreenshot $first.Window "10-final-bottom-dock-collapsed.png"
    Click-Element $outputTab
    Save-WindowScreenshot $first.Window "11-final-bottom-dock-expanded.png"
}
finally {
    Close-Studio $first
}

$savedSettings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if ($savedSettings.bottom_panel_height -lt 190 -or $savedSettings.bottom_panel_height -gt 260) {
    throw "Bottom Dock drag height was not persisted: $($savedSettings.bottom_panel_height)."
}

$restart = Start-Studio
try {
    $outputTab = Find-NamedElement $restart.Window "输出"
    Click-Element $outputTab
    Save-WindowScreenshot $restart.Window "12-final-restart-restored-project-and-dock.png"
    [void](Find-NamedElement $restart.Window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    [void](Find-NamedElementOfType $restart.Window "剧情列表" ([System.Windows.Automation.ControlType]::List))
}
finally {
    Close-Studio $restart
}

$detectiveAfter = Get-Content -LiteralPath $detectivePath -Raw | ConvertFrom-Json
$empireAfter = Get-Content -LiteralPath $empireDetectivePath -Raw | ConvertFrom-Json
if ($detectiveAfter.display_name -ne $detectiveBefore.display_name) { throw "Shared Actor display name was not restored." }
if ($empireAfter.display_name -ne $empireBefore.display_name) { throw "Independent Actor display name was not restored." }
if ($detectiveAfter.id -eq $empireAfter.id) { throw "Imported Actor does not have an independent resource ID." }

Write-Output "FINAL_UI_AUTO_RESTORE_AND_FILE_MENU=PASS"
Write-Output "FINAL_UI_FIVE_STORY_PAGES=PASS"
Write-Output "FINAL_UI_SHARED_REFERENCE_SYNCHRONIZATION=PASS"
Write-Output "FINAL_UI_IMPORT_AS_NEW_INDEPENDENCE=PASS"
Write-Output "FINAL_UI_BOTTOM_DOCK_RESIZE_COLLAPSE_RESTORE=PASS"
Write-Output "FINAL_UI_AUTOMATION_ACCESSIBILITY_AND_CLIPPING=PASS"
