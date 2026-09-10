$dll = "C:\Users\USER\.nuget\packages\pdftoimage\3.0.0\lib\net8.0\PDFtoImage.dll"
$bytes = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
$hits = @()
foreach ($m in [regex]::Matches($text, '[A-Za-z][A-Za-z0-9_]{3,50}')) {
  $w = $m.Value
  if ($w -match 'Render|SaveImage|ToImage|GetImage|Bitmap|Stream|PageAs|Thumbnail|Export') {
    $hits += $w
  }
}
$hits | Sort-Object -Unique
