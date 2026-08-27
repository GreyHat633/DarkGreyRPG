param(
    [string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG",
    [switch]$GraphOnly)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Studio212ConnectionInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$dotnet = "E:\Java\dotnet-sdk-10\dotnet.exe"
$studioDll = Join-Path $RepositoryRoot "studio\src\DarkGreyRPG.Studio\bin\Release\net10.0-windows\DarkGreyRPGStudio.dll"
$sourceSettings = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\settings.json"
$sourceProject = Join-Path $RepositoryRoot ".tooling\2.1-acceptance\DarkGrey-2.1-Acceptance"
$sourceStories = Join-Path $sourceProject "stories"
$toolingRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".tooling"))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $toolingRoot "2.1.2-connection-ui-work"))
$screenshots = Join-Path $toolingRoot "2.1.2-acceptance\screenshots"
if (-not $qaRoot.StartsWith($toolingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe QA working directory: $qaRoot"
}
if (Test-Path -LiteralPath $qaRoot) { Remove-Item -LiteralPath $qaRoot -Recurse -Force }
New-Item -ItemType Directory -Path $qaRoot | Out-Null
New-Item -ItemType Directory -Force -Path $screenshots | Out-Null
$qaProject = Join-Path $qaRoot "DarkGrey-2.1-Acceptance"
Copy-Item -LiteralPath $sourceProject -Destination $qaProject -Recurse
$qaRecoveryDirectory = Join-Path $qaProject "resources\editor\recovery"
if (Test-Path -LiteralPath $qaRecoveryDirectory) {
    Remove-Item -LiteralPath $qaRecoveryDirectory -Recurse -Force
}
$stories = Join-Path $qaProject "stories"
$storyFile = Join-Path $stories "royal_mystery.json"
$recoveryFile = Join-Path $qaProject "resources\editor\recovery\royal_mystery.json"
$deleteStoryFile = Join-Path $stories "qa_delete_story.json"
$deleteDialogueFile = Join-Path $qaProject "dialogues\qa_delete_dialogue.json"
$deleteQuestFile = Join-Path $qaProject "quests\qa_delete_quest.json"
$deleteStoryModel = Get-Content -LiteralPath (Join-Path $stories "uncategorized.json") -Raw | ConvertFrom-Json
$deleteStoryModel.id = "qa_delete_story"
$deleteStoryModel.display_name = "待删除验收剧情"
$deleteStoryModel.title = "待删除验收剧情"
$deleteStoryModel.description = "只应在左侧导航列表出现；右侧显示移出的完整剧情概览。"
$deleteStoryModel.flow_ref = "qa_delete_story"
$deleteStoryModel | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $deleteStoryFile -Encoding utf8
$deleteDialogueModel = Get-Content -LiteralPath (Join-Path $qaProject "dialogues\final_confrontation.json") -Raw | ConvertFrom-Json
$deleteDialogueModel.id = "qa_delete_dialogue"
$deleteDialogueModel.title = "随剧情删除的对话"
$deleteDialogueModel.display_name = "随剧情删除的对话"
$deleteDialogueModel.home_story_id = "qa_delete_story"
$deleteDialogueModel | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $deleteDialogueFile -Encoding utf8
$deleteQuestModel = Get-Content -LiteralPath (Join-Path $qaProject "quests\evidence.json") -Raw | ConvertFrom-Json
$deleteQuestModel.id = "qa_delete_quest"
$deleteQuestModel.title = "随剧情删除的任务"
$deleteQuestModel.display_name = "随剧情删除的任务"
$deleteQuestModel.home_story_id = "qa_delete_story"
$deleteQuestModel | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $deleteQuestFile -Encoding utf8
$storyModel = Get-Content -LiteralPath $storyFile -Raw | ConvertFrom-Json
$storyModel.nodes += [pscustomobject]@{
    id = "sequence_stage_f"
    type = "sequence"
    position = [pscustomobject]@{ x = 720; y = 620 }
    properties = [pscustomobject]@{ step_count = "1" }
}
$storyModel.nodes += [pscustomobject]@{
    id = "has_item_stage_f"
    type = "has_item"
    position = [pscustomobject]@{ x = 980; y = 620 }
    properties = [pscustomobject]@{ item = "minecraft:diamond"; metadata = "7"; amount = "3"; unknown_compat = "kept" }
}
$storyModel | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $storyFile -Encoding utf8
$settings = Join-Path $qaRoot "settings.json"
$settingsModel = Get-Content -LiteralPath $sourceSettings -Raw | ConvertFrom-Json
$settingsModel.last_project = $qaProject
$settingsModel | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settings -Encoding utf8
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

function Find-NamedElement {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [int]$TimeoutMilliseconds = 8000)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($matches.Count -gt 0) {
            $element = $matches | Where-Object {
                $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
            } | Sort-Object @(
                @{ Expression = { $_.Current.IsEnabled }; Descending = $true },
                @{ Expression = { $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height }; Descending = $true }
            ) | Select-Object -First 1
            if ($null -ne $element) { return $element }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element '$Name' was not found."
}

function Find-NamedElementContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Fragment, [int]$TimeoutMilliseconds = 8000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $element = $matches | Where-Object {
            $_.Current.Name -like "*$Fragment*" -and
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
        } | Sort-Object {
            $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height
        } -Descending | Select-Object -First 1
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element containing '$Fragment' was not found."
}

function Find-MenuItem {
    param([string]$Name, [int]$TimeoutMilliseconds = 8000)
    $condition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem))
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $item = $matches | Where-Object {
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
        } | Select-Object -First 1
        if ($null -ne $item) { return $item }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Menu item '$Name' was not found."
}

