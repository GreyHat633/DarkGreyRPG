param([string]$Action='snapshot',[string]$Id,[string]$Name,[string]$Value,[int]$X,[int]$Y,[int]$X2,[int]$Y2,[string]$Shot='current',[ValidateSet('studio','game')][string]$Target='studio')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Native0332 {
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
[DllImport("user32.dll")] public static extern void keybd_event(byte v,byte s,uint f,UIntPtr e);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
[DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string name,uint flags);
}
'@
$root='E:\Java\MinecraftMod\DarkGreyRPG'
$pidFile=if($Target -eq 'studio'){'pid.txt'}else{'game-pid.txt'}
$targetPid=[int](Get-Content "$root\.tooling\0332-ui\$pidFile")
$process=Get-Process -Id $targetPid
if($Target -eq 'studio' -and $process.Path -ne "$root\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe"){throw 'Unexpected process'}
if($Target -eq 'game' -and ($process.Path -notlike 'E:\Java\*\java.exe' -or $process.MainWindowTitle -ne 'Minecraft 1.7.10')){throw 'Unexpected game process'}
$condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$targetPid)
$windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition)
if($windows.Count -eq 0){throw 'No target window'}
$window=$windows[0]
foreach($candidate in $windows){if($candidate.Current.NativeWindowHandle -eq $process.MainWindowHandle){$window=$candidate}}
if($Action -ne 'snapshot'){
 [void][Native0332]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
 Start-Sleep -Milliseconds 200
 if($Action -eq 'drag'){
  $rect=$window.Current.BoundingRectangle
  [void][Native0332]::SetCursorPos([int]($rect.X+$X),[int]($rect.Y+$Y))
  [Native0332]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
  try { for($step=1;$step -le 16;$step++){[void][Native0332]::SetCursorPos([int]($rect.X+$X+($X2-$X)*$step/16),[int]($rect.Y+$Y+($Y2-$Y)*$step/16));Start-Sleep -Milliseconds 25} }
  finally {[Native0332]::mouse_event(4,0,0,0,[UIntPtr]::Zero)}
 } elseif($Action -eq 'valueAt'){
  $rect=$window.Current.BoundingRectangle
  $element=[Windows.Automation.AutomationElement]::FromPoint([Windows.Point]::new($rect.X+$X,$rect.Y+$Y))
  if($element.Current.ProcessId -ne $targetPid){throw 'Wrong process at point'}
  $pattern=$element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
  $pattern.SetValue($Value)
  if($pattern.Current.Value -ne $Value){throw 'Value verification failed'}
 } elseif($Action -eq 'click' -or $Action -eq 'double' -or $Action -eq 'right'){
  $rect=$window.Current.BoundingRectangle
  if($X -lt 0 -or $Y -lt 0 -or $X -ge $rect.Width -or $Y -ge $rect.Height){throw 'Outside target window'}
  [void][Native0332]::SetCursorPos([int]($rect.X+$X),[int]($rect.Y+$Y))
  if($Action -eq 'right'){[Native0332]::mouse_event(8,0,0,0,[UIntPtr]::Zero);[Native0332]::mouse_event(16,0,0,0,[UIntPtr]::Zero)}else{[Native0332]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 80; [Native0332]::mouse_event(4,0,0,0,[UIntPtr]::Zero)}
  if($Action -eq 'double'){Start-Sleep -Milliseconds 70; if($Action -eq 'right'){[Native0332]::mouse_event(8,0,0,0,[UIntPtr]::Zero);[Native0332]::mouse_event(16,0,0,0,[UIntPtr]::Zero)}else{[Native0332]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 80; [Native0332]::mouse_event(4,0,0,0,[UIntPtr]::Zero)}}
 } elseif($Action -eq 'english'){
  $layout=[Native0332]::LoadKeyboardLayout('00000409',0)
  [void][Native0332]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,0x50,[IntPtr]::Zero,$layout)
 } elseif($Action -eq 'vk'){
  if([Native0332]::GetForegroundWindow() -ne [IntPtr]$window.Current.NativeWindowHandle){throw 'Target not foreground'}
  [Native0332]::keybd_event([byte]$Value,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 100
  [Native0332]::keybd_event([byte]$Value,0,2,[UIntPtr]::Zero)
 } elseif($Action -eq 'keys'){
  if([Native0332]::GetForegroundWindow() -ne [IntPtr]$window.Current.NativeWindowHandle){throw 'Target not foreground'}
  [Windows.Forms.SendKeys]::SendWait($Value)
 } else {
  $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
  $wanted=if($Id){$Id}else{$Name}
  $matches=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$wanted))
  $visible=@($matches | Where-Object {-not $_.Current.IsOffscreen -and $_.Current.ControlType -ne [Windows.Automation.ControlType]::Text})
  if($visible.Count -ne 1){throw "Expected one visible control: $wanted; found $($visible.Count)"}
  $element=$visible[0]
  if($Action -eq 'invoke'){$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()}
  elseif($Action -eq 'value'){$element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Value)}
  elseif($Action -eq 'select'){$element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()}
  else{throw 'Unknown action'}
 }
 Start-Sleep -Milliseconds 500
}
$lines=[Collections.Generic.List[string]]::new()
foreach($w in $windows){
 $lines.Add("WINDOW: $($w.Current.Name) $($w.Current.BoundingRectangle)")
 $all=$w.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
 foreach($e in $all){if($e.Current.IsOffscreen){continue}; $c=$e.Current
  $lines.Add("$($c.ControlType.ProgrammaticName) id=$($c.AutomationId) name=$($c.Name) enabled=$($c.IsEnabled) rect=$($c.BoundingRectangle)")
 }
}
$lines | Set-Content "$root\PLAN\0.3.3.2\evidence\native-ui\$Shot.txt" -Encoding UTF8
$lines
[void][Native0332]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
Start-Sleep -Milliseconds 200
$r=$window.Current.BoundingRectangle
$bitmap=New-Object Drawing.Bitmap ([int]$r.Width),([int]$r.Height)
$g=[Drawing.Graphics]::FromImage($bitmap)
try{$g.CopyFromScreen([int]$r.X,[int]$r.Y,0,0,$bitmap.Size);$bitmap.Save("$root\PLAN\0.3.3.2\evidence\native-ui\$Shot.png")}finally{$g.Dispose();$bitmap.Dispose()}






