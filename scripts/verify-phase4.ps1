[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $errors.Add($Message)
}

$exampleRoot = Join-Path $projectRoot 'examples\phase4_project'
$storyPath = Join-Path $exampleRoot 'stories\tavern_slime_request.json'
$story = Get-Content -LiteralPath $storyPath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($story.id -ne 'tavern_slime_request' -or $story.entry -ne 'interact_owner') {
    Add-CheckError 'Phase 4 acceptance Story identity or entry is invalid'
}
if (@($story.nodes).Count -ne 20 -or @($story.connections).Count -ne 20) {
    Add-CheckError 'Phase 4 acceptance Story must contain the reviewed 20-node, 20-connection graph'
}

$nodes = @{}
foreach ($node in $story.nodes) {
    if ($nodes.ContainsKey([string]$node.id)) {
        Add-CheckError "Duplicate Story node ID '$($node.id)'"
    }
    $nodes[[string]$node.id] = $node
}

$routes = @{}
foreach ($connection in $story.connections) {
    $key = '{0}:{1}' -f $connection.from, $connection.output
    if ($routes.ContainsKey($key)) {
        Add-CheckError "Duplicate Story output '$key'"
    }
    $routes[$key] = [string]$connection.to
}

$requiredRoutes = @{
    'interact_owner:next' = 'not_started'
    'not_started:true' = 'offer_dialogue'
    'offer_dialogue:accept' = 'start_quest'
    'started_message:next' = 'wait_completion'
    'wait_completion:next' = 'interact_return'
    'interact_return:next' = 'reward_claimed'
    'complete_dialogue:done' = 'give_xp'
    'give_xp:next' = 'give_emeralds'
    'give_emeralds:next' = 'mark_rewarded'
}
foreach ($route in $requiredRoutes.GetEnumerator()) {
    if (-not $routes.ContainsKey($route.Key) -or $routes[$route.Key] -ne $route.Value) {
        Add-CheckError "Acceptance Story route '$($route.Key)' must target '$($route.Value)'"
    }
}

$resourceFiles = @(
    'actors\tavern_owner.json',
    'dialogues\tavern_offer.json',
    'dialogues\tavern_complete.json',
    'quests\kill_10_slimes.json',
    'stories\tavern_slime_request.json'
)
foreach ($resourceFile in $resourceFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $exampleRoot $resourceFile) -PathType Leaf)) {
        Add-CheckError "Acceptance resource is missing: $resourceFile"
    }
}

$quest = Get-Content -LiteralPath (Join-Path $exampleRoot 'quests\kill_10_slimes.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
foreach ($forbiddenField in @('actor', 'actor_id', 'issuerNpc', 'issuer_actor', 'dialogue', 'story')) {
    if ($quest.PSObject.Properties.Name -contains $forbiddenField) {
        Add-CheckError "Quest contains forbidden ownership field '$forbiddenField'"
    }
}

$nodeTypes = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\StoryNodeType.java'
) -Raw -Encoding UTF8
$executors = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\runtime\BuiltinStoryExecutors.java'
) -Raw -Encoding UTF8
$requiredTypes = @(
    'INTERACT_ACTOR',
    'ENTER_REGION',
    'QUEST_COMPLETED',
    'PLAY_DIALOGUE',
    'START_QUEST',
    'COMPLETE_QUEST',
    'BRANCH',
    'SEQUENCE',
    'QUEST_STATE',
    'HAS_ITEM',
    'VARIABLE_COMPARE',
    'GIVE_ITEM',
    'GIVE_XP',
    'SEND_MESSAGE',
    'SET_VARIABLE',
    'END'
)
foreach ($requiredType in $requiredTypes) {
    if (-not $nodeTypes.Contains($requiredType)) {
        Add-CheckError "StoryNodeType is missing '$requiredType'"
    }
    if (-not $executors.Contains("registry.register(StoryNodeType.$requiredType")) {
        Add-CheckError "Node Executor Registry is missing '$requiredType'"
    }
}

$runtimeService = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\runtime\StoryRuntimeService.java'
) -Raw -Encoding UTF8
if ($runtimeService -match '\bswitch\s*\(' -or $executors -match '\bswitch\s*\(') {
    Add-CheckError 'Story runtime must use the executor registry rather than a giant switch'
}

$eventBus = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\runtime\StoryEventBus.java'
) -Raw -Encoding UTF8
$eventBridge = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\runtime\StoryEventBridge.java'
) -Raw -Encoding UTF8
$eventAdapter = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\story\runtime\StoryEventAdapter.java'
) -Raw -Encoding UTF8
foreach ($required in @('INTERACT_ACTOR', 'PLAYER_POSITION', 'QUEST_COMPLETED', 'DIALOGUE_RESULT')) {
    if (-not ($eventBus + $eventBridge + $eventAdapter + $executors).Contains($required)) {
        Add-CheckError "Story event integration is missing '$required'"
    }
}
if (-not $eventAdapter.Contains('event.setCanceled(true)')) {
    Add-CheckError 'Bound Story Actor interaction must suppress the CustomNPC+ interaction GUI path'
}

$store = Get-Content -LiteralPath (Join-Path $projectRoot 'studio\scripts\RpgProjectStore.gd') -Raw -Encoding UTF8
foreach ($required in @(
    'Missing Actor',
    'Missing Dialogue',
    'Missing Quest',
    'Unconnected Node',
    'No Exit',
    'Invalid Connection',
    'Duplicate Story ID',
    'Broken Resource Reference'
)) {
    if (-not $store.Contains($required)) {
        Add-CheckError "Studio Story Problems is missing '$required'"
    }
}

$editor = Get-Content -LiteralPath (Join-Path $projectRoot 'studio\scripts\StoryEditorWindow.gd') -Raw -Encoding UTF8
foreach ($required in @(
    'GraphEdit.new',
    'minimap_enabled',
    'connection_request',
    'disconnection_request',
    '_duplicate_selected',
    '_copy_selected',
    '_paste_node',
    '_undo',
    '_redo',
    'Search Add Node types',
    'Find node ID, type, or property',
    '_search_nodes',
    '_refresh_inspector',
    'item_activated',
    'scroll_offset'
)) {
    if (-not $editor.Contains($required)) {
        Add-CheckError "Studio Story editor is missing '$required'"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PHASE4_STATIC_VERIFY=PASS'
Write-Host 'ACCEPTANCE_STORY_GRAPH=20_NODES,20_CONNECTIONS'
Write-Host 'RESOURCE_INDEPENDENCE_GUARD=PASS'
Write-Host 'STORY_NODE_TYPES=16'
Write-Host 'EXECUTOR_REGISTRY_NO_GIANT_SWITCH=PASS'
Write-Host 'STORY_EVENT_INTEGRATION=PASS'
Write-Host 'CNPC_INTERACTION_GUI_SUPPRESSION=PASS'
Write-Host 'STUDIO_STORY_PROBLEMS=PASS'
Write-Host 'STUDIO_GRAPH_UX_MARKERS=PASS'
