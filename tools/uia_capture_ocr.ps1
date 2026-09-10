Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process OcrAutomation -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "NO_PROC"; exit }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
function Find-Btn($name) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
(Find-Btn "Capture Window").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output "INVOKED_CAPTURE"
Start-Sleep -Seconds 4
(Find-Btn "Run OCR").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output "INVOKED_OCR"
Start-Sleep -Seconds 45
$texts = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
foreach ($t in $texts) { try { Write-Output ("TXT: [{0}]" -f $t.Current.Name) } catch {} }
$edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
foreach ($e in $edits) {
  try {
    $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $t = $vp.Current.Value
    $preview = if ($t.Length -gt 400) { $t.Substring(0,400) } else { $t }
    Write-Output ("EDIT_TEXT len={0}: {1}" -f $t.Length, $preview)
  } catch { Write-Output "EDIT_NOVALUE" }
}
