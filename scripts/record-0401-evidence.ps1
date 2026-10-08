[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
$output = Join-Path $repo 'PLAN/0.4.0.1/evidence'
New-Item -ItemType Directory -Force -Path $output | Out-Null

function Fingerprint([string]$Path) {
    $file = Get-Item -LiteralPath $Path
    [pscustomobject]@{ path=[IO.Path]::GetRelativePath($repo, $file.FullName).Replace('\','/'); size=$file.Length; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}
function Manifest-Digest($Rows) {
    $canonical = ($Rows | ForEach-Object { $_.path + "`t" + $_.size + "`t" + $_.sha256 }) -join "`n"
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
}
function Read-Trx([string]$Path) {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $c = $trx.TestRun.ResultSummary.Counters
    $results = @($trx.TestRun.Results.UnitTestResult)
    $skips = @($results | Where-Object outcome -eq 'NotExecuted' | ForEach-Object {
        [pscustomobject]@{name=$_.testName;reason=$_.Output.ErrorInfo.Message}
    })
    [pscustomobject]@{ file=Fingerprint $Path; outcome=$trx.TestRun.ResultSummary.outcome; total=[int]$c.total; passed=[int]$c.passed; failed=[int]$c.failed; skipped=$skips; results_count=$results.Count; start=$trx.TestRun.Times.start; finish=$trx.TestRun.Times.finish }
}

# Physical workspace bytes, including newly added/untracked source. No Git-blob/CRLF mixing.
$inputs = @()
foreach($directory in @('src/main','src/test','studio/src','buildSrc','gradle','scripts')) {
    if(!(Test-Path -LiteralPath $directory -PathType Container)) { continue }
    $inputs += @(Get-ChildItem -LiteralPath $directory -Recurse -Force -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|\.gradle|build|node_modules)[\\/]'
    })
}
foreach($pattern in @('*.gradle.kts','gradle.properties','gradlew','gradlew.bat','*.lockfile','studio/*.ps1','studio/*.props','studio/*.json','studio/*.config')) {
    $inputs += @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue)
}
$rows = @($inputs | Sort-Object FullName -Unique | ForEach-Object { Fingerprint $_.FullName })
$digest = Manifest-Digest $rows
$productionRows = @($rows | Where-Object {
    $_.path.StartsWith('src/main/') -or ($_.path.StartsWith('studio/src/') -and $_.path -notmatch '\.Tests/')
})
$testRows = @($rows | Where-Object { $_.path.StartsWith('src/test/') -or $_.path -match '^studio/src/.*\.Tests/' })
$buildRows = @($rows | Where-Object { $_ -notin $productionRows -and $_ -notin $testRows })
$source = [pscustomobject]@{
    version='0.4.0.1'; baseline_commit='be0b180008fe58f2f1a6d1724a5a06d9884e4be8'
    branch=(& git branch --show-current); hash_basis='Physical workspace bytes; UTF-8 manifest path<TAB>size<TAB>uppercase SHA-256 joined by LF, no final LF.'
    fingerprint=$digest; files=$rows; includes_untracked_source=$true
    production_source=[pscustomobject]@{fingerprint=(Manifest-Digest $productionRows);files=$productionRows;meaning='Physical production source/resources present for the frozen final binaries; excludes subsequent verification-script edits.'}
    test_source=[pscustomobject]@{fingerprint=(Manifest-Digest $testRows);files=$testRows}
    build_and_verification_inputs=[pscustomobject]@{fingerprint=(Manifest-Digest $buildRows);files=$buildRows;meaning='Includes evidence/fixture scripts edited after compilation; these edits do not change production binaries.'}
    excludes='bin/obj/generated build output; personal state; documentation-only additions; isolated native worlds'
}
$source | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'compiled-inputs.json') -Encoding utf8

