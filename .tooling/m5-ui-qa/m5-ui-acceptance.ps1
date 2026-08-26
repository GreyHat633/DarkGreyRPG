param(
    [string]$RepositoryRoot = "E:\Java\MinecraftMod\DarkGrey_RPG"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$settingsPath = Join-Path $RepositoryRoot ".tooling\m5-ui-qa\settings.json"
$studioDll = Join-Path $RepositoryRoot "Studio\src\DarkGreyRPG.Studio\bin\Debug\net10.0-windows\DarkGreyRPGStudio.dll"
$dotnetPath = "E:\Java\dotnet-sdk-10\dotnet.exe"
$screenshotsPath = Join-Path $RepositoryRoot ".tooling\m5-ui-qa\screenshots"
$dialoguePath = Join-Path $RepositoryRoot ".tooling\m5-ui-qa\project-m5\dialogues\greeting.json"
$questPath = Join-Path $RepositoryRoot ".tooling\m5-ui-qa\project-m5\quests\first_task.json"

New-Item -ItemType Directory -Force -Path $screenshotsPath | Out-Null
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

function Find-NamedElement {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [int]$TimeoutMilliseconds = 5000
    )

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
    param(
        [int]$ProcessId,
        [int]$TimeoutMilliseconds = 10000
    )
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $windows.Count; $index++) {
            $window = $windows.Item($index)
            if ($window.Current.ProcessId -eq $ProcessId -and $window.Current.Name -eq "DarkGrey RPG Studio 2.1") {
                return $window
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Studio main window was not found."
}

function Select-ListItemContaining {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$ChildName
    )

    $element = Find-NamedElement $Root $ChildName
    for ($depth = 0; $depth -lt 12 -and $null -ne $element; $depth++) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $pattern = $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $pattern.Select()
            Start-Sleep -Milliseconds 250
            return
        }
        $element = $walker.GetParent($element)
    }
    throw "No selectable list item contains '$ChildName'."
}

function Invoke-NamedButton {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name
    )
    $button = Find-NamedElement $Root $Name
    if (-not $button.Current.IsEnabled) { throw "Button '$Name' is disabled." }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    Start-Sleep -Milliseconds 350
}

function Set-NamedValue {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [string]$Value
    )
    $element = Find-NamedElement $Root $Name
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
    Start-Sleep -Milliseconds 200
}

function Get-NamedValue {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name
    )
    $element = Find-NamedElement $Root $Name
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    return $pattern.Current.Value
}

function Save-WindowScreenshot {
    param(
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$FileName
    )
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

function Start-Studio {
    $env:DARKGREYRPG_STUDIO_SETTINGS_PATH = $settingsPath
    $process = Start-Process -FilePath $dotnetPath -ArgumentList @($studioDll) -PassThru
    $window = Find-StudioWindow $process.Id
    return @{ Process = $process; Window = $window }
}

function Enter-HomeStory {
    param([System.Windows.Automation.AutomationElement]$Window)
    Select-ListItemContaining $Window "M5 对话与任务编辑器真实窗口验收。"
    Invoke-NamedButton $Window "进入剧情"
}

function Close-Studio {
    param($Studio)
    if (-not $Studio.Process.HasExited) {
        $Studio.Process.Kill()
        $Studio.Process.WaitForExit(5000)
    }
}

$firstRun = Start-Studio
try {
    Enter-HomeStory $firstRun.Window

    Select-ListItemContaining $firstRun.Window "对话"
    Select-ListItemContaining $firstRun.Window "greeting"
    Set-NamedValue $firstRun.Window "Dialogue 显示名称" "港口问候（临时）"
    Set-NamedValue $firstRun.Window "Dialogue 显示名称" "港口问候（已验收）"
    Select-ListItemContaining $firstRun.Window "line_1"
    Set-NamedValue $firstRun.Window "Dialogue 台词" "UI 自动化保存成功。"
    Invoke-NamedButton $firstRun.Window "保存 Dialogue"
    Save-WindowScreenshot $firstRun.Window "01-dialogue-editor.png"

    Select-ListItemContaining $firstRun.Window "任务"
    Select-ListItemContaining $firstRun.Window "寻找航海图"
    Set-NamedValue $firstRun.Window "Quest 描述" "临时描述"
    Set-NamedValue $firstRun.Window "Quest 描述" "真实窗口已完成任务编辑与保存。"
    Invoke-NamedButton $firstRun.Window "添加击杀目标"
    Set-NamedValue $firstRun.Window "KillEntity 实体" "Zombie"
    Invoke-NamedButton $firstRun.Window "保存 Quest"
    Save-WindowScreenshot $firstRun.Window "02-quest-editor.png"
}
finally {
    Close-Studio $firstRun
}

$dialogue = Get-Content -LiteralPath $dialoguePath -Raw | ConvertFrom-Json
$quest = Get-Content -LiteralPath $questPath -Raw | ConvertFrom-Json
if ($dialogue.display_name -ne "港口问候（已验收）") { throw "Dialogue display name was not persisted." }
if (($dialogue.nodes | Where-Object id -eq "line_1").text -ne "UI 自动化保存成功。") { throw "Dialogue line was not persisted." }
if ($quest.description -ne "真实窗口已完成任务编辑与保存。") { throw "Quest description was not persisted." }
if (-not ($quest.objectives | Where-Object { $_.type -eq "kill_entity" -and $_.entity -eq "Zombie" })) { throw "Quest KillEntity objective was not persisted." }

$restart = Start-Studio
try {
    Enter-HomeStory $restart.Window
    Select-ListItemContaining $restart.Window "对话"
    Select-ListItemContaining $restart.Window "港口问候（已验收）"
    if ((Get-NamedValue $restart.Window "Dialogue 显示名称") -ne "港口问候（已验收）") { throw "Dialogue display name was not restored after restart." }
    Select-ListItemContaining $restart.Window "line_1"
    if ((Get-NamedValue $restart.Window "Dialogue 台词") -ne "UI 自动化保存成功。") { throw "Dialogue line was not restored after restart." }
    Save-WindowScreenshot $restart.Window "03-restart-persistence.png"

    $requiredAccessibleNames = @(
        "创建 Dialogue", "引用 Dialogue", "搜索 Dialogue", "剧情 Dialogue 列表",
        "Dialogue 节点列表", "Dialogue 显示名称", "Dialogue 台词", "保存 Dialogue")
    foreach ($name in $requiredAccessibleNames) {
        [void](Find-NamedElement $restart.Window $name)
    }
}
finally {
    Close-Studio $restart
}

Write-Output "M5_UI_DIALOGUE_EDIT_SAVE=PASS"
Write-Output "M5_UI_QUEST_EDIT_SAVE=PASS"
Write-Output "M5_UI_RESTART_PERSISTENCE=PASS"
Write-Output "M5_UI_AUTOMATION_TREE=PASS"
