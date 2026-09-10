$dll = "C:\Users\USER\.nuget\packages\pdftoimage\3.0.0\lib\net8.0\PDFtoImage.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
try { $types = $asm.GetTypes() }
catch {
  $types = $_.Exception.Types | Where-Object { $_ -ne $null }
}
foreach ($t in $types) {
  if ($t -ne $null -and $t.IsPublic) {
    Write-Output ("TYPE: {0}" -f $t.FullName)
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($m in $t.GetMethods($flags)) {
      if ($m.IsSpecialName) { continue }
      $ps = ($m.GetParameters() | ForEach-Object { ("{0} {1}" -f $_.ParameterType.Name, $_.Name) }) -join ", "
      Write-Output ("  {0}({1}) : {2}" -f $m.Name, $ps, $m.ReturnType.Name)
    }
  }
}
