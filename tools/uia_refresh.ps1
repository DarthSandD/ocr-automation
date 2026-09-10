Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process OcrAutomation -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "NO_PROC"; exit }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
function Find-Btn($name) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
$rb = Find-Btn "Refresh"
$rb.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output "INVOKED_REFRESH"
Start-Sleep -Seconds 3
$lis = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
foreach ($li in $lis) { try { Write-Output ("LOG: {0}" -f $li.Current.Name) } catch {} }
