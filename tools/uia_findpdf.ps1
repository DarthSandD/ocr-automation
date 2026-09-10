$found = Get-ChildItem "C:\Users\USER\Documents", "C:\Users\USER\Downloads" -Filter *.pdf -ErrorAction SilentlyContinue | Select-Object -First 5
foreach ($f in $found) {
  Write-Output ("PDF: {0} KB={1:N0} FULL={2}" -f $f.Name, ($f.Length/1KB), $f.FullName)
}
if ($found.Count -eq 0) { Write-Output "NO_PDFS" }
