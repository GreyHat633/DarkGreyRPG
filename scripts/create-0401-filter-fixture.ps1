[CmdletBinding()]
param([string]$SourcePackage='.tooling/0401/Studio/Data/Exports/0400 库存验收.dgrs')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
$source=[IO.Path]::GetFullPath($SourcePackage)
$root=Join-Path $repo '.tooling/0401/Fixtures'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$target=Join-Path $root '0401-filter-controlled.dgrs'
Copy-Item -LiteralPath $source -Destination $target
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($target,[IO.Compression.ZipArchiveMode]::Update)
function Read-Entry([string]$Name) {
    $reader=[IO.StreamReader]::new($zip.GetEntry($Name).Open())
    try { $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
}
function Write-Entry([string]$Name,$Data) {
    $zip.GetEntry($Name).Delete()
    $writer=[IO.StreamWriter]::new($zip.CreateEntry($Name).Open(),[Text.UTF8Encoding]::new($false))
    try { $writer.Write(($Data | ConvertTo-Json -Depth 70)) } finally { $writer.Dispose() }
}
try {
    # Keep the current accepted producer contract. Fixture provenance belongs
    # in the sidecar; a new producer identifier is correctly rejected by Runtime.
    $name='resources/canonical/tasks/stories/ST-4EUB-29DN-V8J6-VTY3/resources/task/r-journey.json'
    $task=Read-Entry $name
    $task.display_name='0401 附加过滤与提交验收'
    foreach($node in $task.graph.nodes) {
        if($node.properties.objective_type -in @('collect_item','submit_item')) {
            $node.properties.metadata=@{damage='3'}
            $node.properties.description='仅 damage=3 的当前 DGR 目标计数／提交；damage=4 留在库存'
        }
    }
    Write-Entry $name $task
} finally { $zip.Dispose() }
[pscustomobject]@{source_package=[IO.Path]::GetRelativePath($repo,$source);source_sha256=(Get-FileHash $source).Hash;fixture_package=[IO.Path]::GetRelativePath($repo,$target);fixture_sha256=(Get-FileHash $target).Hash;origin='Controlled ZIP mutation of an actual Studio export; metadata was NOT authored through UI';changes=@('Task display name/descriptions','collect/submit metadata.damage=3');schemas_changed=$false;producer_preserved=$true} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'provenance.json')
Get-Content (Join-Path $root 'provenance.json')
