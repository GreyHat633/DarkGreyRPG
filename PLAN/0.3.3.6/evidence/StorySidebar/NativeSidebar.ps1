$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
$nativeSource=Get-Content -LiteralPath "$PSScriptRoot/../OutputReferenceFix/Native.ps1" -Raw
Invoke-Expression $nativeSource.Substring(0,$nativeSource.IndexOf('$script:taskUiExe'))
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class SidebarNativeWindow {
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd,int x,int y,int width,int height,bool redraw);
}
'@
$script:sidebarProcess=Get-Process -Id ([int](Get-Content -LiteralPath "$PSScriptRoot/pid.txt"))
$script:sidebarExe=[IO.Path]::GetFullPath("$PSScriptRoot/../Studio/DarkGreyRPGStudio.exe")
if($script:sidebarProcess.Path -ne $script:sidebarExe){throw 'Wrong candidate process'}
function Root { [Windows.Automation.AutomationElement]::FromHandle($script:sidebarProcess.MainWindowHandle) }
function All { (Root).FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) }
function Active {
 [TaskUiInput]::SetForegroundWindow($script:sidebarProcess.MainWindowHandle)|Out-Null
 Start-Sleep -Milliseconds 120
 [uint32]$nativeOwner=0
 [SidebarNativeWindow]::GetWindowThreadProcessId([TaskUiInput]::GetForegroundWindow(),[ref]$nativeOwner)|Out-Null
 if($nativeOwner -ne $script:sidebarProcess.Id){throw 'Candidate is not foreground'}
}
function Click([int]$x,[int]$y){Active;[TaskUiInput]::Click($x,$y);Start-Sleep -Milliseconds 330}
function Chord([ushort]$key){Active;[TaskUiInput]::Key(0x11);[TaskUiInput]::Key($key);[TaskUiInput]::Key($key,$true);[TaskUiInput]::Key(0x11,$true);Start-Sleep -Milliseconds 350}
function Find([string]$name){(Root).FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$name))}
function Invoke([string]$name){$element=(All)|Where-Object {$_.Current.Name -eq $name -and $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button}|Select-Object -First 1;if(!$element){throw "Missing button $name"};([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 330}
function Snap([string]$name){
 (All)|ForEach-Object { [pscustomobject]@{ Type=$_.Current.ControlType.ProgrammaticName; Id=$_.Current.AutomationId; Name=$_.Current.Name; Bounds=$_.Current.BoundingRectangle.ToString(); Offscreen=$_.Current.IsOffscreen } }|ConvertTo-Json -Depth 3|Set-Content -LiteralPath "$PSScriptRoot/$name-uia.json" -Encoding utf8
 $image=[Drawing.Bitmap]::new(1920,1080);$graphics=[Drawing.Graphics]::FromImage($image)
 try{$graphics.CopyFromScreen(0,0,0,0,$image.Size);$image.Save("$PSScriptRoot/$name.png")}finally{$graphics.Dispose();$image.Dispose()}
}
