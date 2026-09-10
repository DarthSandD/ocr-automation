$dll = "C:\Users\USER\.nuget\packages\pdftoimage\3.0.0\lib\net8.0\PDFtoImage.dll"
$bytes = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
$hits = @()
foreach ($m in [regex]::Matches($text, '[A-Za-z][A-Za-z0-9_]{3,40}')) {
  $w = $m.Value
  if ($w -match '^(To|Get|Convert|Render|Save|Load|From|As).*' -or $w -match '.*(Png|Jpeg|Jpg|Image|Bitmap|Page|Count|Dpi|Stream|Pdf)$') {
    $hits += $w
  }
}
$hits | Sort-Object -Unique | Select-Object -First 60
