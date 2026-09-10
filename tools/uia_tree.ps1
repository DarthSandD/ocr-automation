Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process OcrAutomation -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "NO_PROC"; exit }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$kids = $win.FindAll([System.Windows.Automation.TreeScope]::Children,
  [System.Windows.Automation.Condition]::TrueCondition)
Write-Output ("CHILDREN={0}" -f $kids.Count)
foreach ($k in $kids) {
  try { Write-Output ("KID type={0} name=[{1}]" -f $k.Current.ControlType.ProgrammaticName, $k.Current.Name) } catch {}
}
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  [System.Windows.Automation.Condition]::TrueCondition)
Write-Output ("DESCENDANTS={0}" -f $all.Count)