$dependencyFiles = @(Get-ChildItem -LiteralPath (Join-Path $repo 'libs') -File)
$dependencyFiles += @(Get-ChildItem -LiteralPath (Join-Path $repo 'studio/src') -Recurse -File -Filter project.assets.json)
$dependencyFiles += @(Get-ChildItem -LiteralPath (Join-Path $repo 'scripts') -File | Where-Object Name -match 'lock|dependencies')
$dependencyFiles += Get-Item -LiteralPath (Join-Path $repo 'studio/media-tools.lock.json')
$dependencyFiles += Get-Item -LiteralPath (Join-Path $repo 'dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.deps.json')
$dependencyFiles += Get-Item -LiteralPath (Join-Path $repo 'dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.runtimeconfig.json')
$dependencyRows = @($dependencyFiles | Sort-Object FullName -Unique | ForEach-Object {Fingerprint $_.FullName})
[pscustomobject]@{files=$dependencyRows;fingerprint=(Manifest-Digest $dependencyRows);meaning='Locked third-party inputs, actual NuGet restore graphs and delivered self-contained runtime dependency metadata; paths relative to repository.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'dependencies.json') -Encoding utf8

$commands = @(Get-Content '.tooling/0401/VerifiedDynamicFinal/commands.json' -Raw | ConvertFrom-Json)
$probeLog = Get-Content '.tooling/0401/VerifiedDynamicFinal/probes.log' -Raw
$probes = @($commands | Where-Object phase -eq 'probes' | ForEach-Object tasks | Where-Object { $_ -ne '--continue' })
foreach($probe in $probes) {
    if($probeLog -notmatch ('> Task :' + [regex]::Escape($probe) + '\b')) { throw "Missing final execution: $probe" }
}
if(@($commands | Where-Object exit_code -ne 0).Count) { throw 'Final Runtime verification failed' }
$commands | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'runtime-commands.json') -Encoding utf8
$core=Read-Trx '.tooling/0401/Tests/core-complete.trx'
$wpf=Read-Trx '.tooling/0401/Tests/wpf-complete.trx'
if($core.failed -or $wpf.failed -or $core.outcome -ne 'Completed' -or $wpf.outcome -ne 'Completed') { throw 'Final Studio test run is incomplete or failed' }
$tests=[pscustomobject]@{
    runtime=[pscustomobject]@{build_exit_code=0;probes_exit_code=0;probe_count=$probes.Count;probes=$probes;method_passes=@([regex]::Matches($probeLog,'BEHAVIOR ([A-Za-z0-9_]+)=PASS') | ForEach-Object {$_.Groups[1].Value});logs=@(Fingerprint '.tooling/0401/VerifiedDynamicFinal/build.log'; Fingerprint '.tooling/0401/VerifiedDynamicFinal/probes.log')}
    core=$core;wpf=$wpf
    studio_scope='Inherited .1 runs and delivered binaries; dynamic capacity follow-up changes Java only. Studio production and test source fingerprints must match pre-dynamic receipt.'
    historical_attempts='Baseline failures, interrupted WPF runs and superseded candidates remain local; they are excluded from final PASS counts.'
}
$previous=Get-Content -LiteralPath (Join-Path $output 'pre-dynamic/compiled-inputs.json') -Raw | ConvertFrom-Json
$previousStudio=@($previous.files | Where-Object {$_.path.StartsWith('studio/src/')})
$currentStudio=@($rows | Where-Object {$_.path.StartsWith('studio/src/')})
if(@(Compare-Object $previousStudio $currentStudio -Property path,size,sha256).Count){throw 'Studio inputs changed; inherited Studio tests cannot be reused'}
$tests | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'tests.json') -Encoding utf8

$artifactPaths=@('build/libs/darkgrey_rpg-0.4.0.1.jar','dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe','dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll','dist/DarkGreyRPGStudio/Program/DarkGreyRPG.Studio.Core.dll','dist/DarkGreyRPGStudio/Docs/StudioProgramFiles.json')
$artifacts=@($artifactPaths | ForEach-Object { $row=Fingerprint $_; $row | Add-Member -NotePropertyName product_version -NotePropertyValue (Get-Item -LiteralPath $_).VersionInfo.ProductVersion; $row })
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $repo $artifactPaths[0]))
try {
    $prohibited=@($archive.Entries | Where-Object { $_.FullName -match 'StorageServer0401Driver|StorageProbeSupport|StorageBaseline0401Probe|^net/minecraft/' })
    if($prohibited.Count) { throw 'Formal JAR contains acceptance driver or Minecraft replacements' }
    $entry=$archive.GetEntry('mcmod.info'); $reader=[IO.StreamReader]::new($entry.Open())
    try { $metadata=$reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if($metadata[0].version -ne '0.4.0.1') { throw 'Wrong Runtime metadata version' }
} finally { $archive.Dispose() }
$artifacts | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'artifacts.json') -Encoding utf8

# Preserve personal raw Data inventories outside the shareable evidence directory.
foreach($name in @('0401-authority-data-before-promotion-final.json','0401-authority-data-after-promotion.json')) {
    $from=Join-Path $output $name
    if(Test-Path -LiteralPath $from) { Move-Item -LiteralPath $from -Destination (Join-Path $repo ".tooling/0401/Logs/$name") }
}
[pscustomobject]@{compiled_input_files=$rows.Count;source_fingerprint=$digest;runtime_probes=$probes.Count;core_passed=$core.passed;wpf_passed=$wpf.passed;wpf_skipped=$wpf.skipped.Count} | ConvertTo-Json
