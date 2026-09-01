[CmdletBinding()]
param(
    [string] $RepositoryRoot = 'E:\Java\MinecraftMod\DarkGrey_RPG',
    [string] $ProjectDirectory = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s20-authoring-project',
    [string] $MetadataPath = 'E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0314-s20-authoring-project\fixture.json'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$tooling = [IO.Path]::GetFullPath((Join-Path $repository '.tooling')).TrimEnd('\')
$project = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\')
$metadata = [IO.Path]::GetFullPath($MetadataPath)

if (-not $project.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "S20 smoke project must remain below $tooling"
}
if (-not $metadata.StartsWith($tooling + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "S20 metadata must remain below $tooling"
}
if (-not $metadata.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "S20 metadata must remain inside the S20 smoke project: $project"
}
if ([IO.Path]::GetFileName($project) -notmatch 's20') {
    throw "Refusing to recreate a non-S20 directory: $project"
}

# This is the only authoring fixture write.  The directory contains no Story,
# actor, item, Session, Task, membership, graph node, or graph connection.
if (Test-Path -LiteralPath $project) { Remove-Item -LiteralPath $project -Recurse -Force }
New-Item -ItemType Directory -Path $project -Force | Out-Null
foreach ($directory in @('actors', 'dialogues', 'quests', 'stories', 'items', 'resources')) {
    New-Item -ItemType Directory -Path (Join-Path $project $directory) -Force | Out-Null
}

$projectJson = [ordered]@{
    schema_version = 2
    id = 's20_authoring_smoke'
    display_name = '0314 S20 Canonical Authoring Smoke'
}
[IO.File]::WriteAllText(
    (Join-Path $project 'project.json'),
    ($projectJson | ConvertTo-Json -Depth 4) + "`n",
    [Text.UTF8Encoding]::new($false))

$metadataObject = [ordered]@{
    schema_version = 1
    project = $project
    story_id = 'slime_cleanup_request'
    story_display_name = '史莱姆清理委托'
    actor_group_id = 'slimes'
    actor_id = 'tavern_boss'
    item_id = 'copper_coin'
    session_id = 'slime_cleanup_session'
    task_id = 'slime_cleanup_task'
    objective_type = 'kill_entity'
    objective_entity = 'slimes'
    objective_required = 10
    expected_choice_options = 3
    expected_empty_before_ui = $true
}
[IO.File]::WriteAllText($metadata, ($metadataObject | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    ProjectDirectory = $project
    MetadataPath = $metadata
    UnexpectedFilesPresent = @(
        Get-ChildItem -LiteralPath $project -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notin @((Join-Path $project 'project.json'), $metadata) } |
            Select-Object -ExpandProperty FullName
    )
    StoryId = $metadataObject.story_id
} | ConvertTo-Json -Depth 8
