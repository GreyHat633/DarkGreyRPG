$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class DgrDesktop {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool paint);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
[DgrDesktop]::SetProcessDPIAware() | Out-Null
$qaRoot=Join-Path $PSScriptRoot '../evidence/live'
New-Item -ItemType Directory -Force -Path $qaRoot | Out-Null
function Get-Studio {
 $p=@(Get-Process DarkGreyRPGStudio -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0)
 if($p.Count -ne 1){throw "Expected one Studio, found $($p.Count)"}
 $wins=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition); for($k=0;$k -lt $wins.Count;$k++){if($wins[$k].Current.ProcessId -eq $p[0].Id -and $wins[$k].Current.Name -like "*DarkGrey RPG Studio*"){return $wins[$k]}}; throw "Studio root window missing"
}
function Get-Elements($root=(Get-Studio)) {
 $all=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 for($i=0;$i -lt $all.Count;$i++){ $all.Item($i) }
}
function Show-Tree($root=(Get-Studio)) {
 $i=0; Get-Elements $root | ForEach-Object {
  $c=$_.Current; $r=$c.BoundingRectangle
  if(-not $c.IsOffscreen){"$i | $($c.ControlType.ProgrammaticName) | $($c.AutomationId) | $($c.Name) | $($r.X),$($r.Y),$($r.Width),$($r.Height) | enabled=$($c.IsEnabled)"}; $i++
 }
}
function Find-Name([string]$name){
 $e=@(Get-Elements | Where-Object { $_.Current.Name -eq $name -and -not $_.Current.IsOffscreen })
 if($e.Count -ne 1){throw "Expected one '$name', found $($e.Count)"};$e[0]
}
function Click-Element($e){
 if($null -eq $e -or @($e).Count -ne 1){throw "Exactly one observed element required"}
 $w=Get-Studio; [DgrDesktop]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle)|Out-Null
 $r=$e.Current.BoundingRectangle
 if($r.IsEmpty -or $e.Current.IsOffscreen){throw 'Target invisible'}
 [DgrDesktop]::SetCursorPos([int]($r.X+$r.Width/2),[int]($r.Y+$r.Height/2))|Out-Null
 [DgrDesktop]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[DgrDesktop]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 250
}
function Shot([string]$name){
 $w=Get-Studio;[DgrDesktop]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle)|Out-Null
 Start-Sleep -Milliseconds 300
 $r=$w.Current.BoundingRectangle;$b=New-Object Drawing.Bitmap ([int]$r.Width),([int]$r.Height)
 $g=[Drawing.Graphics]::FromImage($b)
 try{$g.CopyFromScreen([int]$r.X,[int]$r.Y,0,0,$b.Size);$path=Join-Path $qaRoot ($name+'.png');$b.Save($path);$path}finally{$g.Dispose();$b.Dispose()}
}
