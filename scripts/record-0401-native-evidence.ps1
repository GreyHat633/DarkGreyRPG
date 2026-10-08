[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
$out = Join-Path $repo 'PLAN/0.4.0.1/evidence'
function Fingerprint([string]$Path) {
    $file=Get-Item -LiteralPath $Path
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($repo,$file.FullName).Replace('\','/');size=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
}
function ReadJson([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
$inputs=@(Get-ChildItem -LiteralPath '.tooling/0401' -Filter '*.ps1' -File | ForEach-Object {Fingerprint $_.FullName})
$inputs+=@('.tooling/0400-minecraft/tools/Native.ps1','.tooling/0400-minecraft/tools/ReadNbt.ps1','.tooling/0400-minecraft/tools/Rcon.ps1' | ForEach-Object {Fingerprint $_})
[pscustomobject]@{meaning='Final local harness/helper bytes; earlier failed attempts remain evidence, not PASS. No worlds, secrets or third-party binaries are embedded.';files=$inputs} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'native-inputs.json')
$rawPaths=@(
 '.tooling/0401/Logs/baseline-storage.log',
 '.tooling/0401/Logs/baseline-probes.log',
 '.tooling/0401/Logs/studio-native-sort-undo-redo.json',
 '.tooling/0401/Logs/studio-native-cancellations.json',
 '.tooling/0401/Logs/studio-workspace-cancel.json',
 '.tooling/0401/Logs/studio-whole-group-sort.json',
 '.tooling/0401/Logs/studio-readonly-drag.json',
 '.tooling/0401/Logs/studio-resource-drag.json',
 '.tooling/0401/Logs/studio-resource-cold-reopen.json',
 '.tooling/0401/Logs/server-fault-preservation.json',
 '.tooling/0401/Logs/final-integrated-fault-preservation.json',
 '.tooling/0401/Logs/final-quarantine-gui-client.log',
 '.tooling/0401/Logs/filter-final-saved-state.json',
 '.tooling/0401/Logs/formal-cnpc-phase1.log',
 '.tooling/0401/Logs/formal-cnpc-phase2.log',
 '.tooling/0401/Logs/formal-cnpc-phase3.log',
 '.tooling/0401/Logs/normal-two-player-choice-line-state.json',
 '.tooling/0401/Logs/normal-two-player-after-restart-state.json',
 '.tooling/0401/Logs/normal-server-packages.json',
 '.tooling/0401/Minecraft/Server/fault-line-context-stdout.log',
 '.tooling/0401/Minecraft/Server/fault-line-context-fixed-stdout.log',
 '.tooling/0401/Minecraft/Server/final-capacity-restart-stdout.log',
 '.tooling/0401/Minecraft/Server/normal-two-player-stdout.log',
 '.tooling/0401/Minecraft/Server/normal-two-player-restart-stdout.log',
 '.tooling/0401/Minecraft/Dev/crash-reports/crash-2026-10-08_20.00.06-client.txt',
 '.tooling/0401/Fixtures/provenance.json',
 '.tooling/0401/Fixtures/RejectedProducer/provenance.json'
)
$rawPaths+=@('.tooling/0401/Minecraft/DynamicServer/dynamic-seed-stdout.log','.tooling/0401/Minecraft/DynamicServer/dynamic-shrink-stdout.log','.tooling/0401/Minecraft/DynamicServer/dynamic-verify-stdout.log','.tooling/0401/Logs/dynamic-forge-storage.json','.tooling/0401/Minecraft/DynamicServer-wrong-storage/dynamic-shrink-stdout.log')
$rawPaths+=@(
 'normal-a-reconnected.png','normal-b-reconnected.png',
 'normal-group-player-a-choice-final.png','normal-group-player-a-task.png',
 'final-package-gui-quarantine-feedback.png','readonly-after-drag-view.png',
 'resource-drag-undo-complete.png','resource-cold-reopen-verified.png'
) | ForEach-Object {'.tooling/0401/Logs/'+$_}
$raw=@($rawPaths | ForEach-Object {Fingerprint $_})
$raw | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'native-raw-evidence.json')
$packages=@(
 '.tooling/0401/Studio/Data/Exports/0400 补充验收.dgrs',
 '.tooling/0401/Studio/Data/Exports/故事组（1）.dgrs.g',
 '.tooling/0401/Studio/Data/Exports/0400 库存验收.dgrs',
 '.tooling/0401/Studio/Data/Exports/FullGroup/故事组（1）.dgrs.g'
) | ForEach-Object {Fingerprint $_}
$fault=ReadJson '.tooling/0401/Logs/final-integrated-fault-preservation.json'
$serverFault=ReadJson '.tooling/0401/Logs/server-fault-preservation.json'
$filter=ReadJson '.tooling/0401/Logs/filter-final-saved-state.json'
$readonly=ReadJson '.tooling/0401/Logs/studio-readonly-drag.json'
$resource=ReadJson '.tooling/0401/Logs/studio-resource-drag.json'
$reopen=ReadJson '.tooling/0401/Logs/studio-resource-cold-reopen.json'
$state=ReadJson '.tooling/0401/Logs/normal-two-player-after-restart-state.json'
if($fault.expected -ne $fault.after_stop_sha256 -or !$serverFault.equal){throw 'Fault file changed'}
if($filter.xp_total -ne 7 -or $filter.inventory.Count -ne 1 -or $filter.inventory[0].Damage -ne 4 -or $filter.inventory[0].Count -ne 2){throw 'Native filter result differs'}
if(!$readonly.package_and_project_unchanged_after_member -or !$readonly.package_and_project_unchanged_after_whole -or !$resource.one_undo_restored -or !$reopen.graph_hash_same){throw 'Native Studio result differs'}
$delta=ReadJson (Join-Path $out 'pre-dynamic/final-runtime-delta.json')
$dynamic=ReadJson (Join-Path $out 'dynamic-capacity.json')
$summary=[pscustomobject]@{
 final_jar_sha256=(Fingerprint 'build/libs/darkgrey_rpg-0.4.0.1.jar').sha256
 pre_dynamic_final_jar_sha256=$fault.jar
 earlier_native_jar_sha256=$delta.previous_jar_sha256
 earlier_native_scope='Historical .1 native runs: EF427861 -> pre-dynamic B2A2DFE7 differed only in ClientPackageManager.class. Dynamic capacity changes storage/identity/service classes, so historical native tests are not relabeled as new-JAR runs. Final dynamic JAR reruns full build/probes and isolated 6500-record save/restart/edit, real five-minute shrink, save and second restart; see dynamic-runtime-delta.json.'
 isolated_root='.tooling/0401'; engine='Minecraft 1.7.10 / Forge 10.13.4.1614 / Java 8u471; third-party CNPC JAR unchanged'
 driver='Separate isolated acceptance JAR, used only for storage save/tick checks; absent from formal Runtime JAR and normal multiplayer server.'
 storage_fault=[pscustomobject]@{sha256=$fault.expected;integrated_pre_dynamic_jar_preserved=$true;dedicated_preserved=$serverFault.equal;forced_dirty_test='Actual MapStorage.saveAllData; writer throws before mutating output/file';startup_failure_preserved_as_original=$true}
 historical_capacity=[pscustomobject]@{records=513;pre_dynamic_restart_revision_before=519;after_edit=521;continued_editable=$true;driver_used=$true}
 dynamic_capacity=$dynamic
 normal_ui_exports=$packages
 normal_two_player_group=[pscustomobject]@{package_sha256='ACF60FB192723F924CEF462952AD1407F4C7D12A64B0A6683A39319E8ACB73EE';pre_dynamic_final_jar_used=$true;players=@('9a49dc9a-37a7-3967-91b3-c55b08c3ca87','46cd6446-b347-3a16-81ac-bd17ab274408');first_a='CHOICE';first_b='LINE';a_after_choice_and_restart='TASK 0/2';b_after_restart='LINE first sentence';snapshot_session_instances=@($state.data.sessions.instances | Select-Object player_uuid,transport_id,current_node_id,status);reload_unchanged=$true;fresh_progression_fixture='Copy of owned Positive0401 terrain/binding, initial progression files preserved as .InitialSnapshot only in new Normal0401 world'}
 filter=[pscustomobject]@{source='Normal UI Inventory single export, then explicit controlled damage metadata mutation';fixture=ReadJson '.tooling/0401/Fixtures/provenance.json';xp_total=$filter.xp_total;remaining_inventory=$filter.inventory;normal_story_task_settled=@($filter.task_data.instances | Where-Object runtime_status -eq 'SETTLED' | Select-Object aggregate_placement_id,runtime_status,reward_states);auxiliary_debug_task='CANCELLED_BY_STORY_TERMINATION; not reported as a second completed task';repeat_interaction_and_reload_saved=$true}
 studio=[pscustomobject]@{candidate_exe_sha256='BC82D7805D0D0C91930814CBABFA075CFA421D76F509B892523BD09837CA6E6A';readonly_package_unchanged=$true;resource_placement=$resource.placement;single_undo_and_redo=$true;exact_actor_parameter=$resource.actor_parameter;cold_reopen=$reopen}
 dev_cnpc=[pscustomobject]@{status='BLOCKED';error='NoSuchFieldError field_78804_l in ModelPlaneRenderer.addPlane / ModelSkirtArmor; fixed SRG CNPC JAR fails in MCP GradleStart';third_party_patch_applied=$false;entered_world=$false}
 human_audio=[pscustomobject]@{status='NOT_RUN';human_confirmation_received=$false;logs_not_counted_as_hearing=$true}
 remaining_native=[pscustomobject]@{direct_live_bridge_fault_request='NOT_RUN';direct_readonly_diagnostic_fault_request='NOT_RUN';disabled_new_vs_existing_game_instances='NOT_RUN (automatic current probes passed)';zero_settlement_game_chain='NOT_RUN (automatic current probes passed)'}
 failed_harness_attempts='Retained: initial startup missing boundary (fixed), UIA unsupported expander Invoke, minimized black captures, invalid debug Task UID/long chat truncation, controlled fixture rejected producer, CNPC fixture ReturnStartPos/chunk-render setup issues; first dynamic driver selected Forge perWorldStorage by type instead of production mapStorage and correctly timed out. Fixed driver selects MCP/SRG field and asserts same instance as NominatorSavedData.get(). These attempts are excluded from PASS.'
 local_raw_evidence='native-raw-evidence.json fingerprints refer to local files; raw worlds/personal Data/third-party binaries are not embedded.'
}
$summary | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $out 'native-summary.json')
[pscustomobject]@{native_inputs=$inputs.Count;raw_files=$raw.Count;normal_packages=$packages.Count;native_fault_preserved=$true;human_audio='NOT_RUN';dev_cnpc='BLOCKED'} | ConvertTo-Json
