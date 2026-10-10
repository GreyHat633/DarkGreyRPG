[CmdletBinding()]
param(
    [ValidateSet('ActiveTasks','StaticPlayers','Idle','MixedLifecycle','MixedAuthored')][string]$Scenario='ActiveTasks',
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Name,
    [string]$EngineDirectory='.tooling/0400-minecraft/Server Base',
    [string]$JavaPath='E:/Java/jdk1.8.0_471/bin/java.exe',
    [string]$DriverJar='.tooling/0402/Fixtures/dgr0402-isolated-stability-driver.jar',
    [string]$RuntimeJar='build/libs/darkgrey_rpg-0.4.0.2.jar',
    [switch]$Baseline,
    [ValidateRange(1,120)][int]$DurationMinutes=1,
    [ValidateRange(0,10)][int]$WarmupMinutes=0,
    [ValidateRange(0,5000)][int]$Active=10,
    [ValidateRange(1,32)][int]$Users=4,
    [ValidateRange(1,300)][int]$Nodes=20,
    [ValidateRange(0,10000)][int]$OfflineInstances=0,
    [ValidateRange(0,4)][int]$NetworkPlayers=0,
    [int]$Seed=40201,
    [ValidateRange(1024,65535)][int]$Port=25721,
    [ValidateRange(256,2048)][int]$HeapMiB=512,
    [switch]$Restart,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$Stage='first',
    [switch]$GateReference,
    [switch]$ProfileDrivers,
    [switch]$PrepareFromPlayerImages,
    [ValidateRange(0,5)][int]$SubmissionSpacingTicks=0,
    [switch]$DirectSubmissions
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Absolute([string]$path) {
    if([IO.Path]::IsPathRooted($path)){return [IO.Path]::GetFullPath($path)}
    return [IO.Path]::GetFullPath((Join-Path $repo $path))
}
$worlds=Absolute '.tooling/0402/Worlds'
$root=[IO.Path]::GetFullPath((Join-Path $worlds $Name))
if(!$root.StartsWith($worlds+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid isolated world'}
foreach($parent in @($worlds,(Split-Path $worlds),(Join-Path $repo '.tooling'))){
    if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Linked test directory: $parent"}
}
if($Scenario -eq 'Idle'){$Active=0}
if($Scenario -eq 'MixedLifecycle' -and ($Users -ne 4 -or $NetworkPlayers -ne 0)){throw 'MixedLifecycle uses 4 logical users and separately counted network tests'}
if($Scenario -eq 'MixedAuthored' -and ($Users -ne 4 -or $NetworkPlayers -ne 2)){throw 'MixedAuthored requires 4 logical users and 2 genuine network clients'}
$runtime=Absolute $RuntimeJar
$driver=Absolute $DriverJar
$java=Absolute $JavaPath
foreach($file in @($runtime,$driver,$java)){if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing test input: $file"}}
if(!$Restart){
    if(Test-Path -LiteralPath $root){throw 'Existing isolated world is preserved; use a new Name or explicit Restart'}
    if(Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Port already in use'}
    New-Item -ItemType Directory -Path $root | Out-Null
    $engine=Absolute $EngineDirectory
    foreach($component in @('libraries','forge-1.7.10-10.13.4.1614-1.7.10-universal.jar','minecraft_server.1.7.10.jar','eula.txt')){
        Copy-Item -LiteralPath (Join-Path $engine $component) -Destination $root -Recurse
    }
    New-Item -ItemType Directory -Path (Join-Path $root 'mods'),(Join-Path $root 'config') | Out-Null
    if($Scenario -eq 'MixedAuthored') {
        foreach($locked in @('CustomNPC-Plus-1.11.1-fixed-v1.jar','+unimixins-all-1.7.10-0.3.1.jar')){
            Copy-Item -LiteralPath (Join-Path $repo ('.tooling/0400-minecraft/Server CNPC/mods/'+$locked)) -Destination (Join-Path $root 'mods')
        }
        $packages=Join-Path $root 'DarkGreyRPG/StoryPackages'
        New-Item -ItemType Directory -Path $packages | Out-Null
        $exports=@(Get-ChildItem -LiteralPath (Absolute '.tooling/0402/Fixtures') -File | Where-Object {$_.Extension -eq '.dgrs' -or $_.Name.EndsWith('.dgrs.g')})
        if($exports.Count -ne 2){throw 'Two explicit current Studio export fixtures required'}
        $exports | Copy-Item -Destination $packages
        $generated=Absolute '.tooling/0402/Fixtures/Generated-Mixed-v2'
        $loadPackages=@(Get-ChildItem -LiteralPath $generated -Filter '*.dgrs' -File)
        if($loadPackages.Count -ne 3){throw 'Three validated generated load containers required'}
        $loadPackages | Copy-Item -Destination $packages
        @($exports + $loadPackages | ForEach-Object {[pscustomobject]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash;bytes=$_.Length}}) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'authored-input-packages.json')
    }
    @('level-name=TestWorld',"level-seed=$Seed",'level-type=FLAT',"server-port=$Port",'online-mode=false','gamemode=1','difficulty=0','view-distance=3','max-players=4','spawn-protection=0','allow-flight=true','enable-rcon=false') | Set-Content -LiteralPath (Join-Path $root 'server.properties')
    @('live_bridge {','    B:enabled=false','}') | Set-Content -LiteralPath (Join-Path $root 'config/darkgrey_rpg.cfg')
} elseif(!(Test-Path -LiteralPath (Join-Path $root 'server.properties'))){throw 'Restart requires this existing isolated world'}
if((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked case directory'}
$ownedFile=Join-Path $root 'owned-process.json'
if(Test-Path -LiteralPath $ownedFile){
    $previous=Get-Content -LiteralPath $ownedFile -Raw | ConvertFrom-Json
    $previousProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$($previous.pid)"
    if($previousProcess -and $previousProcess.ExecutablePath -eq $java -and $previousProcess.CommandLine.Contains($root)){throw 'Previous owned process is still running'}
}
Copy-Item -LiteralPath $runtime -Destination (Join-Path $root 'mods/darkgrey_rpg.jar') -Force
Copy-Item -LiteralPath $driver -Destination (Join-Path $root 'mods/dgr0402-isolated-stability-driver.jar') -Force
$stdout=Join-Path $root "$Stage-stdout.log"
$stderr=Join-Path $root "$Stage-stderr.log"
if((Test-Path -LiteralPath $stdout) -or (Test-Path -LiteralPath $stderr)){throw 'Stage logs already exist; preserve them and select another Stage'}
$arguments=@('-Xms128m',"-Xmx${HeapMiB}m", "-Ddgr0402.ticks=$(( $DurationMinutes + $WarmupMinutes ) * 1200)","-Ddgr0402.warmupTicks=$($WarmupMinutes * 1200)","-Ddgr0402.active=$Active","-Ddgr0402.users=$Users","-Ddgr0402.nodes=$Nodes","-Ddgr0402.offline=$OfflineInstances","-Ddgr0402.network=$NetworkPlayers",('-Ddgr0402.root="'+$root+'"'))
$arguments+="-Ddgr0402.submissionSpacingTicks=$SubmissionSpacingTicks"
if($DirectSubmissions){$arguments+='-Ddgr0402.directSubmissions=true'}
if($Baseline){$arguments+='-Ddgr0402.baseline=true'}
if($ProfileDrivers){$arguments+='-Ddgr0402.profileDrivers=true'}
if($PrepareFromPlayerImages){$arguments+='-Ddgr0402.prepareFromPlayerImages=true'}
$arguments+="-Ddgr0402.scenario=$Scenario"
$arguments+=@('-jar','forge-1.7.10-10.13.4.1614-1.7.10-universal.jar','nogui')
$started=[DateTimeOffset]::Now
$configuredSeed=(Get-Content -LiteralPath (Join-Path $root 'server.properties') | Where-Object {$_ -match '^level-seed='}) -replace '^level-seed=',''
$process=Start-Process -FilePath $java -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
$owned=[pscustomobject]@{pid=$process.Id;started=$started.ToString('o');root=$root;stage=$Stage;scenario=$Scenario;baseline=[bool]$Baseline;args=$arguments;runtime_sha256=(Get-FileHash -LiteralPath $runtime).Hash;driver_sha256=(Get-FileHash -LiteralPath $driver).Hash;seed=$Seed;logical_users=$Users;actual_network_required=$NetworkPlayers;active_tasks=$Active;nodes=$Nodes;offline_instances=$OfflineInstances;heap_mib=$HeapMiB;measurement_minutes=$DurationMinutes;warmup_minutes=$WarmupMinutes}
$owned|Add-Member -NotePropertyName configured_level_seed -NotePropertyValue $configuredSeed
if($Scenario -like 'Mixed*'){$owned|Add-Member -NotePropertyName mixed_tasks_per_cycle -NotePropertyValue 12}
$owned|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $ownedFile
$owned|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root "$Stage-inputs.json")
Write-Output "START $Name $Stage PID=$($process.Id) runtime=$($owned.runtime_sha256)"
$telemetry=[Collections.Generic.List[object]]::new()
$heartbeat=Join-Path $root 'heartbeat.txt'
$nextResource=[DateTimeOffset]::Now
$abort=''
while(!$process.HasExited){
    Start-Sleep -Seconds 2
    $process.Refresh()
    if($process.HasExited){break}
    $now=[DateTimeOffset]::Now
    if((Test-Path -LiteralPath $heartbeat) -and (Get-Item -LiteralPath $heartbeat).LastWriteTime -ge $started.LocalDateTime){
        if(($now.LocalDateTime-(Get-Item -LiteralPath $heartbeat).LastWriteTime).TotalSeconds -gt 10){$abort='NO_HEARTBEAT_10S'}
    } elseif(($now-$started).TotalSeconds -gt 90){$abort='NO_STARTUP_HEARTBEAT_90S'}
    if($now -ge $nextResource){
        $free=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory*1024L
        $telemetry.Add([pscustomobject]@{at=$now.ToString('o');cpu_ms=$process.TotalProcessorTime.TotalMilliseconds;working_set=$process.WorkingSet64;private_bytes=$process.PrivateMemorySize64;handles=$process.HandleCount;free_physical_bytes=$free})
        $telemetry|Export-Csv -LiteralPath (Join-Path $root "$Stage-host-resources.csv") -NoTypeInformation
        $nextResource=$now.AddSeconds(10)
        if($free -lt 536870912L){$abort='FREE_PHYSICAL_MEMORY_BELOW_512MIB'}
    }
    if(!$abort){continue}
    # Terminate only the exact Java process launched for this isolated root.
    $target=Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)"
    if(!$target -or $target.ExecutablePath -ne $java -or !$target.CommandLine.Contains($root)){throw 'Watchdog ownership check failed; no process was terminated'}
    $stackPath=Join-Path $root "$Stage-watchdog-stack.txt"
    $jstack=Join-Path (Split-Path $java) 'jstack.exe'
    $helper=Start-Process -FilePath $jstack -ArgumentList @([string]$process.Id) -WindowStyle Hidden -RedirectStandardOutput $stackPath -RedirectStandardError (Join-Path $root "$Stage-watchdog-jstack-error.txt") -PassThru
    if(!$helper.WaitForExit(5000)){Stop-Process -Id $helper.Id -Force}
    [pscustomobject]@{status='STOPPED_BY_WATCHDOG';reason=$abort;pid=$process.Id;root=$root;at=$now.ToString('o');last_heartbeat=$(if(Test-Path -LiteralPath $heartbeat){Get-Content -LiteralPath $heartbeat -Raw}else{''})}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root "$Stage-watchdog.json")
    Stop-Process -Id $process.Id -Force
    $process.WaitForExit()
    throw "$Name stopped by owned-process watchdog: $abort; stack and inputs retained"
}
$process.WaitForExit()
if(Select-String -LiteralPath $stdout -Pattern 'DGR0402_CACHE_STOP=FAIL' -Quiet){throw 'Player journal caches remained after server stop'}
if(!$Baseline -and $DriverJar -eq '.tooling/0402/Fixtures/dgr0402-isolated-stability-driver.jar') {
    $cacheStop=Join-Path $root 'journal-cache-stop.json'
    if(!(Test-Path $cacheStop) -or (Get-Item $cacheStop).LastWriteTime -lt $started.LocalDateTime -or (Get-Content $cacheStop -Raw|ConvertFrom-Json).status -ne 'PASS'){throw 'Fresh journal-cache stop proof missing'}
}
if(!(Select-String -LiteralPath $stdout -Pattern 'DGR0402_REAL_FORGE=PASS' -Quiet) -or (Select-String -LiteralPath $stdout -Pattern 'DGR0402_REAL_FORGE=FAIL' -Quiet)){throw 'Driver failed or did not produce its independent assertions'}
$resultFile=Join-Path $root 'complete-tick-result.json'
if(!(Test-Path -LiteralPath $resultFile) -or (Get-Item -LiteralPath $resultFile).LastWriteTime -lt $started.LocalDateTime){throw 'Missing fresh complete-tick evidence'}
$complete=Get-Content -LiteralPath $resultFile -Raw|ConvertFrom-Json
$gate=$complete.p95_ns -le 40000000L -and $complete.p99_ns -le 50000000L
$summary=[pscustomobject]@{status='PASS_DRIVER_ASSERTIONS_PERFORMANCE_RECORDED';exit_code=$process.ExitCode;at=[DateTimeOffset]::Now.ToString('o');inputs=$owned;complete_tick=$complete;reference_gate_pass=$gate;has_250ms_spike=$complete.max_ns -ge 250000000L;has_500ms_spike=$complete.max_ns -ge 500000000L;world_data_deleted=$false}
$summary|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $root "$Stage-summary.json")
# Preserve raw samples before any later restart reuses the driver's fixed output names.
foreach($sampleName in @('complete-tick-samples.csv','complete-tick-result.json','tick-samples.csv','operation-samples.csv','memory-queue.csv','mixed-operations.csv','mixed-result.json','authored-state.json','driver-tick-profile.csv','driver-tick-profile-inputs.json','prepared-player-baseline.csv','mixed-preparation.json','warmup-complete-tick-samples.csv','warmup-driver-tick-profile.csv','warmup-driver-tick-profile-inputs.json','journal-cache-stop.json')) {
    $sample=Join-Path $root $sampleName
    if((Test-Path -LiteralPath $sample) -and (Get-Item -LiteralPath $sample).LastWriteTime -ge $started.LocalDateTime){Copy-Item -LiteralPath $sample -Destination (Join-Path $root "$Stage-$sampleName")}
}
Write-Output "END $Name reference_gate=$gate p95_ms=$($complete.p95_ns/1e6) p99_ms=$($complete.p99_ns/1e6) max_ms=$($complete.max_ns/1e6)"
if($GateReference -and !$gate){throw 'Reference performance gate failed; complete evidence retained'}
