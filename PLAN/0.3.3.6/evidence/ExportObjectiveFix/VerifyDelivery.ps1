$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$dist=Join-Path $repo 'dist/DarkGreyRPGStudio'
$candidate=Join-Path $PSScriptRoot '../Studio'
$data=Join-Path $dist 'Data'
function DataSnapshot {
    @(Get-ChildItem -LiteralPath $data -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=[IO.Path]::GetRelativePath($data,$_.FullName);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
$before=@(Get-Content -LiteralPath "$PSScriptRoot/DataBefore.json" -Raw | ConvertFrom-Json)
$after=DataSnapshot
$after | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$PSScriptRoot/DataAfter.json"
$difference=@(Compare-Object $before $after -Property Path,Bytes,Sha256)
$manifest=@(Get-Content -LiteralPath "$dist/Docs/StudioProgramFiles.json" -Raw | ConvertFrom-Json)
$missing=@($manifest | Where-Object {!(Test-Path -LiteralPath (Join-Path $dist $_) -PathType Leaf)})
$files=@('DarkGreyRPGStudio.exe','Program/DarkGreyRPGStudio.dll','Program/DarkGreyRPG.Studio.Core.dll')
$artifacts=@($files | ForEach-Object {
    $file=Get-Item -LiteralPath (Join-Path $dist $_)
    $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $candidateHash=(Get-FileHash -LiteralPath (Join-Path $candidate $_) -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{Path=$_;Bytes=$file.Length;ProductVersion=$file.VersionInfo.ProductVersion;Sha256=$hash;CandidateMatches=($hash -eq $candidateHash)}
})
$jar=Get-Item -LiteralPath (Join-Path $repo 'dist/darkgrey_rpg-0.3.3.6.jar')
$proof=[pscustomobject]@{
    Version='0.3.3.6';Artifacts=$artifacts
    Runtime=[pscustomobject]@{Path='dist/darkgrey_rpg-0.3.3.6.jar';Bytes=$jar.Length;Sha256=(Get-FileHash -LiteralPath $jar.FullName).Hash.ToLowerInvariant();RebuiltThisTurn=$true}
    DataFilesBefore=$before.Count;DataFilesAfter=$after.Count;DataBytes=($after | Measure-Object Bytes -Sum).Sum;DataDifferenceCount=$difference.Count
    ProgramManifestCount=$manifest.Count;ProgramManifestMissing=$missing.Count
    RootFiles=@(Get-ChildItem -LiteralPath $dist -File | ForEach-Object Name)
}
$proof | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$PSScriptRoot/DeliveryProof.json"
$proof | ConvertTo-Json -Depth 6
if($difference.Count -or $missing.Count -or ($artifacts | Where-Object {!$_.CandidateMatches})) {throw 'Authoritative delivery verification failed'}
if($proof.RootFiles.Count -ne 1 -or $proof.RootFiles[0] -ne 'DarkGreyRPGStudio.exe') {throw 'Wrong root layout'}
