[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-stage2-layout-project'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repository '.tooling')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Stage 2 fixture must stay below $tooling"
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
    "{`n  `"schema_version`": 2,`n  `"id`": `"layout_gate`",`n  `"display_name`": `"0314 Layout Gate`"`n}`n",
    [Text.UTF8Encoding]::new($false))

$scopeType = [DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope]
$factory = [DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory]
$nodeType = [DarkGreyRPG.Studio.Core.Graphs.GraphNode]
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

$storyStart = $factory::CreateStoryStart('story_start', '布局开始', 'layout_trigger')
$storyEnd = $factory::Create($scopeType::StoryFlow, 'terminate', 'story_end', '布局终止')
$storyConnection = $connectionType::new('story_start', 'layout_trigger', 'story_end', 'flow_in', $interfaceType::Flow)
$story = New-Envelope $kindType::Story 'layout_gate' '0314 布局门' @($storyStart, $storyEnd) @($storyConnection)

function New-SessionEnvelope([string] $Id, [string] $Name, [string] $Suffix) {
    $start = $factory::Create($scopeType::Session, 'start', "${Suffix}_start", "${Name} 起始")
    $line = $factory::Create($scopeType::Session, 'line', "${Suffix}_line", "${Name} 台词")
    $end = $factory::Create($scopeType::Session, 'end', "${Suffix}_end", "${Name} 结束")
    $connections = @(
        $connectionType::new($start.Id, 'flow_out', $line.Id, 'flow_in', $interfaceType::Flow),
        $connectionType::new($line.Id, 'flow_out', $end.Id, 'flow_in', $interfaceType::Flow)
    )
    return New-Envelope $kindType::Session $Id $Name @($start, $line, $end) $connections
}

function New-TaskEnvelope([string] $Id, [string] $Name, [string] $Suffix) {
    $objective = $factory::Create($scopeType::Task, 'objective', "${Suffix}_objective", "${Name} 目标")
    $settle = $factory::Create($scopeType::Task, 'settle', "${Suffix}_settle", "${Name} 结算")
    $settle.Ports.Add([DarkGreyRPG.Studio.Core.Graphs.GraphPort]::new(
        "${Suffix}_settle_in", '结果 1', $true, $interfaceType::Logic, 0))
    $connection = $connectionType::new(
        $objective.Id, 'logic_status', $settle.Id, "${Suffix}_settle_in", $interfaceType::Logic)
    return New-Envelope $kindType::Task $Id $Name @($objective, $settle) @($connection)
}

$sessionA = New-SessionEnvelope 'layout_session_a' '布局会话 A' 'session_a'
$sessionB = New-SessionEnvelope 'layout_session_b' '布局会话 B' 'session_b'
$taskA = New-TaskEnvelope 'layout_task_a' '布局任务 A' 'task_a'
$taskB = New-TaskEnvelope 'layout_task_b' '布局任务 B' 'task_b'

[DarkGreyRPG.Studio.Core.Stories.StoryRepository]::new($project).CreateStory('layout_gate', '0314 布局门') | Out-Null
$store = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalProjectGraphStore]::new($project)
$store.Stories.Create($story) | Out-Null
$store.Sessions.Create($sessionA) | Out-Null
$store.Sessions.Create($sessionB) | Out-Null
$store.Tasks.Create($taskA) | Out-Null
$store.Tasks.Create($taskB) | Out-Null

$owned = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new()
$owned.Sessions = [Collections.Generic.List[string]]@('layout_session_a', 'layout_session_b')
$owned.Tasks = [Collections.Generic.List[string]]@('layout_task_a', 'layout_task_b')
$manifest = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipManifest]::new(
    'layout_gate', $owned, [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new())
$store.Memberships.Create($manifest) | Out-Null

$settingsPath = Join-Path (Split-Path -Parent $project) '0314-stage2-settings.json'
$settingsJson = @{
    schema_version = 1
    theme = 'Dark'
    window_width = 1540
    window_height = 900
    window_maximized = $false
    resource_browser_width = 260
    story_resource_library_width = 280
    bottom_panel_height = 180
    last_project = $project
    recent_projects = @($project)
} | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($settingsPath, $settingsJson + "`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    ProjectDirectory = $project
    SettingsPath = $settingsPath
    Story = $story.Id
    Sessions = @($sessionA.Id, $sessionB.Id)
    Tasks = @($taskA.Id, $taskB.Id)
} | ConvertTo-Json -Depth 4
