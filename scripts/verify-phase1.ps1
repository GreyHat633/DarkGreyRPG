[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $errors.Add($Message)
}

function Read-JsonUtf8 {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

$projectPath = Join-Path $projectRoot 'examples\phase1_project'
$projectJsonPath = Join-Path $projectPath 'project.json'
$actorDirectory = Join-Path $projectPath 'actors'

if (-not (Test-Path -LiteralPath $projectJsonPath -PathType Leaf)) {
    Add-CheckError "Missing project.json"
} else {
    $project = Read-JsonUtf8 $projectJsonPath
    if ($project.schema_version -ne 1) {
        Add-CheckError "project.json schema_version must be 1"
    }
}

$allowedActorFields = @('schema_version', 'id', 'display_name', 'notes', 'tags')
$ids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$actorFiles = @(Get-ChildItem -LiteralPath $actorDirectory -File -Filter '*.json')
foreach ($file in $actorFiles) {
    $actor = Read-JsonUtf8 $file.FullName
    $fieldNames = @($actor.PSObject.Properties.Name)
    foreach ($field in $fieldNames) {
        if ($field -notin $allowedActorFields) {
            Add-CheckError "$($file.Name): unsupported Actor field '$field'"
        }
    }
    if ($actor.schema_version -ne 1) {
        Add-CheckError "$($file.Name): schema_version must be 1"
    }
    if ([string]::IsNullOrWhiteSpace($actor.id) -or $actor.id -cnotmatch '^[a-z0-9][a-z0-9_.-]*$') {
        Add-CheckError "$($file.Name): invalid Actor ID"
    }
    if ($file.Name -cne "$($actor.id).json") {
        Add-CheckError "$($file.Name): filename must match Actor ID"
    }
    if (-not $ids.Add([string]$actor.id)) {
        Add-CheckError "$($file.Name): duplicate Actor ID '$($actor.id)'"
    }
    if ([string]::IsNullOrWhiteSpace($actor.display_name)) {
        Add-CheckError "$($file.Name): display_name is required"
    }
}

$requiredCommandFragments = @(
    '"status"',
    '"reload"',
    '"list"',
    '"info"',
    '"select"',
    '"bind"',
    '"unbind"'
)
$commandPath = Join-Path $projectRoot 'src\main\java\darkgrey\rpg\command\CommandDarkGreyRpg.java'
$commandSource = Get-Content -LiteralPath $commandPath -Raw -Encoding UTF8
foreach ($fragment in $requiredCommandFragments) {
    if (-not $commandSource.Contains($fragment)) {
        Add-CheckError "Command implementation is missing $fragment"
    }
}

$bindingPath = Join-Path $projectRoot 'src\main\java\darkgrey\rpg\compat\customnpcs\CustomNpcActorBinding.java'
$bindingSource = Get-Content -LiteralPath $bindingPath -Raw -Encoding UTF8
foreach ($fragment in @(
    'darkgrey_rpg.actor_id',
    'EntityNPCInterface',
    'wrappedNPC',
    'getStoredData',
    'setStoredData',
    'removeStoredData'
)) {
    if (-not $bindingSource.Contains($fragment)) {
        Add-CheckError "CNPC binding implementation is missing '$fragment'"
    }
}

$phaseTwoPatterns = @(
    'class DialogueDefinition',
    'class QuestDefinition',
    'class StoryDefinition',
    'StartQuest'
)
$javaSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\main\java') -Recurse -File -Filter '*.java')
foreach ($source in $javaSources) {
    $text = Get-Content -LiteralPath $source.FullName -Raw -Encoding UTF8
    foreach ($pattern in $phaseTwoPatterns) {
        if ($text.Contains($pattern)) {
            Add-CheckError "Out-of-scope Phase 2+ implementation marker '$pattern' in $($source.FullName)"
        }
    }
}

$studioFiles = @(
    'studio\project.godot',
    'studio\Main.tscn',
    'studio\scripts\Main.gd',
    'studio\scripts\RpgProjectStore.gd'
)
foreach ($relativePath in $studioFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $relativePath) -PathType Leaf)) {
        Add-CheckError "Missing Studio file: $relativePath"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "PHASE1_STATIC_VERIFY=PASS"
Write-Host "ACTOR_JSON_COUNT=$($actorFiles.Count)"
Write-Host "COMMAND_SET=PASS"
Write-Host "CNPC_STORED_DATA_BINDING=PASS"
Write-Host "PHASE2_SCOPE_GUARD=PASS"
Write-Host "STUDIO_SHELL_FILES=PASS"
