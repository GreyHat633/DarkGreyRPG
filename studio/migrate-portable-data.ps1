[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$StudioDirectory,
    [Parameter(Mandatory)][string]$LegacySettingsPath
)
$ErrorActionPreference='Stop'
function Assert-NoLinks([string]$Path){
    $fullPath=[IO.Path]::GetFullPath($Path)
    while(-not(Test-Path -LiteralPath $fullPath)){ $fullPath=Split-Path -Parent $fullPath }
    for($ancestor=Get-Item -LiteralPath $fullPath; $null -ne $ancestor; $ancestor=$ancestor.Parent){if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Directory link is not portable: $($ancestor.FullName)"}}
}
$studioRoot=[IO.Path]::GetFullPath($StudioDirectory).TrimEnd('\')
Assert-NoLinks $studioRoot
$legacySettings=[IO.Path]::GetFullPath($LegacySettingsPath)
if(-not(Test-Path -LiteralPath (Join-Path $studioRoot 'DarkGreyRPGStudio.exe') -PathType Leaf)){throw 'Target Studio executable is missing'}
$dataRoot=Join-Path $studioRoot 'Data'
$configPath=Join-Path $dataRoot 'Config\settings.json'
if(Test-Path -LiteralPath $configPath){throw 'Local settings already exist; refusing to overwrite user data'}
if(@(Get-Process -Name DarkGreyRPGStudio -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $studioRoot 'DarkGreyRPGStudio.exe')}).Count){throw 'Close the target Studio before migration'}
$settings=Get-Content -LiteralPath $legacySettings -Raw | ConvertFrom-Json
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot=Join-Path $dataRoot ('MigrationBackup\'+$stamp)
foreach($directory in @('Config','Projects','Cache','Logs','Temp','Exports')){ $directoryPath=Join-Path $dataRoot $directory; Assert-NoLinks $directoryPath; New-Item -ItemType Directory -Force -Path $directoryPath|Out-Null }

function Get-Inventory([string]$Root){
    $rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    Assert-NoLinks $rootPath
    $entries=[Collections.Generic.List[object]]::new()
    $pending=[Collections.Generic.Stack[string]]::new(); $pending.Push($rootPath)
    while($pending.Count){foreach($item in Get-ChildItem -LiteralPath $pending.Pop() -Force){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Link in migration input: $($item.FullName)"}
        if($item.PSIsContainer){$pending.Push($item.FullName)}else{$entries.Add([pscustomobject]@{path=[IO.Path]::GetRelativePath($rootPath,$item.FullName);bytes=$item.Length;sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash})}
    }}
    return @($entries | Sort-Object path)
}
function Copy-Verified([string]$Source,[string]$Destination){
    $before=@(Get-Inventory $Source)
    Assert-NoLinks (Split-Path -Parent $Destination)
    if(Test-Path -LiteralPath $Destination){throw "Migration destination exists: $Destination"}
    $stage=Join-Path $dataRoot ('Temp\migration-'+[guid]::NewGuid().ToString('N'))
    try{
        New-Item -ItemType Directory -Path $stage|Out-Null
        foreach($directory in Get-ChildItem -LiteralPath $Source -Directory -Recurse -Force){
            Assert-NoLinks $directory.FullName
            New-Item -ItemType Directory -Force -Path (Join-Path $stage ([IO.Path]::GetRelativePath($Source,$directory.FullName)))|Out-Null
        }
        foreach($file in $before){$target=Join-Path $stage $file.path; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target)|Out-Null; Copy-Item -LiteralPath (Join-Path $Source $file.path) -Destination $target}
        $encoded=$before|ConvertTo-Json -Depth 4 -Compress
        if($encoded -ne ((Get-Inventory $Source)|ConvertTo-Json -Depth 4 -Compress) -or $encoded -ne ((Get-Inventory $stage)|ConvertTo-Json -Depth 4 -Compress)){throw "Migration copy verification failed: $Source"}
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination)|Out-Null
        Move-Item -LiteralPath $stage -Destination $Destination
        return $before
    }finally{
        $resolvedStage=[IO.Path]::GetFullPath($stage)
        if(-not $resolvedStage.StartsWith((Join-Path $dataRoot 'Temp')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe staging cleanup path'}
        if(Test-Path -LiteralPath $resolvedStage){Remove-Item -LiteralPath $resolvedStage -Recurse -Force}
    }
}
$settingsInventory=@(Copy-Verified (Split-Path -Parent $legacySettings) (Join-Path $backupRoot 'LegacyStudio'))
$records=[Collections.Generic.List[object]]::new(); $mapping=@{}; $recent=[Collections.Generic.List[string]]::new()
foreach($source in @($settings.last_project)+@($settings.recent_projects)){
    if(-not $source -or $mapping.ContainsKey($source) -or -not(Test-Path -LiteralPath (Join-Path $source 'project.json') -PathType Leaf)){continue}
    $folder=Split-Path -Leaf $source; $destination=Join-Path $dataRoot ('Projects\'+$folder)
    for($suffix=2;Test-Path -LiteralPath $destination;$suffix++){$destination=Join-Path $dataRoot ('Projects\'+$folder+'_'+$suffix)}
    $inventory=@(Copy-Verified $source $destination)
    $relative=[IO.Path]::GetRelativePath($studioRoot,$destination)
    $mapping[$source]=$relative; $recent.Add($relative)
    $records.Add([pscustomobject]@{source=$source;destination=$destination;files=$inventory.Count;bytes=($inventory|Measure-Object bytes -Sum).Sum;inventory=$inventory;source_preserved=$true})
}
$settings.last_project=if($settings.last_project -and $mapping.ContainsKey($settings.last_project)){$mapping[$settings.last_project]}else{$null}
$settings.recent_projects=$recent.ToArray()
$settings.last_export_directory='Data\Exports'
$settings.last_import_directory=$null; $settings.last_reference_directory=$null
$temporaryConfig=$configPath+'.tmp'
$settings|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $temporaryConfig -Encoding utf8
Move-Item -LiteralPath $temporaryConfig -Destination $configPath
$report=[ordered]@{time=(Get-Date -Format o);studio=$studioRoot;legacy_settings=$legacySettings;backup=$backupRoot;settings_inventory=$settingsInventory;projects=$records.ToArray();copy_verification='PASS';runtime_open_verification='PENDING';legacy_cleanup='PENDING'}
$report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $backupRoot 'MigrationManifest.json') -Encoding utf8
[pscustomobject]@{Studio=$studioRoot;Backup=$backupRoot;Projects=$records.Count;Settings=$configPath}|ConvertTo-Json
