$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$exe=Join-Path $repo 'dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe'
if(Get-Process -Name DarkGreyRPGStudio -ErrorAction SilentlyContinue | Where-Object Path -eq $exe) {throw 'Authoritative copy is already running'}
$process=Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -PassThru
$process.Id | Set-Content -LiteralPath "$PSScriptRoot/dist-pid.txt"
if(!$process.WaitForInputIdle(10000)) {throw 'Authoritative launch did not become idle'}
Start-Sleep -Milliseconds 800
$process.Refresh()
$native=Get-Content -LiteralPath "$PSScriptRoot/Native.ps1" -Raw
$native=$native.Replace('pid.txt','dist-pid.txt').Replace('$PSScriptRoot/../Studio/DarkGreyRPGStudio.exe', $exe)
$native=$native.Replace('$PSScriptRoot', $PSScriptRoot)
Invoke-Expression $native
[TaskUiInput]::ShowWindow($process.MainWindowHandle,3)|Out-Null
Active
Start-Sleep -Milliseconds 800
Snap '05-authoritative-launch'
$title=(Root).Current.Name
$unexpected=@((All) | Where-Object {$_.Current.ControlType -eq [Windows.Automation.ControlType]::Window -and $_.Current.Name -match '错误|异常|崩溃'})
$closed=$process.CloseMainWindow()
if(!$closed -or !$process.WaitForExit(10000)) {throw 'Authoritative normal close failed'}
$before=@(Get-Content -LiteralPath "$PSScriptRoot/DataBefore.json" -Raw | ConvertFrom-Json)
$data=Join-Path (Split-Path -Parent $exe) 'Data'
$after=@(Get-ChildItem -LiteralPath $data -Recurse -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{Path=[IO.Path]::GetRelativePath($data,$_.FullName);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$differences=@(Compare-Object $before $after -Property Path,Bytes,Sha256)
$proof=[pscustomobject]@{Path=$exe;ProcessId=$process.Id;WindowTitle=$title;NormalExit=$true;ExitCode=$process.ExitCode;UnexpectedErrorWindows=$unexpected.Count;DataFilesBefore=$before.Count;DataFilesAfter=$after.Count;DataDifferenceCount=$differences.Count;Method='UI Automation and Win32 PrintWindow of authoritative app window'}
$proof | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$PSScriptRoot/DistSmoke.json"
$proof | ConvertTo-Json -Depth 4
if($unexpected.Count -or $differences.Count -or $process.ExitCode -ne 0) {throw 'Authoritative smoke check failed'}
