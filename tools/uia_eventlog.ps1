$evts = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddHours(-8)} -MaxEvents 200 -ErrorAction SilentlyContinue
$count = 0
foreach ($e in $evts) {
  $m = ""
  try { $m = $e.Message } catch {}
  if ($m -like '*Ocr*' -or $m -like '*Tesseract*' -or $m -like '*WPF*' -or $m -like '*PresentationFramework*' -or $m -like '*MainWindow*') {
    Write-Output "=== $($e.TimeCreated) [$($e.ProviderName)] ==="
    $len = [Math]::Min(600, $m.Length)
    Write-Output $m.Substring(0, $len)
    $count++
    if ($count -ge 5) { break }
  }
}
Write-Output ("MATCHED={0}" -f $count)
