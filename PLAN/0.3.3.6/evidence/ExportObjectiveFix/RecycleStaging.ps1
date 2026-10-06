$ErrorActionPreference='Stop'
Add-Type -AssemblyName Microsoft.VisualBasic
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$buildRoot=[IO.Path]::GetFullPath((Join-Path $repo '.tooling/wpf-build'))
$names=@(
    'organized-publish-25a32f165fbf400ba9c7fa22edc70768',
    'organized-publish-8892b27a9c8d43838f6cdb689c34a90d',
    'portable-publish-85e71957f3a94b129bc78a6cb410946d',
    'portable-publish-f6d9aa6826fa41a7ae24a930d11966c1'
)
$baseline=@(Get-Content -LiteralPath "$PSScriptRoot/StagingBefore.json" -Raw | ConvertFrom-Json)
$currentHash=(Get-FileHash -LiteralPath (Join-Path $repo 'dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll')).Hash
$targets=@(foreach($name in $names) {
    if($name -in $baseline) {throw 'Attempt to recycle a pre-existing stage'}
    $full=(Resolve-Path -LiteralPath (Join-Path $buildRoot $name)).Path
    if([IO.Path]::GetDirectoryName($full) -ne $buildRoot) {throw "Unexpected target: $full"}
    if((Get-Item -LiteralPath $full).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw "Linked target: $full"}
    $children=@(Get-ChildItem -LiteralPath $full -Recurse -Force)
    if($children | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}) {throw "Linked child: $full"}
    $dll=Join-Path $full $(if($name -like 'organized-*'){'Program/DarkGreyRPGStudio.dll'}else{'DarkGreyRPGStudio.dll'})
    if((Get-FileHash -LiteralPath $dll).Hash -ne $currentHash) {throw "Not a current build: $full"}
    if(Test-Path -LiteralPath (Join-Path $full 'Data')) {throw "Unexpected user Data: $full"}
    [pscustomobject]@{Path=$full;Bytes=($children | Where-Object {!$_.PSIsContainer} | Measure-Object Length -Sum).Sum;StudioDllSHA256=$currentHash}
})
$targets | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$PSScriptRoot/RecycleBefore.json"
foreach($target in $targets) {
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($target.Path,[Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,[Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
    if(Test-Path -LiteralPath $target.Path) {throw "Source remains: $($target.Path)"}
}
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$bin=Join-Path 'E:\$Recycle.Bin' $sid
$payloads=@(foreach($metadata in Get-ChildItem -LiteralPath $bin -Force -File -Filter '$I*') {
    $data=[IO.File]::ReadAllBytes($metadata.FullName)
    if($data.Length -lt 28) {continue}
    $version=[BitConverter]::ToInt64($data,0)
    $offset=if($version -eq 2){28}else{24}
    $original=[Text.Encoding]::Unicode.GetString($data,$offset,$data.Length-$offset).TrimEnd([char]0)
    if($targets.Path -contains $original) {
        $payload=Join-Path $bin ('$R'+$metadata.Name.Substring(2))
        [pscustomobject]@{Original=$original;Payload=$payload;Exists=(Test-Path -LiteralPath $payload);RecordedBytes=[BitConverter]::ToInt64($data,8)}
    }
})
$proof=[pscustomobject]@{Method='SendToRecycleBin';DirectoryCount=$targets.Count;Bytes=($targets | Measure-Object Bytes -Sum).Sum;SourcePathsRemoved=(@($targets | Where-Object {Test-Path -LiteralPath $_.Path}).Count -eq 0);VerifiedPayloadCount=@($payloads | Where-Object Exists).Count;RecycleBinEmptied=$false;Payloads=$payloads}
$proof | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$PSScriptRoot/RecycleProof.json"
$proof | Select-Object Method,DirectoryCount,Bytes,SourcePathsRemoved,VerifiedPayloadCount,RecycleBinEmptied | ConvertTo-Json
if($proof.VerifiedPayloadCount -ne $targets.Count) {throw 'Recycle payload check failed'}