function Find-MenuItemContaining {
    param([string]$Fragment, [int]$TimeoutMilliseconds = 8000)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuItem)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $item = $matches | Where-Object {
            $_.Current.Name -like "*$Fragment*" -and
            $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Height -gt 0
        } | Select-Object -First 1
        if ($null -ne $item) { return $item }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Menu item containing '$Fragment' was not found."
}

function Assert-NamedElementAbsent {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    if ($null -ne $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        throw "UI element '$Name' should be absent."
    }
}

function Find-ExpandableElementContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Fragment, [int]$TimeoutMilliseconds = 8000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($element in $matches) {
            if ($element.Current.Name -notlike "*$Fragment*" -or $element.Current.BoundingRectangle.Width -le 0) { continue }
            $pattern = $null
            if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
                return $element
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Expandable UI element containing '$Fragment' was not found."
}

function Assert-ConnectionState {
    param(
        [string]$StoryFile,
        [string[]]$Present = @(),
        [string[]]$Absent = @())
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    do {
        $resource = Get-Content -LiteralPath $StoryFile -Raw | ConvertFrom-Json
        $connections = @($resource.connections | ForEach-Object { "$($_.from).$($_.output)->$($_.to)" })
        $valid = $true
        foreach ($token in $Present) { if ($token -notin $connections) { $valid = $false } }
        foreach ($token in $Absent) { if ($token -in $connections) { $valid = $false } }
        if ($valid) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Saved Story did not reach the expected state. Current connections: $($connections -join ' | ')"
}

function Assert-NodePropertyState {
    param([string]$StoryFile, [string]$NodeId, [string]$Property, [string]$Expected)
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    do {
        $resource = Get-Content -LiteralPath $StoryFile -Raw | ConvertFrom-Json
        $node = $resource.nodes | Where-Object id -eq $NodeId | Select-Object -First 1
        if ($null -ne $node -and [string]$node.properties.$Property -eq $Expected) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Saved Story node '$NodeId' property '$Property' did not become '$Expected'."
}

function Wait-FileState {
    param([string]$Path, [bool]$Exists, [int]$TimeoutMilliseconds = 5000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        if ((Test-Path -LiteralPath $Path) -eq $Exists) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "File state did not become Exists=$Exists for '$Path'."
}

function Find-AutomationId {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$AutomationId, [int]$TimeoutMilliseconds = 8000)
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($matches.Count -gt 0) {
            return $matches | Sort-Object {
                $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height
            } -Descending | Select-Object -First 1
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "UI element with AutomationId '$AutomationId' was not found."
}

function Find-StudioWindow {
    param([int]$ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $windows.Count; $index++) {
            $candidate = $windows.Item($index)
            if ($candidate.Current.ProcessId -eq $ProcessId -and $candidate.Current.Name -like "DarkGrey RPG Studio*") { return $candidate }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio main window was not found."
}

function Invoke-NamedButton {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    (Find-NamedElement $Root $Name).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 450
}

function Save-StoryFlow {
    param([System.Windows.Automation.AutomationElement]$Root)
    $button = Find-NamedElement $Root "保存 Story Flow"
    if ($button.Current.IsEnabled) {
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 500
    }
    $fit = Find-NamedElement $Root "适应全部流程节点"
    $fit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
}

function Select-ListItemContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName)
    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 12 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 400
            return
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable list item contains '$ChildName'."
}

function Find-ListItemContaining {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName)
    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 12 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { return $element }
        $element = $walker.GetParent($element)
    }
    throw "No list item contains '$ChildName'."
}

function Open-StoryDeleteMenu {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$ChildName)
    $item = Find-ListItemContaining $Root $ChildName
    $itemBounds = $item.Current.BoundingRectangle
    Mouse-Click ([System.Drawing.Point]::new(
        [int]($itemBounds.X + [Math]::Min(40, $itemBounds.Width / 2)),
        [int]($itemBounds.Y + $itemBounds.Height / 2))) Right
    Start-Sleep -Milliseconds 250
    return Find-MenuItem "删除剧情"
}

