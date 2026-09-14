$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$env:DOTNET_CLI_HOME = Join-Path $root '.tooling/wpf-build'
$env:NUGET_PACKAGES = Join-Path $env:DOTNET_CLI_HOME 'nuget-packages'
$env:TEMP = Join-Path $env:DOTNET_CLI_HOME 'temp'
$env:TMP = $env:TEMP
$env:windir = $env:SystemRoot
$candidate = Join-Path $root '.tooling/0.3.3.1/integrated-candidate'
$delivery = Join-Path $root 'dist/DarkGreyRPGStudio'
$exePath = Join-Path $delivery 'DarkGreyRPGStudio.exe'
& E:/Java/dotnet-sdk-10/dotnet.exe publish (Join-Path $root 'studio/src/DarkGreyRPG.Studio/DarkGreyRPG.Studio.csproj') -c Release -r win-x64 --self-contained true --output $candidate --nologo --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Studio publish failed' }
. (Join-Path $PSScriptRoot 'Desktop.ps1')
$processes = @(Get-Process DarkGreyRPGStudio -ErrorAction SilentlyContinue)
foreach ($process in $processes) {
    if ($process.Path -ne $exePath) { throw 'Another Studio is open; preserving it' }
    [DgrDesktop]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
    [Windows.Forms.SendKeys]::SendWait('^s')
    Start-Sleep -Milliseconds 350
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(3000)) { throw 'Studio did not close cleanly' }
}
Copy-Item -Path (Join-Path $candidate '*') -Destination $delivery -Recurse -Force
$exe = Get-Item $exePath
$hash = (Get-FileHash $exePath -Algorithm SHA256).Hash
if ($hash -ne (Get-FileHash (Join-Path $candidate 'DarkGreyRPGStudio.exe') -Algorithm SHA256).Hash) { throw 'Promotion hash mismatch' }
[pscustomobject]@{ ProductVersion = $exe.VersionInfo.ProductVersion; Bytes = $exe.Length; SHA256 = $hash; Path = $exePath } | ConvertTo-Json | Set-Content (Join-Path $root '.tooling/0.3.3.1/integrated-delivery.json')
$env:DARKGREYRPG_STUDIO_SETTINGS_PATH = Join-Path $root '.tooling/0.3.3.1/live-settings.json'
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $root '.tooling/0.3.3.1/bundle'
Start-Process -FilePath $exePath -WorkingDirectory $delivery -WindowStyle Normal
Get-Content (Join-Path $root '.tooling/0.3.3.1/integrated-delivery.json')
