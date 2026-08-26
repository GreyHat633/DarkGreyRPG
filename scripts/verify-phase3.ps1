[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $errors.Add($Message)
}

$questDirectory = Join-Path $projectRoot 'examples\phase3_project\quests'
$quests = @(Get-ChildItem -LiteralPath $questDirectory -Filter '*.json' -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
})

$objectiveTypes = @($quests.objectives | ForEach-Object { $_ } | ForEach-Object { [string]$_.type })
foreach ($requiredType in @('kill_entity', 'collect_item', 'reach_location', 'interact_actor')) {
    if ($requiredType -notin $objectiveTypes) {
        Add-CheckError "Phase 3 examples are missing Objective type '$requiredType'"
    }
}

$groupModes = @($quests.objective_groups | ForEach-Object { $_ } | ForEach-Object { [string]$_.mode })
foreach ($requiredMode in @('ALL', 'ANY', 'SEQUENCE')) {
    if ($requiredMode -notin $groupModes) {
        Add-CheckError "Phase 3 examples are missing Objective Group mode '$requiredMode'"
    }
}

$allowedTopFields = @('schema_version', 'id', 'title', 'description', 'objectives', 'objective_groups', 'metadata')
foreach ($quest in $quests) {
    foreach ($field in $quest.PSObject.Properties.Name) {
        if ($field -notin $allowedTopFields) {
            Add-CheckError "Unsupported Quest top-level field '$field' in $($quest.id)"
        }
    }
    foreach ($forbidden in @('issuerNpc', 'issuer_actor', 'dialogue', 'story')) {
        if ($quest.PSObject.Properties.Name -contains $forbidden) {
            Add-CheckError "Quest '$($quest.id)' contains forbidden ownership field '$forbidden'"
        }
    }

    $objectiveIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($objective in $quest.objectives) {
        if (-not $objectiveIds.Add([string]$objective.id)) {
            Add-CheckError "Duplicate Objective ID '$($objective.id)' in $($quest.id)"
        }
    }
    $assigned = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($group in $quest.objective_groups) {
        foreach ($objectiveId in $group.objectives) {
            if (-not $objectiveIds.Contains([string]$objectiveId)) {
                Add-CheckError "Group '$($group.id)' references missing Objective '$objectiveId'"
            }
            if (-not $assigned.Add([string]$objectiveId)) {
                Add-CheckError "Objective '$objectiveId' is assigned more than once"
            }
        }
    }
    if ($assigned.Count -ne $objectiveIds.Count) {
        Add-CheckError "Every Objective in '$($quest.id)' must belong to exactly one group"
    }
}

$killQuest = $quests | Where-Object { $_.id -eq 'kill_10_slimes' }
if ($null -eq $killQuest -or $killQuest.objectives[0].type -ne 'kill_entity' -or
    [int]$killQuest.objectives[0].required -ne 10 -or $killQuest.objectives[0].entity -ne 'Slime') {
    Add-CheckError 'Kill Slime x10 acceptance Quest is invalid'
}

$adapter = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\quest\runtime\QuestEventAdapter.java'
) -Raw -Encoding UTF8
foreach ($required in @('LivingDeathEvent', 'EntityItemPickupEvent', 'PlayerTickEvent', 'EntityInteractEvent')) {
    if (-not $adapter.Contains($required)) {
        Add-CheckError "Unified Quest event adapter is missing '$required'"
    }
}

$playerData = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\quest\runtime\PlayerQuestData.java'
) -Raw -Encoding UTF8
foreach ($required in @('WorldSavedData', 'getUniqueID', 'readFromNBT', 'writeToNBT', 'markDirty')) {
    if (-not $playerData.Contains($required)) {
        Add-CheckError "Persistent per-player Quest data is missing '$required'"
    }
}

$journal = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\client\gui\GuiQuestJournal.java'
) -Raw -Encoding UTF8
foreach ($required in @('QuestStatus.ACTIVE', 'QuestStatus.COMPLETED', 'handleMouseInput', 'getObjectiveLines')) {
    if (-not $journal.Contains($required)) {
        Add-CheckError "Quest Journal is missing '$required'"
    }
}

$network = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\network\DialogueNetwork.java'
) -Raw -Encoding UTF8
foreach ($required in @('S2CDialogueFrame', 'S2CDialogueClose', 'S2CQuestJournal', 'Side.CLIENT')) {
    if (-not $network.Contains($required)) {
        Add-CheckError "Common network discriminator registration is missing '$required'"
    }
}
foreach ($required in @('C2SQuestJournalRequest', 'Side.SERVER')) {
    if (-not $network.Contains($required)) {
        Add-CheckError "Player Journal request registration is missing '$required'"
    }
}

$journalKey = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\main\java\darkgrey\rpg\client\ClientQuestKeyHandler.java'
) -Raw -Encoding UTF8
foreach ($required in @('Keyboard.KEY_J', 'sendToServer', 'C2SQuestJournalRequest')) {
    if (-not $journalKey.Contains($required)) {
        Add-CheckError "Normal-player Journal key path is missing '$required'"
    }
}

$studioEditor = Get-Content -LiteralPath (
    Join-Path $projectRoot 'studio\scripts\QuestEditorWindow.gd'
) -Raw -Encoding UTF8
foreach ($required in @(
    'item_reordered',
    '_duplicate_objective',
    '_delete_objective',
    '_undo',
    '_redo',
    'KillEntity',
    'CollectItem',
    'ReachLocation',
    'InteractActor'
)) {
    if (-not $studioEditor.Contains($required)) {
        Add-CheckError "Studio Quest editor is missing '$required'"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PHASE3_STATIC_VERIFY=PASS'
Write-Host 'QUEST_OBJECTIVE_TYPES=KILL_ENTITY,COLLECT_ITEM,REACH_LOCATION,INTERACT_ACTOR'
Write-Host 'QUEST_GROUP_MODES=ALL,ANY,SEQUENCE'
Write-Host 'QUEST_SCOPE_GUARD=PASS'
Write-Host 'UNIFIED_EVENT_ADAPTER=PASS'
Write-Host 'PLAYER_UUID_PERSISTENCE_MARKERS=PASS'
Write-Host 'QUEST_JOURNAL_MARKERS=PASS'
Write-Host 'COMMON_S2C_REGISTRATION=PASS'
Write-Host 'PLAYER_JOURNAL_KEY_REQUEST=PASS'
Write-Host 'STUDIO_QUEST_UX_MARKERS=PASS'
