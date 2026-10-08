[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
& (Join-Path $PSScriptRoot 'record-0401-dynamic-evidence.ps1')
& (Join-Path $PSScriptRoot 'record-0401-evidence.ps1')
& (Join-Path $PSScriptRoot 'record-0401-native-evidence.ps1')
$directory=Join-Path $repo 'PLAN/0.4.0.1'
function Json([string]$Name) { Get-Content -LiteralPath (Join-Path $directory ('evidence/'+$Name)) -Raw | ConvertFrom-Json }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
$source=Json 'compiled-inputs.json'
$tests=Json 'tests.json'
$artifacts=Json 'artifacts.json'
$promotion=Json '0401-authority-promotion-receipt.json'
$program=Json '0401-authority-program-file-provenance.json'
$destination=Join-Path $repo 'dist/DarkGreyRPGStudio'
$candidate=Join-Path $repo '.tooling/0401/Studio'
foreach($file in $program) {
    foreach($root in @($destination,$candidate)) {
        $path=Join-Path $root $file.path
        if((Get-Item -LiteralPath $path).Length -ne $file.size -or (Hash $path) -ne $file.sha256){throw "Program differs: $($file.path)"}
    }
}
# Runtime-only follow-up: original promotion inventory protects that update, not subsequent user edits.
# Do not rewrite or require personal Data to remain frozen after the user resumes Studio.
if(!$promotion.data_fingerprints_equal -or !$promotion.all_program_files_match_validated_candidate){throw 'Original promotion protection receipt failed'}
$exe=Get-Item -LiteralPath (Join-Path $destination 'DarkGreyRPGStudio.exe')
if($exe.VersionInfo.ProductVersion -ne '0.4.0.1' -or (Hash $exe.FullName) -ne 'BC82D7805D0D0C91930814CBABFA075CFA421D76F509B892523BD09837CA6E6A'){throw 'Authority EXE is stale'}
if(Test-Path -LiteralPath 'artifacts/DGR0.4.0.1'){throw 'Unrequested finished-product directory exists'}
$matrix=@(Get-Content -LiteralPath (Join-Path $directory 'Acceptance.md') | Where-Object {$_ -match '^\| [ABTVD][0-9]{2} \|'} | ForEach-Object {
    $columns=$_.Split('|').Trim()
    [pscustomobject]@{id=$columns[1];status=$columns[2];evidence_levels=@($columns[3].Split('/'));result=$columns[4]}
})
$expected=@(@(1..8 | ForEach-Object {'A{0:00}' -f $_}); @(1..11 | ForEach-Object {'B{0:00}' -f $_}); @(1..6 | ForEach-Object {'T{0:00}' -f $_}); @(1..9 | ForEach-Object {'V{0:00}' -f $_}); @(1..8 | ForEach-Object {'D{0:00}' -f $_}))
if($matrix.Count -ne 42 -or @($matrix.id | Sort-Object -Unique).Count -ne 42 -or @(Compare-Object $expected $matrix.id).Count){throw 'Acceptance matrix IDs differ'}
$counts=[ordered]@{}
foreach($group in ($matrix | Group-Object status)){ $counts[$group.Name]=$group.Count }
$summary=[pscustomobject]@{
 version='0.4.0.1';recorded_at=[DateTimeOffset]::Now.ToString('o');matrix_count=$matrix.Count;status_counts=$counts;matrix=$matrix
 evidence=[pscustomobject]@{source='compiled-inputs.json';dependencies='dependencies.json';tests='tests.json';native='native-summary.json';raw_native='native-raw-evidence.json';promotion='0401-authority-promotion-receipt.json';runtime_candidate_delta='dynamic-runtime-delta.json';dynamic_capacity='dynamic-capacity.json';historical='pre-dynamic/'}
 human_confirmation_received=$false;all_mandatory_acceptance_passed=$false
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'evidence/verification-summary.json')
$delivery=[pscustomobject]@{
 version='0.4.0.1';recorded_at=[DateTimeOffset]::Now.ToString('o')
 baseline_commit='be0b180008fe58f2f1a6d1724a5a06d9884e4be8';branch=(& git branch --show-current);head=(& git rev-parse HEAD)
 implementation_source=[pscustomobject]@{type='Baseline plus physical workspace manifest including newly added/untracked files';committed=$false;manifest='evidence/compiled-inputs.json';fingerprint=$source.fingerprint;production_fingerprint=$source.production_source.fingerprint;test_fingerprint=$source.test_source.fingerprint;build_and_verification_fingerprint=$source.build_and_verification_inputs.fingerprint;input_files=$source.files.Count;hash_basis=$source.hash_basis;documentation='Implementation.md / Acceptance.md; kept separate from compiled production fingerprint'}
 issue_status=[pscustomobject]@{R1='IMPLEMENTED_AND_VERIFIED_REAL_MAPSTORAGE';R2=[pscustomobject]@{baseline_reproduction='Real isolated bad files became usable empty state and were rewritten after forced dirty';protection='PASS real loader/cache/get/write/save and integrated/dedicated representative';formal_user_world_incident_proven=$false};R3='CURRENT_ASSERTIONS_EXECUTED_10_TASK_6_STORY';R4='PARTIAL_FINITE_ACCEPTANCE_RECORDED'}
 storage_contract=[pscustomobject]@{default_runtime_capacity=4096;capacity_step=512;grow_at_percent=75;shrink_at_next_lower_capacity_percent=50;shrink_delay_seconds=300;maintenance_interval_seconds=5;final_record_limit=$null;extra_byte_budget=$null;capacity_and_timer_persisted=$false;unit='Distinct world entity UUID records';directory_network_limit=512;maximum_groups_per_record=32;maximum_local_id_ascii_length=63;measured_maximum_fields_fixture=[pscustomobject]@{records=8192;raw_nbt_bytes=35299420;compressed_bytes=240647;meaning='Measured roundtrip fixture, not an admission ceiling'};operation='Adjust after successful final net count under existing dual locks; failures and no-ops preserve state';compaction='Ordered metadata and both NPC indexes; no revision/dirty/serialization change';session_failure='Quarantine affected file and dependent execution; no empty success or write-back';protocol_changed=$false;schemas=@{graph=3;package=3;nominator=4;session_world=7}}
 tests=$tests
 native_matrix=[pscustomobject]@{document='Acceptance.md';summary='evidence/native-summary.json';verification='evidence/verification-summary.json';counts=$counts;normal_exports_and_controlled_filter_are_separate=$true;older_native_candidate_class_identity='evidence/pre-dynamic/final-runtime-delta.json';dynamic_runtime_delta='evidence/dynamic-runtime-delta.json';dynamic_forge='evidence/dynamic-capacity.json'}
 acceptance_exceptions=@($matrix | Where-Object status -ne 'PASS')
 artifacts=$artifacts
 additional_dev_jar=[pscustomobject]@{path='build/libs/darkgrey_rpg-0.4.0.1-dev.jar';size=(Get-Item -LiteralPath 'build/libs/darkgrey_rpg-0.4.0.1-dev.jar').Length;sha256=(Hash 'build/libs/darkgrey_rpg-0.4.0.1-dev.jar');scope='MCP development artifact; actual locked CNPC startup remains blocked'}
 authoritative_studio=[pscustomobject]@{path=$exe.FullName;product_version=$exe.VersionInfo.ProductVersion;size=$exe.Length;sha256=(Hash $exe.FullName);program_files_checked=$program.Count;candidate_program_match=$true;post_acceptance_readback=[DateTimeOffset]::Now.ToString('o')}
 data_preservation=[pscustomobject]@{user_confirmed_saved_closed=$true;target_exit_evidence='evidence/0401-authority-exit-before-promotion.json';deployment_only_replaced_program_files=$true;files_at_original_promotion=$promotion.data_files;bytes_at_original_promotion=$promotion.data_bytes;path_size_sha256_equal_at_original_promotion=$promotion.data_fingerprints_equal;promotion_time=$promotion.completed_at;followup_scope='Runtime-only; Studio program and personal Data not written or launched';raw_inventories_local_only=$true;fault_injection_root='.tooling/0401'}
 implementation_status='COMPLETE_R1_R2_R3';acceptance_status='INCOMPLETE_H_DEV_CNPC_AND_NATIVE_REQUEST_STATE_COMBINATIONS';delivery_status='RUNTIME_DYNAMIC_CAPACITY_BUILT_STUDIO_PRIOR_DELIVERY_UNCHANGED'
 user_accepted=$false;source_upload=$false;release=[pscustomobject]@{authorized=$false;finished_product_created=$false;pushed=$false;tag_created_or_moved=$false;github_release_created_or_modified=$false}
 rollback='Restore matched program plus world/DGR data backup. A >512-record .1 table is not guaranteed readable by .0; a >4096-record dynamic table is not guaranteed readable by the earlier fixed-capacity .1 reader. Do not downgrade JAR alone or truncate bindings.'
}
$delivery | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $directory 'DELIVERY.json')
[pscustomobject]@{matrix=$matrix.Count;counts=$counts;authoritative_version=$exe.VersionInfo.ProductVersion;authoritative_bytes=$exe.Length;authoritative_sha256=(Hash $exe.FullName);program_files=$program.Count;studio_unchanged_in_followup=$true;production_source_fingerprint=$source.production_source.fingerprint;implementation_status=$delivery.implementation_status;acceptance_status=$delivery.acceptance_status;delivery_status=$delivery.delivery_status} | ConvertTo-Json -Depth 5
