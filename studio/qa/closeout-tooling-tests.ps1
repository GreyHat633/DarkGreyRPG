[CmdletBinding()]
param([string]$MediaSourceDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testRoot = Join-Path $root ('.tooling/closeout/tooling-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$prepare = Join-Path $root 'studio/prepare-media-tools.ps1'
$package = Join-Path $root 'studio/package-studio.ps1'
$source = if ($MediaSourceDirectory) { [IO.Path]::GetFullPath($MediaSourceDirectory) } else { Join-Path $root '.tooling/media-tools/ffmpeg' }
$destination = Join-Path $testRoot '中文 Media Tools'
$passed = 0
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    $failed = $false
    try { & $Action } catch { if ($_.Exception.Message -notlike ('*' + $Message + '*')) { throw }; $failed = $true }
    if (-not $failed) { throw "Expected failure: $Message" }
    $script:passed++
}
function Fingerprint([string]$Path) {
    return (Get-ChildItem -LiteralPath $Path -File | Sort-Object Name | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash + ':' + $_.LastWriteTimeUtc.Ticks }) -join '|'
}
try {
    & $prepare -SourceDirectory $source -DestinationDirectory $destination -Offline
    $before = Fingerprint $destination
    & $prepare -DestinationDirectory $destination -Offline
    if ((Fingerprint $destination) -ne $before) { throw 'Verified offline reuse changed files' }
    $passed++
    & $prepare -DestinationDirectory $destination
    if ((Fingerprint $destination) -ne $before) { throw 'Verified repeated prepare changed files' }
    $passed++
    Expect-Failure { & $prepare -DestinationDirectory (Join-Path $testRoot 'Missing') -Offline } 'Offline preparation requires'
    $corrupt = Join-Path $testRoot 'corrupt.zip'
    Set-Content -LiteralPath $corrupt -Value 'incomplete download'
    Expect-Failure { & $prepare -ArchivePath $corrupt -DestinationDirectory $destination } 'SHA-256 mismatch'
    if ((Fingerprint $destination) -ne $before) { throw 'Failed archive changed valid files' }
    $invalidSource = Join-Path $testRoot 'InvalidSource'
    New-Item -ItemType Directory -Path $invalidSource | Out-Null
    foreach ($name in @('ffmpeg.exe','ffprobe.exe','LICENSE','README.txt')) { Set-Content -LiteralPath (Join-Path $invalidSource $name) -Value 'wrong bytes' }
    Expect-Failure { & $prepare -SourceDirectory $invalidSource -DestinationDirectory $destination } 'file SHA-256 mismatch'
    if ((Fingerprint $destination) -ne $before) { throw 'Failed local preparation changed valid files' }
    Expect-Failure { & $package -DotnetPath (Join-Path $testRoot 'missing-dotnet.exe') } 'was not found'
    $fakeSdk = Join-Path $testRoot 'unsupported-dotnet.ps1'
    Set-Content -LiteralPath $fakeSdk -Value "Write-Output '9.0.100'; `$global:LASTEXITCODE = 0"
    Expect-Failure { & $package -DotnetPath $fakeSdk } 'stable .NET 10 SDK is required'
    $fakeRoot = Join-Path $testRoot 'x86-sdk'
    $fakeSdkFiles = Join-Path $fakeRoot 'sdk/10.0.100'
    New-Item -ItemType Directory -Path (Join-Path $fakeSdkFiles 'AppHostTemplate') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $fakeSdkFiles 'Microsoft.NET.HostModel.dll') -Value 'preflight fixture'
    $pe = [byte[]]::new(256)
    $pe[0] = 0x4d; $pe[1] = 0x5a; $pe[0x3c] = 0x80
    $pe[0x80] = 0x50; $pe[0x81] = 0x45; $pe[0x84] = 0x4c; $pe[0x85] = 0x01
    [IO.File]::WriteAllBytes((Join-Path $fakeSdkFiles 'AppHostTemplate/apphost.exe'), $pe)
    $fakeX86 = Join-Path $fakeRoot 'dotnet.ps1'
    Set-Content -LiteralPath $fakeX86 -Value @'
if ($args[0] -eq '--version') { Write-Output '10.0.100' }
elseif ($args[0] -eq '--list-sdks') { Write-Output ('10.0.100 [' + (Join-Path $PSScriptRoot 'sdk') + ']') }
$global:LASTEXITCODE = 0
'@
    Expect-Failure { & $package -DotnetPath $fakeX86 } 'Windows x64 apphost'
    [pscustomobject]@{Passed=$passed;Failed=0;Scope='offline/repeat, missing/corrupt media, preservation, SDK preflight'}
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath((Join-Path $root '.tooling/closeout')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path' }
    if (@(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Refusing test cleanup through links' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
