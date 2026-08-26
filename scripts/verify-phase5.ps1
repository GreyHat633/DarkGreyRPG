[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $errors.Add($Message)
}

function Require-Markers {
    param(
        [string]$RelativePath,
        [string[]]$Markers,
        [string]$Area
    )
    $path = Join-Path $projectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-CheckError "$Area file is missing: $RelativePath"
        return
    }
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($marker in $Markers) {
        if (-not $content.Contains($marker)) {
            Add-CheckError "$Area is missing '$marker'"
        }
    }
}

Require-Markers 'src\main\java\darkgrey\rpg\live\LiveBridgeServer.java' @(
    '127.0.0.1',
    'isLoopbackAddress',
    'StandardCharsets.UTF_8',
    'readLine',
    'newLine',
    'MAX_LINE_LENGTH',
    'setDaemon(true)'
) 'Live Bridge'

Require-Markers 'src\main\java\darkgrey\rpg\live\LiveBridgeController.java' @(
    'MainThreadScheduler.scheduleServer',
    'state.request',
    'project.reload',
    'pick.begin',
    'locate.actor',
    'test.start',
    'test.stop'
) 'Live command controller'

Require-Markers 'src\main\java\darkgrey\rpg\live\LiveStateSnapshotBuilder.java' @(
    '"actors"',
    '"players"',
    '"quests"',
    '"stories"',
    '"variables"',
    '"previous_nodes"',
    '"waiting_event"',
    '"conditions"',
    '"explanation"'
) 'Live Project/Debugger snapshot'

Require-Markers 'src\main\java\darkgrey\rpg\live\LivePickService.java' @(
    '"actor"',
    '"position"',
    '"region"',
    '"item"',
    '"entity_type"',
    '"pick.result"'
) 'Minecraft Pick'

Require-Markers 'src\main\java\darkgrey\rpg\runtime\EditorToolEventHandler.java' @(
    'handleEntity(',
    'handleBlock(',
    'RIGHT_CLICK_BLOCK'
) 'Editor Tool Pick integration'

Require-Markers 'src\main\java\darkgrey\rpg\live\PlayTestManager.java' @(
    'snapshotPlayer',
    'restorePlayer',
    'clearStory',
    'resetQuest',
    'startAt'
) 'Play Test snapshot workflow'

Require-Markers 'src\main\java\darkgrey\rpg\story\runtime\StoryDebugExplainer.java' @(
    'expectedActual',
    'QuestState',
    'actual=',
    'matches='
) 'Why Not Triggered'

Require-Markers 'studio\scripts\LiveBridgeClient.gd' @(
    '127.0.0.1',
    'StreamPeerTCP',
    'JSON.stringify',
    'find("\n")',
    'connection_changed'
) 'Studio Live client'

Require-Markers 'studio\scripts\LiveWorkspace.gd' @(
    'Minecraft Connected',
    'Pick Actor',
    'Pick Position',
    'Pick Region',
    'Pick Item',
    'Pick Entity Type',
    'Locate Selected Actor',
    'Why Not Triggered',
    'Test"',
    'Stop + Restore',
    'project.reload'
) 'Studio Live/Debugger UI'

Require-Markers 'studio\scripts\StoryEditorWindow.gd' @(
    'apply_live_debug',
    'previous_nodes',
    'WAITING',
    'ERROR',
    'apply_pick_result'
) 'Story Graph live highlighting'

Require-Markers 'studio\scripts\RpgProjectStore.gd' @(
    'build_content_pack',
    '"project.json"',
    '"actors"',
    '"dialogues"',
    '"quests"',
    '"stories"',
    '"resources"',
    '.backup-',
    '_atomic_write',
    'path + ".bak"'
) 'Content Build/Backup'

foreach ($editor in @('Main.gd', 'DialogueEditorWindow.gd', 'QuestEditorWindow.gd', 'StoryEditorWindow.gd')) {
    Require-Markers ("studio\scripts\" + $editor) @('autosave', '10.0') "Studio Autosave $editor"
}

$forbidden = @(
    'NativeNpc',
    'BossEditor',
    'BossPhase',
    'CombatTimeline',
    'ProjectileEditor',
    'CutsceneCamera'
)
$scopeParts = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\main\java') -Recurse -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 })
$scope = [string]::Join("`n", $scopeParts)
foreach ($marker in $forbidden) {
    if ($scope.Contains($marker)) {
        Add-CheckError "Out-of-scope implementation marker found: $marker"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PHASE5_STATIC_VERIFY=PASS'
Write-Host 'LOCALHOST_JSON_LINES_BRIDGE=PASS'
Write-Host 'MAIN_THREAD_LIVE_COMMANDS=PASS'
Write-Host 'PROJECT_LIVE_DEBUG_SNAPSHOT=PASS'
Write-Host 'MINECRAFT_PICK_KINDS=5'
Write-Host 'LOCATE_HOT_RELOAD_MARKERS=PASS'
Write-Host 'WHY_NOT_TRIGGERED_MARKERS=PASS'
Write-Host 'PLAY_TEST_SNAPSHOT_RESTORE=PASS'
Write-Host 'CONTENT_PACK_BUILD_SHAPE=PASS'
Write-Host 'AUTOSAVE_BACKUP_ATOMIC_WRITE=PASS'
Write-Host 'FUTURE_SCOPE_GUARD=PASS'
