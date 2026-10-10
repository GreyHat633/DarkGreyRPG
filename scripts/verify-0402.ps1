[CmdletBinding()]
param([string]$OutputDirectory='.tooling/0402/Final', [switch]$Offline)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$prior=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'verify-0401.ps1') -Raw
$section=[regex]::Match($prior,'(?s)\$probes = @\((.*?)\n\)').Groups[1].Value
$probes=@([regex]::Matches($section,"'([a-zA-Z0-9]+Probe)'") | ForEach-Object {$_.Groups[1].Value})
if($probes.Count -ne 30){throw 'The inherited current probe list changed; review the baseline'}
$probes+=@('stability0402Probe','stabilityBinding0402Probe','mainThreadQueue0402Probe','taskHotPath0402Probe','storyRecovery0402Probe','mediaLifecycle0402Probe','mixedBusiness0402Probe')
$probes+='runtimeFontPolicy0402Probe'
$probes+='taskNotificationsPlanProbe'
$probes+=@('inspection0334Probe','inspectionBoundary0334Probe','inspectionClient0334Probe')
$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(!$output.StartsWith([IO.Path]::GetFullPath((Join-Path $repo '.tooling'))+[IO.Path]::DirectorySeparatorChar)){throw 'Verification output must be isolated'}
New-Item -ItemType Directory -Force -Path $output | Out-Null
Set-Location -LiteralPath $repo
$common=@('--no-daemon','--no-configuration-cache','--max-workers=1','--console=plain')
if($Offline){$common+='--offline'}
$records=@()
foreach($phase in @('build','probes')) {
    [string[]]$tasks=@(if($phase -eq 'build'){'build'}else{$probes; '--continue'})
    $log=Join-Path $output ($phase+'.log')
    $started=[DateTimeOffset]::Now.ToString('o')
    & (Join-Path $repo 'gradlew.bat') @tasks @common *> $log
    $code=$LASTEXITCODE
    $records+=[pscustomobject]@{phase=$phase;tasks=$tasks;args=$common;started=$started;finished=[DateTimeOffset]::Now.ToString('o');exit_code=$code;log=[IO.Path]::GetRelativePath($repo,$log)}
    $records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'commands.json')
    Write-Output "$phase exit_code=$code"
}
if(@($records | Where-Object exit_code -ne 0).Count){exit 1}
