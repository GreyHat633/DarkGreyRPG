[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage3-wire-project'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repository '.tooling')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Stage 3 fixture must stay below $tooling"
}

$core = Join-Path $repository 'studio\src\DarkGreyRPG.Studio.Core\bin\Release\net10.0\DarkGreyRPG.Studio.Core.dll'
if (-not (Test-Path -LiteralPath $core)) { throw "Core assembly not found: $core" }
Add-Type -Path $core

if (Test-Path -LiteralPath $project) { Remove-Item -LiteralPath $project -Recurse -Force }
New-Item -ItemType Directory -Path $project -Force | Out-Null
foreach ($directoryName in @('actors', 'dialogues', 'quests', 'stories')) {
    New-Item -ItemType Directory -Path (Join-Path $project $directoryName) -Force | Out-Null
}
[IO.File]::WriteAllText(
    (Join-Path $project 'project.json'),
    "{`n  `"schema_version`": 2,`n  `"id`": `"wire_gate`",`n  `"display_name`": `"0314 Wire Gate`"`n}`n",
    [Text.UTF8Encoding]::new($false))

$scopeType = [DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope]
$factory = [DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory]
$connectionType = [DarkGreyRPG.Studio.Core.Graphs.GraphConnection]
$documentType = [DarkGreyRPG.Studio.Core.Graphs.GraphDocument]
$kindType = [DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind]
$envelopeType = [DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope]
$interfaceType = [DarkGreyRPG.Studio.Core.Graphs.GraphInterfaceKind]

function New-Envelope {
    param($Kind, [string] $Id, [string] $Name, [object[]] $Nodes, [object[]] $Connections)
    $document = $documentType::new(
        [DarkGreyRPG.Studio.Core.Graphs.GraphNode[]]$Nodes,
        [DarkGreyRPG.Studio.Core.Graphs.GraphConnection[]]$Connections)
    return $envelopeType::new($Kind, $Id, $Name, $document)
}

function New-SessionNode([string] $Type, [string] $Id, [string] $Name) {
    return $factory::Create($scopeType::Session, $Type, $Id, $Name)
}

function New-TaskNode([string] $Type, [string] $Id, [string] $Name) {
    return $factory::Create($scopeType::Task, $Type, $Id, $Name)
}

function Add-SettleSlot($Node, [string] $PortId) {
    $Node.Ports.Add([DarkGreyRPG.Studio.Core.Graphs.GraphPort]::new(
        $PortId, '结果 1', $true, $interfaceType::Logic, 0))
}

function Add-LogicInput($Node, [string] $PortId, [string] $Name, [int] $Order) {
    $Node.Ports.Add([DarkGreyRPG.Studio.Core.Graphs.GraphPort]::new(
        $PortId, $Name, $true, $interfaceType::Logic, $Order))
}

function Set-StringProperty($Node, [string] $Name, [string] $Value) {
    $json = [Text.Json.JsonDocument]::Parse(($Value | ConvertTo-Json -Compress))
    try { $Node.Properties[$Name] = $json.RootElement.Clone() }
    finally { $json.Dispose() }
}

$storyStart = $factory::CreateStoryStart('wire_story_start', '线手势开始', 'wire_trigger')
$storyEnd = $factory::Create($scopeType::StoryFlow, 'terminate', 'wire_story_end', '线手势终止')
$story = New-Envelope $kindType::Story 'wire_gate' '0314 线手势门' @($storyStart, $storyEnd) @(
    $connectionType::new('wire_story_start', 'wire_trigger', 'wire_story_end', 'flow_in', $interfaceType::Flow))

$flowSource = New-SessionNode 'line' 'flow_source' '单线原输出'
$flowReplacement = New-SessionNode 'line' 'flow_replacement' '单线替换输出'
$flowFixed = New-SessionNode 'line' 'flow_fixed' '单线固定输入'
$flowEnd = New-SessionNode 'end' 'flow_end' '单线结束'
Set-StringProperty $flowEnd 'port_id' 'flow_single_end'
Set-StringProperty $flowEnd 'display_name' '单线结束'
$flowSingleStart = New-SessionNode 'start' 'flow_single_start' '单线起始'
$flowSingle = New-Envelope $kindType::Session 'wire_flow_single' '线手势：Flow 单线' @(
    $flowSingleStart, $flowSource, $flowReplacement, $flowFixed, $flowEnd) @(
    $connectionType::new('flow_source', 'flow_out', 'flow_fixed', 'flow_in', $interfaceType::Flow))

$multiSources = 1..3 | ForEach-Object { New-SessionNode 'line' "flow_multi_source_$_" "多线来源 $_" }
$multiExtra = New-SessionNode 'line' 'flow_multi_extra' '普通新增来源'
$multiTarget = New-SessionNode 'line' 'flow_multi_target' '多线原输入'
$multiReplacement = New-SessionNode 'line' 'flow_multi_replacement' '整束替换输入'
$multiEnd = New-SessionNode 'end' 'flow_multi_end' '多线结束'
Set-StringProperty $multiEnd 'port_id' 'flow_multi_end'
Set-StringProperty $multiEnd 'display_name' '多线结束'
$flowMultiStart = New-SessionNode 'start' 'flow_multi_start' '多线起始'
$flowMultiConnections = 1..3 | ForEach-Object {
    $connectionType::new("flow_multi_source_$_", 'flow_out', 'flow_multi_target', 'flow_in', $interfaceType::Flow)
}
$flowMulti = New-Envelope $kindType::Session 'wire_flow_multi' '线手势：Flow 多线' @(
    @($flowMultiStart) + $multiSources + @($multiExtra, $multiTarget, $multiReplacement, $multiEnd)) $flowMultiConnections

$logicSource = New-TaskNode 'not' 'logic_source' '单线原逻辑输出'
$logicFixed = New-TaskNode 'not' 'logic_fixed' '单线固定逻辑输入'
$logicReplacement = New-TaskNode 'not' 'logic_replacement' '单线替换逻辑输入'
$logicSettle = New-TaskNode 'settle' 'logic_single_settle' '单线结算'
Add-SettleSlot $logicSettle 'logic_single_settle_in'
$logicSingle = New-Envelope $kindType::Task 'wire_logic_single' '线手势：Logic 单线' @(
    $logicSource, $logicFixed, $logicReplacement, $logicSettle) @(
    $connectionType::new('logic_source', 'logic_out', 'logic_fixed', 'logic_in', $interfaceType::Logic))

$logicBundle = New-TaskNode 'and' 'logic_multi_source' '多线原逻辑输出'
$logicReplacementOutput = New-TaskNode 'or' 'logic_multi_replacement' '整束替换逻辑输出'
Add-LogicInput $logicBundle 'logic_multi_source_in_a' '条件 1' 1
Add-LogicInput $logicBundle 'logic_multi_source_in_b' '条件 2' 2
Add-LogicInput $logicReplacementOutput 'logic_multi_replacement_in_a' '条件 1' 1
Add-LogicInput $logicReplacementOutput 'logic_multi_replacement_in_b' '条件 2' 2
$logicTargets = 1..3 | ForEach-Object { New-TaskNode 'not' "logic_multi_target_$_" "多线目标 $_" }
$logicExtraTarget = New-TaskNode 'not' 'logic_multi_extra' '普通新增目标'
$logicSettle2 = New-TaskNode 'settle' 'logic_multi_settle' '多线结算'
Add-SettleSlot $logicSettle2 'logic_multi_settle_in'
$logicMultiConnections = 1..3 | ForEach-Object {
    $connectionType::new('logic_multi_source', 'logic_out', "logic_multi_target_$_", 'logic_in', $interfaceType::Logic)
}
$logicMulti = New-Envelope $kindType::Task 'wire_logic_multi' '线手势：Logic 多线' @(
    @($logicBundle, $logicReplacementOutput) + $logicTargets + @($logicExtraTarget, $logicSettle2)) $logicMultiConnections

[DarkGreyRPG.Studio.Core.Stories.StoryRepository]::new($project).CreateStory('wire_gate', '0314 线手势门') | Out-Null
$store = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalProjectGraphStore]::new($project)
$store.Stories.Create($story) | Out-Null
foreach ($session in @($flowSingle, $flowMulti)) { $store.Sessions.Create($session) | Out-Null }
foreach ($task in @($logicSingle, $logicMulti)) { $store.Tasks.Create($task) | Out-Null }

$owned = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new()
$owned.Sessions = [Collections.Generic.List[string]]@('wire_flow_single', 'wire_flow_multi')
$owned.Tasks = [Collections.Generic.List[string]]@('wire_logic_single', 'wire_logic_multi')
$manifest = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipManifest]::new(
    'wire_gate', $owned, [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new())
$store.Memberships.Create($manifest) | Out-Null

$layout = [ordered]@{
    schema_version = 1
    graphs = [ordered]@{
        'session:wire_flow_single' = [ordered]@{
            flow_single_start = @{ x = 80; y = 520 }
            flow_source = @{ x = 80; y = 110 }; flow_replacement = @{ x = 80; y = 330 }
            flow_fixed = @{ x = 520; y = 110 }; flow_end = @{ x = 880; y = 110 }
        }
        'session:wire_flow_multi' = [ordered]@{
            flow_multi_start = @{ x = 50; y = 630 }
            flow_multi_source_1 = @{ x = 50; y = 30 }; flow_multi_source_2 = @{ x = 50; y = 180 }
            flow_multi_source_3 = @{ x = 50; y = 330 }; flow_multi_extra = @{ x = 50; y = 480 }
            flow_multi_target = @{ x = 520; y = 210 }; flow_multi_replacement = @{ x = 860; y = 210 }
            flow_multi_end = @{ x = 1160; y = 210 }
        }
        'task:wire_logic_single' = [ordered]@{
            logic_source = @{ x = 70; y = 130 }; logic_fixed = @{ x = 500; y = 80 }
            logic_replacement = @{ x = 500; y = 330 }; logic_single_settle = @{ x = 900; y = 160 }
        }
        'task:wire_logic_multi' = [ordered]@{
            logic_multi_source = @{ x = 70; y = 210 }; logic_multi_replacement = @{ x = 70; y = 470 }
            logic_multi_target_1 = @{ x = 520; y = 30 }; logic_multi_target_2 = @{ x = 520; y = 180 }
            logic_multi_target_3 = @{ x = 520; y = 330 }; logic_multi_extra = @{ x = 520; y = 480 }
            logic_multi_settle = @{ x = 900; y = 210 }
        }
    }
}
$layoutPath = Join-Path $project 'resources\editor\studio_layout.json'
New-Item -ItemType Directory -Path (Split-Path -Parent $layoutPath) -Force | Out-Null
[IO.File]::WriteAllText($layoutPath, ($layout | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))

$settingsPath = Join-Path (Split-Path -Parent $project) '0314-stage3-settings.json'
$settings = @{
    schema_version = 1; theme = 'Dark'; window_width = 1540; window_height = 900; window_maximized = $false
    resource_browser_width = 260; story_resource_library_width = 280; bottom_panel_height = 180
    last_project = $project; recent_projects = @($project)
}
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 5) + "`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    ProjectDirectory = $project
    SettingsPath = $settingsPath
    Story = $story.Id
    Sessions = @($flowSingle.Id, $flowMulti.Id)
    Tasks = @($logicSingle.Id, $logicMulti.Id)
} | ConvertTo-Json -Depth 4
