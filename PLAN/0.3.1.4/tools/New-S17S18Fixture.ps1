[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s17-s18-project',
    [string] $MetadataPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s17-s18-project\fixture.json'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repository '.tooling')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$metadata = [IO.Path]::GetFullPath($MetadataPath)
if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "S17/S18 fixture must stay below $tooling"
}
if (-not $metadata.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "S17/S18 metadata must stay below $tooling"
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
    "{`n  `"schema_version`": 2,`n  `"id`": `"s17_s18_gate`",`n  `"display_name`": `"0314 S17 S18 Gate`"`n}`n",
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

function ConvertTo-JsonElement([string] $Value) {
    return [Text.Json.JsonDocument]::Parse(($Value | ConvertTo-Json -Compress)).RootElement.Clone()
}

function New-SessionEnvelope([string] $Id, [string] $Name, [string] $Suffix) {
    $start = $factory::Create($scopeType::Session, 'start', "${Suffix}_start", "${Name} 起始")
    $line = $factory::Create($scopeType::Session, 'line', "${Suffix}_line", "${Name} 台词")
    $end = $factory::Create($scopeType::Session, 'end', "${Suffix}_end", "${Name} 结束")
    $end.Properties['port_id'] = ConvertTo-JsonElement "${Suffix}_end_port"
    $end.Properties['display_name'] = ConvertTo-JsonElement "${Name} 完成"
    $connections = @(
        $connectionType::new($start.Id, 'flow_out', $line.Id, 'flow_in', $interfaceType::Flow),
        $connectionType::new($line.Id, 'flow_out', $end.Id, 'flow_in', $interfaceType::Flow)
    )
    return New-Envelope $kindType::Session $Id $Name @($start, $line, $end) $connections
}

$storyId = 's17_s18_gate'
$storyName = '0314 生命周期与排序门'
$sessionA = New-SessionEnvelope 'session_a' '会话 A' 'session_a'
$sessionB = New-SessionEnvelope 'session_b' '会话 B（删除）' 'session_b'
$sessionC = New-SessionEnvelope 'session_c' '会话 C' 'session_c'

$storyStart = $factory::CreateStoryStart('story_start', '生命周期门开始', 'story_trigger')
$storyAction = $factory::Create($scopeType::StoryFlow, 'action', 'story_action', 'B placement wire')
$storyGraph = [DarkGreyRPG.Studio.Core.Graphs.GraphDocument]::new()
$aggregateFactory = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalAggregateNodeFactory]
$aggregateA = $aggregateFactory::Create($storyGraph, $sessionA, 'placement_a').Candidate
$aggregateB = $aggregateFactory::Create($storyGraph, $sessionB, 'placement_b').Candidate
if ($null -eq $aggregateA -or $null -eq $aggregateB) { throw 'Could not create lifecycle aggregate placements.' }
$storyConnections = @(
    $connectionType::new($storyStart.Id, 'story_trigger', $storyAction.Id, 'flow_in', $interfaceType::Flow),
    $connectionType::new($storyAction.Id, 'flow_out', $aggregateB.Id, 'flow_in', $interfaceType::Flow)
)
$story = New-Envelope $kindType::Story $storyId $storyName @($storyStart, $storyAction, $aggregateA, $aggregateB) $storyConnections

[DarkGreyRPG.Studio.Core.Stories.StoryRepository]::new($project).CreateStory($storyId, $storyName) | Out-Null
$store = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalProjectGraphStore]::new($project)
$store.EnsureDirectories()
$store.Stories.Create($story) | Out-Null
$store.Sessions.Create($sessionA) | Out-Null
$store.Sessions.Create($sessionB) | Out-Null
$store.Sessions.Create($sessionC) | Out-Null

$owned = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new()
$owned.Sessions = [Collections.Generic.List[string]]@('session_a', 'session_b', 'session_c')
$membership = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipManifest]::new($storyId, $owned, [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipSet]::new())
$membership.SchemaVersion = [DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryMembershipManifest]::CurrentSchemaVersion
$displayOrder = $membership.DisplayOrder
$displayOrder.Sessions = [Collections.Generic.List[string]]@('session_a', 'session_b', 'session_c')
$membership.DisplayOrder = $displayOrder
$store.Memberships.Create($membership) | Out-Null

$metadataObject = [ordered]@{
    schema_version = 1
    project = $project
    story_id = $storyId
    story_display_name = $storyName
    sessions = [ordered]@{
        a = [ordered]@{ id = 'session_a'; display_name = '会话 A' }
        b = [ordered]@{ id = 'session_b'; display_name = '会话 B（删除）' }
        c = [ordered]@{ id = 'session_c'; display_name = '会话 C' }
    }
    placement_a = 'placement_a'
    placement_b = 'placement_b'
    expected_initial_order = @('session_a', 'session_b', 'session_c')
}
[IO.File]::WriteAllText($metadata, ($metadataObject | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    ProjectDirectory = $project
    MetadataPath = $metadata
    Story = $storyId
    Sessions = @('session_a', 'session_b', 'session_c')
    Placements = @('placement_a', 'placement_b')
} | ConvertTo-Json -Depth 8
