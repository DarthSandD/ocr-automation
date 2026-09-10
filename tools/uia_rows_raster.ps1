Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process OcrAutomation -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "NO_PROC"; exit }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
function Find-Btn($name) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
(Find-Btn "To Rows").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output "INVOKED_TO_ROWS"
Start-Sleep -Seconds 2
(Find-Btn "Save Raster").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output "INVOKED_SAVE_RASTER"
Start-Sleep -Seconds 3
Get-ChildItem "$env:USERPROFILE/Pictures/OcrAutomation" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | Format-Table Name, Length, LastWriteTime
$edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
foreach ($e in $edits) {
  try {
    $t = ($e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
    Write-Output ("EDIT len={0} lines={1}" -f $t.Length, ($t -split "`r?`n").Count)
  } catch {}
}
