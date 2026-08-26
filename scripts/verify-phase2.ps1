[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $errors.Add($Message)
}

$dialoguePath = Join-Path $projectRoot 'examples\phase2_project\dialogues\tavern_offer.json'
$dialogue = Get-Content -LiteralPath $dialoguePath -Raw -Encoding UTF8 | ConvertFrom-Json
$allowedTopFields = @('schema_version', 'id', 'title', 'speakers', 'entry', 'nodes', 'metadata')
foreach ($field in $dialogue.PSObject.Properties.Name) {
    if ($field -notin $allowedTopFields) {
        Add-CheckError "Unsupported Dialogue top-level field: $field"
    }
}

$types = @($dialogue.nodes | ForEach-Object { [string]$_.type })
foreach ($requiredType in @('line', 'choice', 'jump', 'end')) {
    if ($requiredType -notin $types) {
        Add-CheckError "Example Dialogue is missing node type '$requiredType'"
    }
}

$nodeIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($node in $dialogue.nodes) {
    if (-not $nodeIds.Add([string]$node.id)) {
        Add-CheckError "Duplicate Dialogue node ID: $($node.id)"
    }
}
if (-not $nodeIds.Contains([string]$dialogue.entry)) {
    Add-CheckError "Dialogue entry references a missing node"
}
foreach ($node in $dialogue.nodes) {
    $targets = [System.Collections.Generic.List[string]]::new()
    if ($node.type -eq 'line') {
        $targets.Add([string]$node.next)
    } elseif ($node.type -eq 'jump') {
        $targets.Add([string]$node.target)
    } elseif ($node.type -eq 'choice') {
        foreach ($choice in $node.choices) {
            $targets.Add([string]$choice.next)
        }
    }
    foreach ($target in $targets) {
        if (-not $nodeIds.Contains($target)) {
            Add-CheckError "Node '$($node.id)' references missing node '$target'"
        }
    }
}

$dialogueSources = @(
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\main') -Recurse -File
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'examples\phase2_project\dialogues') -Recurse -File
)
foreach ($source in $dialogueSources) {
    $text = Get-Content -LiteralPath $source.FullName -Raw -Encoding UTF8
    foreach ($forbidden in @('StartQuest(', '"start_quest"', '"quest_id"')) {
        if ($text.Contains($forbidden)) {
            Add-CheckError "Dialogue runtime contains forbidden Quest ownership marker '$forbidden': $($source.FullName)"
        }
    }
}

$schedulerPath = Join-Path $projectRoot 'src\main\java\darkgrey\rpg\network\MainThreadScheduler.java'
$scheduler = Get-Content -LiteralPath $schedulerPath -Raw -Encoding UTF8
foreach ($required in @('ConcurrentLinkedQueue', 'ServerTickEvent', 'ClientTickEvent')) {
    if (-not $scheduler.Contains($required)) {
        Add-CheckError "Main-thread scheduler is missing '$required'"
    }
}

$sessionPath = Join-Path $projectRoot 'src\main\java\darkgrey\rpg\dialogue\runtime\DialogueSessionManager.java'
$session = Get-Content -LiteralPath $sessionPath -Raw -Encoding UTF8
foreach ($required in @('sessionId', 'choiceIndex', 'DialogueFlowEngine', 'S2CDialogueClose')) {
    if (-not $session.Contains($required)) {
        Add-CheckError "Server Dialogue session validation is missing '$required'"
    }
}

$studioEditor = Get-Content -LiteralPath (
    Join-Path $projectRoot 'studio\scripts\DialogueEditorWindow.gd'
) -Raw -Encoding UTF8
foreach ($required in @(
    'item_reordered',
    '_duplicate_node',
    '_delete_node',
    '_undo',
    '_redo',
    '_find_next',
    '_replace_all',
    'Speaker'
)) {
    if (-not $studioEditor.Contains($required)) {
        Add-CheckError "Studio Dialogue editor is missing '$required'"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PHASE2_STATIC_VERIFY=PASS'
Write-Host 'DIALOGUE_NODE_TYPES=LINE,CHOICE,JUMP,END'
Write-Host 'DIALOGUE_CONNECTIONS=PASS'
Write-Host 'DIALOGUE_QUEST_SCOPE_GUARD=PASS'
Write-Host 'NETWORK_MAIN_THREAD_GUARD=PASS'
Write-Host 'SERVER_SESSION_AUTHORITY=PASS'
Write-Host 'STUDIO_DIALOGUE_UX_MARKERS=PASS'
