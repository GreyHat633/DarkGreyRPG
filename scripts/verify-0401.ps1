[CmdletBinding()]
param([string]$OutputDirectory = '.tooling/0401/Final', [switch]$Offline)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
# Current contracts only; the historical pre-fix reproduction is deliberately excluded.
$probes = @(
    'nominatorCapacity0401Probe', 'sessionStorage0401Probe',
    'canonicalTaskRuntimeProbe', 'canonicalStoryServerServiceProbe',
    'b4NetworkDiscriminatorProbe', 'canonicalOnly0400PackageProbe',
    'canonicalSessionClientModelProbe', 'canonicalSessionForgeRoutingProbe',
    'canonicalSessionNetworkCodecProbe', 'canonicalSessionRuntimeProbe',
    'canonicalSessionSavedDataProbe', 'canonicalSessionServerServiceProbe',
    'canonicalTaskView0400Probe', 'copierTemplateActionCodecProbe',
    'currentProject0400Probe', 'entityDgrIdentityResolverProbe',
    'entityToolsStage5Probe', 'itemIdentitySavedDataProbe',
    'nominator0400Probe', 'nominatorCurrentActions0400Probe',
    'nominatorFailureBoundary0400Probe', 'nominatorItemContainerProbe',
    'nominatorStage4Probe', 'npcIdentitySavedDataProbe',
    'runtimeFontResourceRows0336Probe', 'taskCandidate0400Probe',
    'canonicalTaskForgeProbe', 'publicOutputPriority0336Probe',
    'construction0337Probe', 'taskLayout0337Probe'
)
$common = @('--no-daemon', '--no-configuration-cache', '--max-workers=1', '--console=plain')
if ($Offline) { $common += '--offline' }
$records = @()
foreach ($phase in @('build', 'probes')) {
    $tasks = @(if ($phase -eq 'build') { 'build' } else { $probes + '--continue' })
    $started = [DateTimeOffset]::Now.ToString('o')
    $log = Join-Path $output ($phase + '.log')
    & (Join-Path $root 'gradlew.bat') @tasks @common *> $log
    $code = $LASTEXITCODE
    $records += [pscustomobject]@{
        phase = $phase; tasks = $tasks; arguments = $common; started_at = $started
        finished_at = [DateTimeOffset]::Now.ToString('o'); exit_code = $code
        log = [IO.Path]::GetRelativePath($root, $log)
    }
    Write-Output "$phase exit_code=$code log=$log"
}
$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'commands.json') -Encoding utf8
$failed = @($records | Where-Object exit_code -ne 0).Count
if ($failed) { exit 1 }
