Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$proc = Get-Process OcrAutomation -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "NO_PROC"; exit }
$win = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$lists = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::List)))
foreach ($list in $lists) {
  try {
    $sp = $list.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    $sp.SetScrollPercent(-1, 100)
    Write-Output "SCROLLED_TO_BOTTOM"
  } catch { Write-Output "NO_SCROLL" }
}
Start-Sleep -Seconds 1
$texts = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
foreach ($t in $texts) {
  try {
    $n = $t.Current.Name
    if ($n -like "*vailable*" -or $n -like "*rror*" -or $n -like "*xception*" -or $n -like "*essdata*" -or $n -like "*Init*" -or $n -like "*OCR done*" -or $n -like "*No OCR*") { Write-Output ("TXT: [{0}]" -f $n) }
  } catch {}
}