function Mouse-Drag {
    param([System.Drawing.Point]$From, [System.Drawing.Point]$To)
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 150
    [void][Studio212ConnectionInput]::SetCursorPos($From.X, $From.Y)
    Start-Sleep -Milliseconds 100
    [Studio212ConnectionInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 14; $step++) {
        [void][Studio212ConnectionInput]::SetCursorPos(
            [int]($From.X + ($To.X - $From.X) * $step / 14),
            [int]($From.Y + ($To.Y - $From.Y) * $step / 14))
        Start-Sleep -Milliseconds 20
    }
    [Studio212ConnectionInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
}

function Mouse-Click {
    param([System.Drawing.Point]$Point, [ValidateSet('Left','Right')]$Button = 'Left')
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    [void][Studio212ConnectionInput]::SetCursorPos($Point.X, $Point.Y)
    $down = if ($Button -eq 'Left') { 0x0002 } else { 0x0008 }
    $up = if ($Button -eq 'Left') { 0x0004 } else { 0x0010 }
    [Studio212ConnectionInput]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
    [Studio212ConnectionInput]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Mouse-DoubleClick {
    param([System.Drawing.Point]$Point)
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    [void][Studio212ConnectionInput]::SetCursorPos($Point.X, $Point.Y)
    foreach ($click in 1..2) {
        [Studio212ConnectionInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        [Studio212ConnectionInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
    }
    Start-Sleep -Milliseconds 650
}

function Get-RuntimeIdToken {
    param([System.Windows.Automation.AutomationElement]$Element)
    return (($Element.GetRuntimeId() | ForEach-Object { [string]$_ }) -join '.')
}

function Capture-DragAndCancel {
    param([System.Drawing.Point]$From, [System.Drawing.Point]$To, [string]$ScreenshotName)
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    [void][Studio212ConnectionInput]::SetCursorPos($From.X, $From.Y)
    Start-Sleep -Milliseconds 150
    [Studio212ConnectionInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    foreach ($step in 1..10) {
        [void][Studio212ConnectionInput]::SetCursorPos(
            [int]($From.X + ($To.X - $From.X) * $step / 10),
            [int]($From.Y + ($To.Y - $From.Y) * $step / 10))
        Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Milliseconds 250
    Save-Screenshot $window $ScreenshotName
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Start-Sleep -Milliseconds 150
    [Studio212ConnectionInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}

function Set-TextAndBlur {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name, [string]$Value)
    $textBox = Find-NamedElement $Root $Name
    $textBox.SetFocus()
    $textBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
    Start-Sleep -Milliseconds 150
    $clickable = (Find-NamedElement $window "适应全部流程节点").GetClickablePoint()
    Mouse-Click ([System.Drawing.Point]::new([int]$clickable.X, [int]$clickable.Y))
}

function Invoke-DialogButtonContaining {
    param([string]$Fragment, [int]$TimeoutMilliseconds = 8000)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $matches = $desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $button = $matches | Where-Object {
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -like "*$Fragment*" -and $_.Current.IsEnabled
        } | Select-Object -First 1
        if ($null -ne $button) {
            $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 450
            return
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Dialog button containing '$Fragment' was not found."
}

function Cancel-DragWithEscape {
    param([System.Drawing.Point]$From, [System.Drawing.Point]$To)
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 150
    [void][Studio212ConnectionInput]::SetCursorPos($From.X, $From.Y)
    [Studio212ConnectionInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 8; $step++) {
        [void][Studio212ConnectionInput]::SetCursorPos(
            [int]($From.X + ($To.X - $From.X) * $step / 8),
            [int]($From.Y + ($To.Y - $From.Y) * $step / 8))
        Start-Sleep -Milliseconds 25
    }
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Start-Sleep -Milliseconds 150
    [Studio212ConnectionInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}

function Undo-FlowEdit {
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    $focusBounds = (Find-NamedElement $window $startNodeName).Current.BoundingRectangle
    $focusPoint = [System.Drawing.Point]::new(
        [int]($focusBounds.X + $focusBounds.Width / 2),
        [int]($focusBounds.Y + 14))
    Mouse-Click $focusPoint
    Start-Sleep -Milliseconds 150
    [System.Windows.Forms.SendKeys]::SendWait("^z")
    Start-Sleep -Milliseconds 550
}

function Find-BlankFlowCanvasPoint {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [System.Windows.Automation.AutomationElement]$Canvas)
    $bounds = $Canvas.Current.BoundingRectangle
    $nodes = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.Name -like "Flow 节点 *" -and $_.Current.BoundingRectangle.Width -gt 0 }
    foreach ($yRatio in @(0.9, 0.75, 0.25, 0.55)) {
        foreach ($xRatio in @(0.9, 0.1, 0.7, 0.3, 0.5)) {
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
    throw "No blank Flow canvas point was available for Undo focus."
}

function Port-Center {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$Name)
    $point = (Find-NamedElement $Root $Name).GetClickablePoint()
    return [System.Drawing.Point]::new([int]$point.X, [int]$point.Y)
}

function Relative-NodePoint {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$NodeName,
        [double]$XRatio,
        [double]$YRatio)
    $bounds = (Find-NamedElement $Root $NodeName).Current.BoundingRectangle
    return [System.Drawing.Point]::new([int]($bounds.X + $bounds.Width * $XRatio), [int]($bounds.Y + $bounds.Height * $YRatio))
}

function Expand-NodeParameters {
    param([System.Windows.Automation.AutomationElement]$Root, [string]$NodeId)
    $expander = Find-NamedElementContaining $Root "$NodeId 参数："
    $pattern = $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($pattern.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
        $pattern.Expand()
        Start-Sleep -Milliseconds 300
    }
}

function Save-Screenshot {
    param([System.Windows.Automation.AutomationElement]$Window, [string]$Name)
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int][Math]::Ceiling($bounds.Width), [int][Math]::Ceiling($bounds.Height))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $screenshots $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$hashesBefore = Get-ChildItem -LiteralPath $sourceStories -Filter '*.json' -File | Sort-Object Name | ForEach-Object {
    "$($_.Name):$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)"
}
$env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settings
$process = Start-Process -FilePath $dotnet -ArgumentList @($studioDll) -PassThru
try {
    $window = Find-StudioWindow $process.Id
    $handle = [IntPtr]$window.Current.NativeWindowHandle
    $windowDpi = [Studio212ConnectionInput]::GetDpiForWindow($handle)
    [void][Studio212ConnectionInput]::MoveWindow($handle, 40, 30, 1700, 980, $true)
    [void][Studio212ConnectionInput]::SetForegroundWindow($handle)
    [void](Find-NamedElement $window "DarkGrey 2.1 Acceptance (darkgrey_2_1_acceptance)")
    if (-not $GraphOnly) {
    $fileMenu = Find-MenuItem "文件(F)"
    $fileMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $recentProjectsMenu = Find-MenuItemContaining "最近项目"
    $projectFolderItem = Find-MenuItem "项目文件夹"
    if (-not $projectFolderItem.Current.IsEnabled) { throw "Project folder menu item should be enabled for an open project." }
    $recentProjectsMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    [void](Find-MenuItem "打开最近项目 DarkGrey-2.1-Acceptance")
    Save-Screenshot $window "00-file-recent-projects-menu-dpi-$windowDpi.png"
    Write-Output "FILE_RECENT_PROJECTS_MENU=PASS"
    Write-Output "FILE_PROJECT_FOLDER_LABEL=PASS"
    $recentProjectsMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    $fileMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()

    $storyNavigation = Find-NamedElement $window "剧情导航列表"
    [void](Find-NamedElement $window "剧情概览")
    Select-ListItemContaining $storyNavigation "qa_delete_story"
    [void](Find-NamedElement $window "选中剧情完整概览")
    [void](Find-NamedElement $window "只应在左侧导航列表出现；右侧显示移出的完整剧情概览。")
    [void](Find-NamedElement $window "0 个本剧情角色 · 0 个引用角色 · 1 个对话 · 1 个任务")
    [void](Find-NamedElement $window "1 个节点")
    Save-Screenshot $window "00-project-home-single-navigation-dpi-$windowDpi.png"
    Write-Output "PROJECT_HOME_FULL_STORY_OVERVIEW=PASS"

    $deleteItem = Open-StoryDeleteMenu $storyNavigation "qa_delete_story"
    $openStoryItem = Find-MenuItem "打开剧情"
    if ($openStoryItem.Current.BoundingRectangle.Height -lt 32 -or $deleteItem.Current.BoundingRectangle.Height -lt 32) {
        throw "Story context menu did not use the expected Fluent item height."
    }
    Save-Screenshot $window "00b-story-fluent-context-menu-dpi-$windowDpi.png"
    Write-Output "STORY_FLUENT_CONTEXT_MENU_VISUAL=CAPTURED"
    $deleteItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    [void](Find-NamedElementContaining $desktop "对话：qa_delete_dialogue")
    [void](Find-NamedElementContaining $desktop "任务：qa_delete_quest")
    Invoke-DialogButtonContaining "否"
    if (-not (Test-Path -LiteralPath $deleteStoryFile)) { throw "Story was deleted after the user rejected confirmation." }
    if (-not (Test-Path -LiteralPath $deleteDialogueFile) -or -not (Test-Path -LiteralPath $deleteQuestFile)) {
        throw "Owned resources were deleted after the user rejected confirmation."
    }
    Write-Output "STORY_DELETE_CANCEL_PRESERVES_FILE=PASS"

    $deleteItem = Open-StoryDeleteMenu $storyNavigation "qa_delete_story"
    $deleteItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Invoke-DialogButtonContaining "是"
    if (Test-Path -LiteralPath $deleteStoryFile) { throw "Confirmed Story deletion did not remove its JSON file." }
    if ((Test-Path -LiteralPath $deleteDialogueFile) -or (Test-Path -LiteralPath $deleteQuestFile)) {
        throw "Confirmed Story deletion did not cascade to its owned resources."
    }
    try {
        [void](Find-NamedElement $storyNavigation "待删除验收剧情" 700)
        throw "Deleted Story remained visible in project navigation."
    }
    catch {
        if ($_.Exception.Message -notlike "UI element '待删除验收剧情' was not found.*") { throw }
    }
    Write-Output "STORY_DELETE_CONFIRM_REMOVES_FILE_AND_LIST_ITEM=PASS"
    Write-Output "STORY_DELETE_CASCADE_RESOURCES=PASS"

    Select-ListItemContaining $storyNavigation "royal_mystery"
    Invoke-NamedButton $window "进入选中剧情"
    $storyPages = Find-NamedElement $window "剧情页面列表"
    Assert-NamedElementAbsent $storyPages "概览"
    Select-ListItemContaining $storyPages "流程"
    Write-Output "STORY_WORKSPACE_OVERVIEW_ROUTE_REMOVED=PASS"
    [void](Find-NamedElement $window "节点内资源选择器")
    [void](Find-NamedElement $window "start 输出端口 next")
    Invoke-NamedButton $window "适应全部流程节点"
    $parameterExpander = Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）"
    $expandPattern = $parameterExpander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($expandPattern.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Expanded) {
        $expandPattern.Collapse()
        Start-Sleep -Milliseconds 350
    }
    [void](Find-NamedElement $window "wait_evidence 输出端口 next")
    [void](Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）")
    Write-Output "FLOW_PARAMETER_EXPANDER_DISCOVERABLE=PASS"

    # Capture stable relative port positions before any edit rebuilds the ItemsControls.
    $startNodeName = "Flow 节点 剧情开始 start"
    $startEvidenceNodeName = "Flow 节点 开始任务 start_evidence"
    $startBounds = (Find-NamedElement $window $startNodeName).Current.BoundingRectangle
    $startEvidenceBounds = (Find-NamedElement $window $startEvidenceNodeName).Current.BoundingRectangle
    $startOutputPoint = Port-Center $window "start 输出端口 next"
    $startEvidenceInputPoint = Port-Center $window "start_evidence 输入端口"
    $startOutputRatio = @((($startOutputPoint.X - $startBounds.X) / $startBounds.Width), (($startOutputPoint.Y - $startBounds.Y) / $startBounds.Height))
    $startEvidenceInputRatio = @((($startEvidenceInputPoint.X - $startEvidenceBounds.X) / $startEvidenceBounds.Width), (($startEvidenceInputPoint.Y - $startEvidenceBounds.Y) / $startEvidenceBounds.Height))
    $exitNodeName = "Flow 节点 按 Dialogue Exit 分支 exit_branch"
    $waitNodeName = "Flow 节点 等待任务完成 wait_evidence"
    $kingdomNodeName = "Flow 节点 进入剧情 enter_kingdom"
    $empireNodeName = "Flow 节点 进入剧情 enter_empire"
    $exitBounds = (Find-NamedElement $window $exitNodeName).Current.BoundingRectangle
    $waitBounds = (Find-NamedElement $window $waitNodeName).Current.BoundingRectangle
    $kingdomBounds = (Find-NamedElement $window $kingdomNodeName).Current.BoundingRectangle
    $empireBounds = (Find-NamedElement $window $empireNodeName).Current.BoundingRectangle
    $handOverPoint = Port-Center $window "exit_branch 输出端口 hand_over"
    $concealPoint = Port-Center $window "exit_branch 输出端口 conceal"
    $waitInputPoint = Port-Center $window "wait_evidence 输入端口"
    $kingdomInputPoint = Port-Center $window "enter_kingdom 输入端口"
    $empireInputPoint = Port-Center $window "enter_empire 输入端口"
    $handOverRatio = @((($handOverPoint.X - $exitBounds.X) / $exitBounds.Width), (($handOverPoint.Y - $exitBounds.Y) / $exitBounds.Height))
    $concealRatio = @((($concealPoint.X - $exitBounds.X) / $exitBounds.Width), (($concealPoint.Y - $exitBounds.Y) / $exitBounds.Height))
    $waitInputRatio = @((($waitInputPoint.X - $waitBounds.X) / $waitBounds.Width), (($waitInputPoint.Y - $waitBounds.Y) / $waitBounds.Height))
    $kingdomInputRatio = @((($kingdomInputPoint.X - $kingdomBounds.X) / $kingdomBounds.Width), (($kingdomInputPoint.Y - $kingdomBounds.Y) / $kingdomBounds.Height))
    $empireInputRatio = @((($empireInputPoint.X - $empireBounds.X) / $empireBounds.Width), (($empireInputPoint.Y - $empireBounds.Y) / $empireBounds.Height))
    $canvas = Find-AutomationId $window "StoryFlowCanvasViewport"
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    $canvasBounds = $canvas.Current.BoundingRectangle
    $blank = [System.Drawing.Point]::new([int]($canvasBounds.Right - 55), [int]($canvasBounds.Bottom - 55))

    # Node selection/movement/Undo must retain every unaffected node-control instance.
    $stableParameter = Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）"
    $stableParameterRuntimeId = Get-RuntimeIdToken $stableParameter
    $startClickBounds = (Find-NamedElement $window $startNodeName).Current.BoundingRectangle
    $startHeaderPoint = [System.Drawing.Point]::new([int]($startClickBounds.X + $startClickBounds.Width / 2), [int]($startClickBounds.Y + 14))
    Mouse-Click $startHeaderPoint
    if ((Get-RuntimeIdToken (Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）")) -ne $stableParameterRuntimeId) {
        throw "A node click recreated an unaffected parameter editor."
    }
    Mouse-Drag $startHeaderPoint ([System.Drawing.Point]::new($startHeaderPoint.X + 28, $startHeaderPoint.Y + 18))
    if ((Get-RuntimeIdToken (Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）")) -ne $stableParameterRuntimeId) {
        throw "Node movement recreated an unaffected parameter editor."
    }
    Undo-FlowEdit
    if ((Get-RuntimeIdToken (Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）")) -ne $stableParameterRuntimeId) {
        throw "Undoing node movement recreated an unaffected parameter editor."
    }
    Write-Output "FLOW_NODE_CONTROL_IDENTITY_STABLE=PASS"

    # Reconnecting an occupied endpoint keeps the opposite endpoint fixed and visible from mouse-down.
    $outputReconnectStart = Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1]
    Capture-DragAndCancel $outputReconnectStart ([System.Drawing.Point]::new($outputReconnectStart.X - 120, $outputReconnectStart.Y + 65)) "04-connected-output-endpoint-drag-dpi-$windowDpi.png"
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    $inputReconnectStart = Relative-NodePoint $window $kingdomNodeName $kingdomInputRatio[0] $kingdomInputRatio[1]
    Capture-DragAndCancel $inputReconnectStart ([System.Drawing.Point]::new($inputReconnectStart.X + 115, $inputReconnectStart.Y + 70)) "05-connected-input-endpoint-drag-dpi-$windowDpi.png"
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    if ((Get-RuntimeIdToken (Find-ExpandableElementContaining $window "wait_evidence 参数：参数（1）")) -ne $stableParameterRuntimeId) {
        throw "Reconnect preview recreated an unaffected parameter editor."
    }
    Write-Output "FLOW_EXISTING_ENDPOINT_DRAG_VISUAL=PASS"

    $hasItemNode = Find-NamedElement $window "Flow 节点 是否持有物品 has_item_stage_f"
    [void](Find-NamedElement $hasItemNode "Flow 核心参数 item")
    [void](Find-NamedElement $hasItemNode "Flow 核心参数 amount")
    $advancedExpander = Find-NamedElement $hasItemNode "Flow 高级与兼容参数"
    $advancedExpander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 250
    [void](Find-NamedElement $hasItemNode "Flow 高级参数 metadata")
    [void](Find-NamedElement $hasItemNode "Flow 高级参数 unknown_compat")
    Set-TextAndBlur $hasItemNode "Flow 核心参数 amount" "5"
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-NodePropertyState $storyFile "has_item_stage_f" "amount" "3"
    Set-TextAndBlur (Find-NamedElement $window "Flow 节点 是否持有物品 has_item_stage_f") "Flow 核心参数 amount" "5"
    Save-StoryFlow $window
    Assert-NodePropertyState $storyFile "has_item_stage_f" "amount" "5"
    Set-TextAndBlur (Find-NamedElement $window "Flow 节点 是否持有物品 has_item_stage_f") "Flow 核心参数 amount" "3"
    Save-StoryFlow $window
    Assert-NodePropertyState $storyFile "has_item_stage_f" "amount" "3"
    Save-Screenshot $window "01-flow-property-editors-dpi-$windowDpi.png"
    Write-Output "FLOW_CORE_ADVANCED_PROPERTY_EDITORS_UNDO=PASS"

    # Invalid editor state writes Recovery only; double-clicking its Problem centers the node and focuses the exact field.
    Set-TextAndBlur $window "Flow 核心参数 item" ""
    Wait-FileState $recoveryFile $true
    Invoke-NamedButton $window "打开当前 Flow 错误"
    $problem = Find-NamedElementContaining $window "Story node 'has_item_stage_f' requires a non-empty resource reference."
    $problemPoint = $problem.GetClickablePoint()
    Mouse-DoubleClick ([System.Drawing.Point]::new([int]$problemPoint.X, [int]$problemPoint.Y))
    $focusedField = Find-NamedElement (Find-NamedElement $window "Flow 节点 是否持有物品 has_item_stage_f") "Flow 核心参数 item"
    if (-not $focusedField.Current.HasKeyboardFocus) {
        $actualFocus = [System.Windows.Automation.AutomationElement]::FocusedElement
        throw "Problem navigation did not focus the mapped item field. Actual focus: '$($actualFocus.Current.Name)' ($($actualFocus.Current.ControlType.ProgrammaticName))."
    }
    Set-TextAndBlur $window "Flow 核心参数 item" "minecraft:diamond"
    Save-StoryFlow $window
    Wait-FileState $recoveryFile $false
    $viewMenu = Find-NamedElementContaining $window "视图"
    $viewMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 250
    (Find-NamedElement $desktop "底部面板").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    Invoke-NamedButton $window "适应全部流程节点"
    $canvas = Find-AutomationId $window "StoryFlowCanvasViewport"
    $blank = Find-BlankFlowCanvasPoint $window $canvas
    Write-Output "FLOW_PROBLEM_NODE_FIELD_FOCUS_RECOVERY=PASS"

    # Adjacent node edges must not make the start output hit the neighboring input.
    Assert-ConnectionState $storyFile -Present @("start.next->start_evidence", "start_evidence.next->wait_evidence")
    Mouse-Drag (Relative-NodePoint $window $startNodeName $startOutputRatio[0] $startOutputRatio[1]) $blank
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("start_evidence.next->wait_evidence") -Absent @("start.next->start_evidence")
    Mouse-Drag (Relative-NodePoint $window $startEvidenceNodeName $startEvidenceInputRatio[0] $startEvidenceInputRatio[1]) (Relative-NodePoint $window $startNodeName $startOutputRatio[0] $startOutputRatio[1])
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("start.next->start_evidence", "start_evidence.next->wait_evidence")
    Write-Output "FLOW_ADJACENT_PORT_DISAMBIGUATION=PASS"

    # A connected Dialogue Exit rename is explicit and migrates the target in one transaction.
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "tribute, conceal"
    Invoke-DialogButtonContaining "是"
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom") -Absent @("exit_branch.tribute->enter_kingdom")
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "tribute, conceal"
    Invoke-DialogButtonContaining "是"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.tribute->enter_kingdom") -Absent @("exit_branch.hand_over->enter_kingdom")
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "hand_over, conceal"
    Invoke-DialogButtonContaining "是"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom") -Absent @("exit_branch.tribute->enter_kingdom")
    Write-Output "FLOW_DIALOGUE_EXIT_RENAME_MIGRATION_UNDO=PASS"

    # Cancellation preserves the old port/connection; confirmation deletes both and Undo restores both.
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "conceal"
    Invoke-DialogButtonContaining "取消"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "conceal"
    Invoke-DialogButtonContaining "确定"
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "conceal"
    Invoke-DialogButtonContaining "确定"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Absent @("exit_branch.hand_over->enter_kingdom")
    Set-TextAndBlur $window "节点内 Dialogue Exit 输出" "hand_over, conceal"
    Save-StoryFlow $window
    Mouse-Drag (Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1]) (Relative-NodePoint $window $kingdomNodeName $kingdomInputRatio[0] $kingdomInputRatio[1])
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Write-Output "FLOW_DIALOGUE_EXIT_DELETE_CONFIRM_UNDO=PASS"

    # Sequence buttons keep numeric ports visible and protect a connected last step.
    $sequenceExpander = Find-NamedElementContaining $window "sequence_stage_f 参数：参数（1）"
    $sequenceExpander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 300
    Invoke-NamedButton $window "添加 Sequence 步骤"
    Save-StoryFlow $window
    [void](Find-NamedElement $window "sequence_stage_f 输出端口 2")
    Expand-NodeParameters $window "sequence_stage_f"
    Invoke-NamedButton $window "删除 Sequence 最后一步"
    Save-StoryFlow $window
    try { [void](Find-NamedElement $window "sequence_stage_f 输出端口 2" 700); throw "Sequence output 2 remained after unconnected removal." } catch { if ($_.Exception.Message -notlike "UI element 'sequence_stage_f 输出端口 2' was not found.*") { throw } }
    Expand-NodeParameters $window "sequence_stage_f"
    Invoke-NamedButton $window "添加 Sequence 步骤"
    Save-StoryFlow $window
    Mouse-Drag (Port-Center $window "sequence_stage_f 输出端口 2") (Relative-NodePoint $window $waitNodeName $waitInputRatio[0] $waitInputRatio[1])
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("sequence_stage_f.2->wait_evidence")
    Expand-NodeParameters $window "sequence_stage_f"
    Invoke-NamedButton $window "删除 Sequence 最后一步"
    Invoke-DialogButtonContaining "取消"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("sequence_stage_f.2->wait_evidence")
    Expand-NodeParameters $window "sequence_stage_f"
    Invoke-NamedButton $window "删除 Sequence 最后一步"
    Invoke-DialogButtonContaining "确定"
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Absent @("sequence_stage_f.2->wait_evidence")
    Write-Output "FLOW_SEQUENCE_STEP_PROTECTION_UNDO=PASS"

    # Grabbing an occupied input moves that input endpoint to another input; the source output stays fixed.
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") (Port-Center $window "wait_evidence 输入端口")
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->wait_evidence", "start_evidence.next->wait_evidence") -Absent @("exit_branch.hand_over->enter_kingdom")
    Mouse-Drag (Port-Center $window "wait_evidence 入线端口 exit_branch") (Port-Center $window "enter_kingdom 输入端口")
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom") -Absent @("exit_branch.hand_over->wait_evidence")
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") (Port-Center $window "wait_evidence 输入端口")
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom") -Absent @("exit_branch.hand_over->wait_evidence")
    Write-Output "FLOW_INPUT_ENDPOINT_RECONNECT_UNDO=PASS"

    # Esc restores the original line; blank release disconnects, and an empty input can still create a line to an output.
    Cancel-DragWithEscape (Port-Center $window "enter_kingdom 输入端口") $blank
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Write-Output "FLOW_RECONNECT_ESCAPE_RESTORE=PASS"
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") $blank
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Absent @("exit_branch.hand_over->enter_kingdom")
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") (Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1])
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Write-Output "FLOW_INPUT_TO_OUTPUT_NEW_CONNECTION=PASS"
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") $blank
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom")
    Write-Output "FLOW_BLANK_DISCONNECT_UNDO=PASS"

    # Grabbing an occupied output moves that output endpoint to another output; the target input stays fixed.
    Mouse-Drag (Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1]) (Relative-NodePoint $window $exitNodeName $concealRatio[0] $concealRatio[1])
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.conceal->enter_kingdom") -Absent @("exit_branch.hand_over->enter_kingdom", "exit_branch.conceal->enter_empire")
    Mouse-Drag (Relative-NodePoint $window $exitNodeName $concealRatio[0] $concealRatio[1]) (Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1])
    Mouse-Drag (Relative-NodePoint $window $exitNodeName $concealRatio[0] $concealRatio[1]) (Port-Center $window "enter_empire 输入端口")
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom", "exit_branch.conceal->enter_empire")
    Mouse-Drag (Relative-NodePoint $window $exitNodeName $handOverRatio[0] $handOverRatio[1]) (Relative-NodePoint $window $exitNodeName $concealRatio[0] $concealRatio[1])
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom", "exit_branch.conceal->enter_empire")
    Write-Output "FLOW_OUTPUT_ENDPOINT_RECONNECT_UNDO=PASS"

    # Multi-incoming fan handles move only their concrete input endpoint and remain one undo transaction.
    Mouse-Drag (Port-Center $window "enter_kingdom 输入端口") (Port-Center $window "wait_evidence 输入端口")
    Save-StoryFlow $window
    [void](Find-NamedElement $window "wait_evidence 入线端口 exit_branch")
    [void](Find-NamedElement $window "wait_evidence 入线端口 start_evidence")
    Mouse-Drag (Port-Center $window "wait_evidence 入线端口 exit_branch") (Port-Center $window "enter_kingdom 输入端口")
    Undo-FlowEdit
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->wait_evidence", "start_evidence.next->wait_evidence", "exit_branch.conceal->enter_empire")
    Mouse-Drag (Port-Center $window "wait_evidence 入线端口 exit_branch") (Port-Center $window "enter_kingdom 输入端口")
    Save-StoryFlow $window
    Assert-ConnectionState $storyFile -Present @("exit_branch.hand_over->enter_kingdom", "exit_branch.conceal->enter_empire", "start_evidence.next->wait_evidence")
    Write-Output "FLOW_MULTI_INCOMING_EXPLICIT_HANDLE_UNDO=PASS"

    Mouse-Click $blank Right
    $addMenu = Find-NamedElement $desktop "添加节点"
    $addMenu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 350
    foreach ($category in @("触发", "条件", "对话", "任务", "动作 / 奖励", "流程控制", "剧情")) {
        [void](Find-NamedElement $desktop $category)
    }
    [void](Find-NamedElement $desktop "结束")
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "FLOW_COMPLETE_REGISTRY_MENU=PASS"
    }

    # Add graph-only acceptance fixtures after Flow editing is complete so the intentional
    # missing target cannot disable any of the preceding saves.
    $graphStory = Get-Content -LiteralPath $storyFile -Raw | ConvertFrom-Json
    $graphStory.nodes += [pscustomobject]@{
        id = "branch_stage_h"
        type = "branch"
        position = [pscustomobject]@{ x = 1080; y = 700 }
        properties = [pscustomobject]@{ variable = "stage_h_route"; operator = "equals"; value = "kingdom" }
    }
    $graphStory.nodes += [pscustomobject]@{
        id = "enter_kingdom_parallel"
        type = "enter_story"
        position = [pscustomobject]@{ x = 1360; y = 700 }
        properties = [pscustomobject]@{ target_story_id = "kingdom_route" }
    }
    $graphStory.nodes += [pscustomobject]@{
        id = "enter_self_stage_h"
        type = "enter_story"
        position = [pscustomobject]@{ x = 1360; y = 860 }
        properties = [pscustomobject]@{ target_story_id = "royal_mystery" }
    }
    $graphStory.nodes += [pscustomobject]@{
        id = "enter_missing_stage_h"
        type = "enter_story"
        position = [pscustomobject]@{ x = 1640; y = 700 }
        properties = [pscustomobject]@{ target_story_id = "missing_stage_h" }
    }
    $graphStory.connections += [pscustomobject]@{ from = "branch_stage_h"; output = "true"; to = "enter_kingdom_parallel" }
    $graphStory | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $storyFile -Encoding utf8
    $graphStoryHash = (Get-FileHash -LiteralPath $storyFile -Algorithm SHA256).Hash

    if (-not $GraphOnly) { Invoke-NamedButton $window "← 返回项目首页" }
    Invoke-NamedButton $window "剧情图谱"
    [void](Find-NamedElement $window "剧情图谱自由布局画布")
    $aggregateEdge = Find-NamedElement $window "剧情图谱边 royal_mystery 到 kingdom_route 转场 2"
    $selfLoopEdge = Find-NamedElement $window "剧情图谱边 royal_mystery 到 royal_mystery 转场 1"
    foreach ($edge in @($aggregateEdge, $selfLoopEdge)) {
        $bounds = $edge.Current.BoundingRectangle
        if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
            throw "Project Graph edge '$($edge.Current.Name)' did not expose visible finite bounds."
        }
    }
    $aggregateEdge.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    [void](Find-NamedElement $desktop "查看来源 EnterStory（2）")
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Save-Screenshot $window "02-project-graph-aggregate-self-loop-dpi-$windowDpi.png"
    Write-Output "PROJECT_GRAPH_AGGREGATE_SELF_LOOP_EDGE_UI=PASS"

    # SCC/cycle layout must complete and leave all graph nodes at finite, discoverable positions.
    Invoke-NamedButton $window "剧情图谱自动布局"
    foreach ($graphNodeName in @(
        "剧情图谱节点 王城迷案 royal_mystery",
        "剧情图谱节点 王国线 kingdom_route",
        "剧情图谱节点 帝国线 empire_route")) {
        $node = Find-NamedElement $window $graphNodeName
        if ($null -eq $node -or $node.Current.BoundingRectangle.Width -le 0 -or $node.Current.BoundingRectangle.Height -le 0) {
            throw "Project Graph auto-layout did not leave a visible node for '$graphNodeName'."
        }
    }

    # A graph diagnostic must route back to the exact source EnterStory and target field.
    Invoke-NamedButton $window "打开剧情图谱错误"
    $graphProblem = Find-NamedElementContaining $window "royal_mystery.enter_missing_stage_h 指向不存在的 Story 'missing_stage_h'。"
    $graphProblemPoint = $graphProblem.GetClickablePoint()
    Mouse-DoubleClick ([System.Drawing.Point]::new([int]$graphProblemPoint.X, [int]$graphProblemPoint.Y))
    $missingNode = Find-NamedElement $window "Flow 节点 进入剧情 enter_missing_stage_h"
    $targetField = Find-NamedElement $missingNode "节点内资源选择器"
    if (-not $targetField.Current.HasKeyboardFocus) {
        $actualFocus = [System.Windows.Automation.AutomationElement]::FocusedElement
        throw "Project Graph problem navigation did not focus target_story_id. Actual focus: '$($actualFocus.Current.Name)' ($($actualFocus.Current.ControlType.ProgrammaticName))."
    }
    Save-Screenshot $window "03-project-graph-missing-target-focus-dpi-$windowDpi.png"
    Write-Output "PROJECT_GRAPH_MISSING_TARGET_FOCUS=PASS"

    $graphStoryHashAfter = (Get-FileHash -LiteralPath $storyFile -Algorithm SHA256).Hash
    if ($graphStoryHashAfter -ne $graphStoryHash) { throw "Project Graph operations modified the QA Story JSON." }
    Write-Output "PROJECT_GRAPH_STORY_JSON_UNCHANGED=PASS"
}
finally {
    if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) }
    if (Test-Path -LiteralPath $qaRoot) { Remove-Item -LiteralPath $qaRoot -Recurse -Force }
}

$hashesAfter = Get-ChildItem -LiteralPath $sourceStories -Filter '*.json' -File | Sort-Object Name | ForEach-Object {
    "$($_.Name):$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)"
}
if (($hashesBefore -join '|') -ne ($hashesAfter -join '|')) { throw "Connection UI acceptance modified Runtime Story JSON." }

if (-not $GraphOnly) { Write-Output "STUDIO_212_CONNECTION_UI=PASS" }
Write-Output "STUDIO_212_PROJECT_GRAPH_UI=PASS"
Write-Output "STUDIO_212_SYSTEM_DPI=$windowDpi"
Write-Output "STUDIO_212_CONNECTION_JSON_UNCHANGED=PASS"
