$ErrorActionPreference='Stop'
$stagingRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../wpf-build')).Path
$names=@(
 'portable-publish-c920b19cc742476395385ab6c4b8cf8e',
 'organized-publish-56ae1cb35b194e869192986ea628f47e',
 'portable-publish-b2582dc3c06a4dd78c3a7d10b3e1a516',
 'organized-publish-fe60e47747e8440f92a51364c79392cf',
 'portable-publish-fd0b5d883741468fb000872480dd7d53',
 'organized-publish-bafe1972ff30493d8e5a6048821204ba'
)
$entries=@($names|ForEach-Object {
 $path=(Resolve-Path -LiteralPath (Join-Path $stagingRoot $_)).Path
 if((Split-Path -Parent $path) -ne $stagingRoot -or !$path.StartsWith($stagingRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Unsafe staging path: $path"}
 $tree=@(Get-Item -LiteralPath $path)+@(Get-ChildItem -LiteralPath $path -Recurse -Force)
 if(@($tree|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or ($_.PSIsContainer -and $_.Name -eq 'Data')}).Count){throw "Personal data or link in staging: $path"}
 if(@(Get-Process -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path.StartsWith($path+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){throw 'Staging is running'}
 [pscustomobject]@{Path=$path;Bytes=($tree|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum;Removed=$false;RecyclePayloadExists=$false}
})
$entries|ConvertTo-Json|Set-Content -LiteralPath "$PSScriptRoot/recycle-before.json" -Encoding utf8
Add-Type -AssemblyName Microsoft.VisualBasic
foreach($entry in $entries){
 [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($entry.Path,[Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,[Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
 $entry.Removed=!(Test-Path -LiteralPath $entry.Path)
 if(!$entry.Removed){throw 'Recycle transfer failed'}
}
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$recycleRoot=Join-Path 'E:\$Recycle.Bin' $sid
foreach($metadata in Get-ChildItem -LiteralPath $recycleRoot -Filter '$I*' -File -Force){
 $bytes=[IO.File]::ReadAllBytes($metadata.FullName)
 if($bytes.Length -lt 28){continue}
 $version=[BitConverter]::ToInt64($bytes,0)
 $original=if($version -eq 2){[Text.Encoding]::Unicode.GetString($bytes,28,$bytes.Length-28).TrimEnd([char]0)}else{[Text.Encoding]::Unicode.GetString($bytes,24,$bytes.Length-24).TrimEnd([char]0)}
 $entry=$entries|Where-Object {$_.Path -eq $original}
 if($entry){$entry.RecyclePayloadExists=Test-Path -LiteralPath (Join-Path $recycleRoot ('$R'+$metadata.Name.Substring(2)))}
}
$entries|ConvertTo-Json|Set-Content -LiteralPath "$PSScriptRoot/recycle-result.json" -Encoding utf8
if(@($entries|Where-Object {!$_.RecyclePayloadExists}).Count){throw 'Missing Recycle Bin payload'}
@{Directories=$entries.Count;Bytes=($entries|Measure-Object Bytes -Sum).Sum;AllRemoved=$true;AllPayloadsVerified=$true;RecycleBinEmptied=$false}|ConvertTo-Json
