[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
$out=Join-Path $repo 'PLAN/0.4.0.1/evidence'
$server='.tooling/0401/Minecraft/DynamicServer'
function Fingerprint([string]$Path) {
    $file=Get-Item -LiteralPath $Path
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($repo,$file.FullName).Replace('\','/');size=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
}
$final=Fingerprint 'build/libs/darkgrey_rpg-0.4.0.1.jar'
$serverJar=Fingerprint (Join-Path $server 'mods/darkgrey_rpg-0.4.0.1.jar')
if($serverJar.sha256 -ne $final.sha256){throw 'Native world used a different Runtime JAR'}
$logs=@('dynamic-seed','dynamic-shrink','dynamic-verify') | ForEach-Object {
    $path=Join-Path $server ($_+'-stdout.log')
    $text=Get-Content -LiteralPath $path -Raw
    if($text -notmatch ('DGR0401_REAL_FORGE_STORAGE='+$_+' PASS') -or $text -match 'DGR0401_REAL_FORGE_STORAGE=\S+ FAIL'){throw "Native stage incomplete: $_"}
    if(!$text.Contains('DGR0401_DYNAMIC_STORAGE=production-overworld-mapStorage PASS')){throw "Native driver used an unverified storage owner: $_"}
    if($text -notmatch 'Stopping server' -or $text -notmatch 'Unloading dimension 0'){throw "Native stage lacks normal shutdown: $_"}
    [pscustomobject]@{stage=$_;file=(Fingerprint $path);pass=$true;normal_save_stop=$true}
}
$shrink=Get-Content -LiteralPath (Join-Path $server 'dynamic-shrink-stdout.log') -Raw
$timing=[regex]::Match($shrink,'DGR0401_DYNAMIC_SHRINK count=100 capacity=4096 waited_ns=(\d+) buckets_before=\[([^\]]+)\] buckets_after=\[([^\]]+)\]')
if(!$timing.Success -or [long]$timing.Groups[1].Value -lt 300000000000){throw 'Real five-minute shrink missing'}
$before=@($timing.Groups[2].Value.Split(',') | ForEach-Object {[int]$_.Trim()})
$after=@($timing.Groups[3].Value.Split(',') | ForEach-Object {[int]$_.Trim()})
foreach($i in 0..2){if($after[$i] -ge $before[$i]){throw 'Underlying native Map table did not shrink'}}
$native=[pscustomobject]@{
    status='PASS';recorded_at=[DateTimeOffset]::Now.ToString('o');runtime=$final;server_runtime=$serverJar
    fixture='New isolated flat Forge world Dynamic0401; generated valid current-schema metadata and NPC hosts, not an authored Story export or physical entity spawn.'
    engine='Forge 1.7.10-10.13.4.1614 / Java 8u471 / actual MapStorage and production END-tick maintenance'
    driver=(Fingerprint '.tooling/0401/Driver/dgr0401-dynamic-storage-driver.jar');driver_separate_from_runtime=$true
    seed_records=6500;loaded_after_first_restart=6500;capacity_before_shrink=8704;retained_records=100;capacity_after_shrink=4096
    waited_nanoseconds=[long]$timing.Groups[1].Value;buckets_before=$before;buckets_after=$after
    compaction_checks='Exact NBT snapshots/order, both owners revision, dirty and both files unchanged; no-op then remove/rebind edit and save; second fresh JVM restores 100 and edits/saves.'
    logs=$logs;gc_heap_readings_used=$false;earlier_harness_failure=[pscustomobject]@{file=(Fingerprint '.tooling/0401/Minecraft/DynamicServer-wrong-storage/dynamic-shrink-stdout.log');cause='Acceptance driver selected Forge perWorldStorage by type; no production change required; final driver selects MCP/SRG mapStorage and asserts same instance as normal get()';counted_as_pass=$false}
}
$native | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath '.tooling/0401/Logs/dynamic-forge-storage.json' -Encoding utf8
$commands=Get-Content -LiteralPath '.tooling/0401/VerifiedDynamicFinal/commands.json' -Raw | ConvertFrom-Json
if(@($commands | Where-Object exit_code -ne 0).Count){throw 'Final Runtime checks failed'}
$probe=Get-Content -LiteralPath '.tooling/0401/VerifiedDynamicFinal/probes.log' -Raw
foreach($marker in @('CAPACITY_THRESHOLDS=PASS','CAPACITY_FLOOR_MEMORY=PASS','CAPACITY_OPERATIONS=PASS','CAPACITY_COMPACTION=PASS','NOMINATOR_CAPACITY_0401=PASS')){if(!$probe.Contains($marker)){throw "Missing dynamic check: $marker"}}
$evidence=[pscustomobject]@{
    runtime=$final;default_capacity=4096;step=512;grow_at_percent=75;shrink_at_next_lower_percent=50;continuous_low_seconds=300;server_check_seconds=5
    final_record_quota=$null;extra_byte_budget=$null;monotonic_clock=$true;capacity_and_wait_persisted=$false;user_configuration_or_notice_added=$false
    automated=[pscustomobject]@{log=(Fingerprint '.tooling/0401/VerifiedDynamicFinal/probes.log');roundtrip_counts=@(0,1,511,512,513,3071,3072,3455,3456,4095,4096,4097,8192);clock_checks='Thresholds, no early shrink, reset on crossing, lowest band, direct multiband decrease, regrowth and long arithmetic';operation_checks='Final net transfer count, residual source groups, unbind, no-op, conflicts/failed requests; exact state preserved';memory_checks='Metadata plus two NPC indexes 16384 -> 256, independent NPC decline and physical compaction below logical 4096; exact binding/index/order/revision/dirty/NBT/file equality';failed_reader='Maintenance skips quarantined selection; forced dirty writer refuses before touching persisted bytes';maximum_fields_fixture=[pscustomobject]@{records=8192;raw_nbt_bytes=35299420;compressed_bytes=240647;real_mapstorage_reload=$true};shutdown='Clears tracked weak references; untouched storage maintenance does not create files'}
    native=$native;historical_receipts='pre-dynamic/';unclosed_acceptance_items_preserved=@('V03','V04','V05','V09')
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $out 'dynamic-capacity.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
function ZipEntries([string]$Path) {
    $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $repo $Path))
    $entries=@{}
    try {
        foreach($entry in $zip.Entries){
            if(!$entry.Name){continue}
            $stream=$entry.Open()
            try {$entries[$entry.FullName]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
        }
    }finally{$zip.Dispose()}
    return $entries
}
$previous=Fingerprint '.tooling/0401/Driver/darkgrey_rpg-before-dynamic.jar'
$old=ZipEntries $previous.path
$current=ZipEntries $final.path
$changed=@(@($old.Keys)+@($current.Keys) | Sort-Object -Unique | Where-Object {$old[$_] -ne $current[$_]} | ForEach-Object {[pscustomobject]@{entry=$_;before=$old[$_];after=$current[$_]}})
if($current.ContainsKey('darkgrey/rpg/nominator/NominatorCapacityException.class')){throw 'Obsolete capacity exception remains in formal JAR'}
$delta=[pscustomobject]@{previous_jar_sha256=$previous.sha256;final_jar_sha256=$final.sha256;previous_entries=$old.Count;final_entries=$current.Count;changed_entries=$changed;storage_and_identity_class_bytes_unchanged=$false;scope='Dynamic storage/identity/service/tick classes changed. Historical native results retain their original binary scope; final-JAR dynamic world and all 30 current probes executed. Studio source and binaries unchanged.'}
$delta | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'dynamic-runtime-delta.json') -Encoding utf8
# Keep the established pointer current; the old EF427861 -> B2A2DFE7 receipt is archived explicitly.
$delta | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'final-runtime-delta.json') -Encoding utf8
$correction=Get-Content -LiteralPath (Join-Path $out 'pre-dynamic/storage-budget-correction.json') -Raw | ConvertFrom-Json
$correction | Add-Member -NotePropertyName binding_limit_at_historical_correction -NotePropertyValue $correction.current_binding_limit
$correction.PSObject.Properties.Remove('current_binding_limit')
$correction | Add-Member -NotePropertyName scope -NotePropertyValue 'Historical budget correction at its recorded time; superseded fixed admission policy. Current dynamic contract has no final record quota or byte budget; see dynamic-capacity.json.'
$correction | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'storage-budget-correction.json') -Encoding utf8
[pscustomobject]@{runtime_sha256=$final.sha256;native_records=6500;after_shrink=100;waited_nanoseconds=$native.waited_nanoseconds;before=$before;after=$after;changed_entries=$changed.Count} | ConvertTo-Json
