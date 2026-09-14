[CmdletBinding()]
param(
    # P1 removes standalone retired nodes. Existing Start trigger configuration is frozen.
    # The default is the final gate, never the permissive baseline inventory.
    [ValidateSet('P0', 'P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'Final')][string]$Phase = 'Final',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Set-StrictMode -Version Latest
$baseline = '82c6443238e41ce820f076e09d65d3cb26bb2236'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-FrozenCategory([string]$Path) {
    switch -Regex ($Path.Replace('\', '/')) {
        '^(src/main/java/darkgrey/rpg/(identity|item/identity)/|studio/src/DarkGreyRPG\.Studio\.Core/Identity/)' { return 'IDENTITY' }
        '^src/main/java/darkgrey/rpg/(nominator/|item/ItemNominator\.java|client/(Nominator[^/]*\.java|gui/(Nominator[^/]*|GuiNominator[^/]*)\.java)|network/(NominatorNetwork\.java|message/nominator/))' { return 'NOMINATOR' }
        '^src/main/java/darkgrey/rpg/client/gui/(UtilityWindow[^/]*|SmoothScroll)\.java$' { return 'UTILITY_WINDOW_FOUNDATION' }
        '^studio/src/DarkGreyRPG\.Studio\.Core/Graphs/(Definitions/AggregatePortProjection|Resources/CanonicalAggregateNodeFactory)\.cs$' { return 'AGGREGATE_BOUNDARY' }
        '^(studio/src/DarkGreyRPG\.Studio\.Core/Graphs/Definitions/StoryStartSchema\.cs|src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStory(StartConfiguration|TriggerIndex|RegionEntryTracker)\.java)$' { return 'START_CONFIGURATION' }
        '^scripts/verify-0324-freeze\.ps1$' { return 'HISTORICAL_FREEZE_GUARD' }
    }
    return $null
}

function Get-TaskDefinitionViolations([string]$Source) {
    # Examine complete registry entries, including their ports, not just the first line.
    $entries = [regex]::Matches($Source, '(?ms)^\s*Node\([^\r\n]*GraphScope\.Task\b.*?(?=^\s*Node\(|^\s*\];)')
    if ($entries.Count -eq 0) { throw 'No Task definitions found; registry structure requires review.' }
    foreach ($entry in $entries) {
        if ($entry.Value -match 'GraphInterfaceKind\.Flow|\bflowOnly\b|kinds:\s*all\b|"flow_(in|out)"' -or
            $entry.Value -notmatch 'kinds:\s*(logicOnly\b|\[GraphInterfaceKind\.Logic\])') {
            $entry.Value.Trim()
        }
    }
}

function Get-NetworkRegistrations([string]$Source) {
    foreach ($match in [regex]::Matches($Source, '(?s)\bregisterMessage\s*\([^;]+?\)\s*;|\bnewSimpleChannel\s*\([^;]+?\)')) {
        $match.Value -replace '\s', ''
    }
}

if ($SelfTest) {
    $cases = @{
        'src/main/java/darkgrey/rpg/identity/NewIdentity.java' = 'IDENTITY'
        'studio/src/DarkGreyRPG.Studio.Core/Identity/ResourceRenameMap.cs' = 'IDENTITY'
        'src/main/java/darkgrey/rpg/nominator/container/ContainerNominatorInventory.java' = 'NOMINATOR'
        'src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java' = 'NOMINATOR'
        'src/main/java/darkgrey/rpg/client/gui/UtilityWindowGeometry.java' = 'UTILITY_WINDOW_FOUNDATION'
        'scripts/verify-0324-freeze.ps1' = 'HISTORICAL_FREEZE_GUARD'
        'studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/StoryStartSchema.cs' = 'START_CONFIGURATION'
        'src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryStartConfiguration.java' = 'START_CONFIGURATION'
        'src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryTriggerIndex.java' = 'START_CONFIGURATION'
        'src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryRegionEntryTracker.java' = 'START_CONFIGURATION'
    }
    foreach ($case in $cases.GetEnumerator()) {
        if ((Get-FrozenCategory $case.Key) -ne $case.Value) { throw "Missed protected path: $($case.Key)" }
    }
    if ($null -ne (Get-FrozenCategory 'src/main/java/darkgrey/rpg/task/runtime/CanonicalTaskRuntime.java')) {
        throw 'Task feature implementation must remain in scope.'
    }
    $valid = "Node(`"objective`", GraphScope.Task, kinds: logicOnly,`n    ports: [Out(`"done`", `"Done`", GraphInterfaceKind.Logic, 0)]),`n];"
    if (@(Get-TaskDefinitionViolations $valid).Count -ne 0) { throw 'Rejected pure Logic definition.' }
    foreach ($invalid in @($valid.Replace('GraphInterfaceKind.Logic', 'GraphInterfaceKind.Flow'), $valid.Replace('logicOnly', 'all'))) {
        if (@(Get-TaskDefinitionViolations $invalid).Count -ne 1) { throw 'Failed to catch Task Flow regression.' }
    }
    $route = 'CHANNEL.registerMessage(Handler.class, Packet.class, 7, Side.CLIENT);'
    $reformatted = "CHANNEL.registerMessage(`nHandler.class, Packet.class,`n7, Side.CLIENT);"
    if ((Get-NetworkRegistrations $route) -ne (Get-NetworkRegistrations $reformatted)) { throw 'Network comparison is whitespace-sensitive.' }
    if ((Get-NetworkRegistrations $route) -eq (Get-NetworkRegistrations ($route.Replace('7,', '8,')))) { throw 'Missed discriminator change.' }
    'SCOPE_GUARD_SELF_TEST=PASS'
    return
}

Push-Location $repositoryRoot
try {
    git cat-file -e "$baseline^{commit}"
    if ($LASTEXITCODE -ne 0) { throw 'Exact 0.3.2.4 baseline is unavailable.' }
    git merge-base --is-ancestor $baseline HEAD
    if ($LASTEXITCODE -ne 0) { throw 'HEAD is not descended from the exact 0.3.2.4 baseline.' }
    $changed = @(git -c core.quotepath=false diff --name-only --no-renames $baseline -- src studio/src schema scripts)
    if ($LASTEXITCODE -ne 0) { throw 'Baseline comparison failed.' }
    $untracked = @(git -c core.quotepath=false ls-files --others --exclude-standard -- src studio/src schema scripts)
    if ($LASTEXITCODE -ne 0) { throw 'Untracked-file inspection failed.' }
    $changed = @(@($changed) + @($untracked) | Sort-Object -Unique)
    $failures = [Collections.Generic.List[string]]::new()
    "BASELINE=$baseline"
    "PHASE=$Phase"
    # P2 adds only one case label to the existing Item/Group reference and rename rules.
    $identityExtensions = @()
    if ($Phase -in @('P2','P3','P4','P5','P6','Final')) {
        foreach ($path in @('studio/src/DarkGreyRPG.Studio.Core/Identity/ResourceRenameMap.cs','studio/src/DarkGreyRPG.Studio.Core/Identity/NamespaceProjectValidator.cs')) {
            $before = (git show "${baseline}:$path") -join "`n"
            $after = (Get-Content -LiteralPath $path -Raw -Encoding UTF8) -replace 'case "submit_item":\s*case "collect_item":', 'case "collect_item":'
            if ($Phase -in @('P3','P4','P5','P6','Final')) {
                # Exact Task feature hooks only. Existing identity algorithms must still match baseline.
                $after = $after.Replace('CanonicalTaskRewardReferences.Validate(node, graph.ResourceKind, declared);', '')
                $after = $after.Replace('CanonicalTaskRewardReferences.Rewrite(node, source.ResourceKind, Resolve);', '')
                $after = $after.Replace(', TaskMetadata = source.TaskMetadata', '')
            }
            if (($after -replace '\s','') -ceq ($before -replace '\s','')) { $identityExtensions += $path }
        }
    }
    "NARROW_SUBMIT_ITEM_REFERENCE_EXTENSIONS=$($identityExtensions.Count)"
    foreach ($category in @('IDENTITY', 'NOMINATOR', 'UTILITY_WINDOW_FOUNDATION', 'AGGREGATE_BOUNDARY', 'HISTORICAL_FREEZE_GUARD', 'START_CONFIGURATION')) {
        $outside = @($changed | Where-Object { (Get-FrozenCategory $_) -eq $category -and $_ -notin $identityExtensions })
        "UNEXPECTED_${category}_DIFF=$($outside.Count)"
        foreach ($path in $outside) { $failures.Add("Frozen $category changed: $path") }
    }

    $networkFiles = @(git ls-tree -r --name-only $baseline -- src/main/java/darkgrey/rpg/network)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate baseline network.' }
    $networkChanges = 0
    foreach ($file in $networkFiles | Where-Object { $_ -match '\.java$' }) {
        $before = (git show "${baseline}:$file") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "Cannot read baseline network: $file" }
        $original = @(Get-NetworkRegistrations $before)
        if (!$original.Count) { continue }
        $current = if (Test-Path -LiteralPath $file) { @(Get-NetworkRegistrations (Get-Content -LiteralPath $file -Raw -Encoding UTF8)) } else { @() }
        foreach ($registration in $original) {
            if ($registration -cnotin $current) {
                $networkChanges++
                $failures.Add("Existing network route/channel changed: $file : $registration")
            }
        }
    }
    "UNEXPECTED_EXISTING_NETWORK_ROUTE_DIFF=$networkChanges"

    $productionRoots = @('src/main/java', 'studio/src/DarkGreyRPG.Studio.Core', 'studio/src/DarkGreyRPG.Studio', 'schema')
    $files = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard -- @productionRoots)
    if ($LASTEXITCODE -ne 0) { throw 'Production inventory failed.' }
    $files = @($files | Sort-Object -Unique | Where-Object { $_ -match '\.(cs|java|xaml|json)$' -and (Test-Path -LiteralPath $_ -PathType Leaf) })
    $legacyRuntime = [Collections.Generic.List[string]]::new()
    $legacyAuthoring = [Collections.Generic.List[string]]::new()
    $narrationRuntime = [Collections.Generic.List[string]]::new()
    $forbidden = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $source = Get-Content -LiteralPath $file -Raw -Encoding UTF8
        # Only node registration/execution contexts count. Start trigger_type is intentionally retained.
        $standaloneDefinition = $source -match 'Node\("(interact_actor|enter_region|enter_story)",\s*GraphScope\.StoryFlow'
        $standaloneExecution = $file -like '*/CanonicalStoryRuntime.java' -and
            $source -match '"(interact_actor|enter_region|enter_story)"\.equals\(type\)|CanonicalStoryWaitKind\.(ACTOR_INTERACT|INTERACT_ACTOR|ENTER_REGION)|CanonicalStoryStatus\.TRANSFERRED'
        if ($standaloneExecution) { $legacyRuntime.Add($file) }
        if ($standaloneDefinition) { $legacyAuthoring.Add($file) }
        if ($file.StartsWith('src/main/java/') -and $source -match '"narration"') { $narrationRuntime.Add($file) }
        if ($source -cmatch '\b(AssetID|ImageID|MusicID|BuffID|RewardID)\b') { $forbidden.Add($file) }
    }

    $registryPath = 'studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinitionRegistry.cs'
    $registryBefore = (git show "${baseline}:$registryPath") -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read baseline Start definition.' }
    $registryCurrent = Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8
    $startPattern = '(?s)Node\("start", GraphScope\.StoryFlow.*?(?=Node\("terminate")'
    $startBefore = [regex]::Match($registryBefore, $startPattern).Value -replace '\s', ''
    $startNow = [regex]::Match($registryCurrent, $startPattern).Value -replace '\s', ''
    if (!$startBefore -or $startBefore -cne $startNow) { $failures.Add('Existing Start registry configuration changed.') }
    "START_REGISTRY_UNCHANGED=$($startBefore -ceq $startNow)"
    $taskViolations = @(Get-TaskDefinitionViolations (Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8))
    # A changed Runtime loader still needs canonicalGraphResourceProbe to prove Logic-only loading.
    'TASK_FLOW_CHECK=static registry definitions including allowed kinds; Runtime requires graph probe'
    "TASK_FLOW_DEFINITION_VIOLATIONS=$($taskViolations.Count)"
    $flowPorts = 0
    foreach ($violation in $taskViolations) { $flowPorts += [regex]::Matches($violation, '(In|Out)\([^\r\n]*GraphInterfaceKind\.Flow').Count }
    "TASK_FLOW_PORT_COUNT=$flowPorts"
    if ($taskViolations.Count -gt 0) { $failures.Add('Task registry permits Flow; inspect registry entries.') }
    "FORBIDDEN_MEDIA_ID_TERMS=$($forbidden.Count)"
    foreach ($file in $forbidden) { $failures.Add("Forbidden user-facing ID term: $file") }

    $addedCompat = [Collections.Generic.List[string]]::new()
    foreach ($file in $changed | Where-Object { $_ -match '\.(cs|java|xaml|json)$' -and ($_ -like 'src/main/*' -or $_ -like 'studio/src/DarkGreyRPG.Studio.Core/*' -or $_ -like 'studio/src/DarkGreyRPG.Studio/*' -or $_ -like 'schema/*') }) {
        if ($file -in $untracked) {
            $added = Get-Content -LiteralPath $file
        } else {
            $diff = @(git diff --no-ext-diff --unified=0 $baseline -- $file)
            if ($LASTEXITCODE -ne 0) { throw "Cannot inspect additions: $file" }
            $added = @($diff | Where-Object { $_.StartsWith('+') -and -not $_.StartsWith('+++') } | ForEach-Object { $_.Substring(1) })
        }
        # A conservative tripwire. It is not a proof that no arbitrary fallback was added.
        if (@($added | Where-Object { $_ -match 'compatibilityOnly:\s*true|compatibilityMode:\s*true|[Cc]ompatibilityOnly\s*=\s*true|[Ll]egacyTrigger|legacy.*fallback|fallback.*legacy' }).Count -gt 0) {
            $addedCompat.Add($file)
        }
    }
    "UNEXPECTED_LEGACY_COMPAT_ADDITION=$($addedCompat.Count)"
    foreach ($file in $addedCompat) { $failures.Add("Review new legacy compatibility: $file") }

    "LEGACY_STANDALONE_NODE_EXECUTION_PATHS=$($legacyRuntime.Count)"
    "LEGACY_STANDALONE_NODE_DEFINITIONS=$($legacyAuthoring.Count)"
    "NARRATION_RUNTIME_PATHS=$($narrationRuntime.Count)"
    'RESIDUAL_COUNT_UNIT=node-context production files; load/migration/package rejection requires tests'
    if ($Phase -ne 'P0') {
        foreach ($file in @($legacyRuntime) + @($legacyAuthoring)) { $failures.Add("Retired standalone node residue: $file") }
    }
    if ($Phase -in @('P5', 'P6', 'Final')) {
        foreach ($file in $narrationRuntime) { $failures.Add("Narration Runtime residue: $file") }
    }
    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { "FAIL: $_" }
        'SCOPE_GUARD=FAIL'
        throw "$($failures.Count) scope violations."
    }
    if ($Phase -eq 'P0') {
        'PURGE_GATE=NOT_RUN (P0 inventory only)'
        'SCOPE_GUARD_P0=PASS'
    } else {
        "SCOPE_GUARD_${Phase}=PASS"
    }
} finally {
    Pop-Location
}
