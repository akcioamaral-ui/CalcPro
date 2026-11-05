# Desfaz o que definir-padrao.ps1 fez (a tecla Calculadora volta a abrir a calculadora do Windows).
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\AppKey\18' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\calcpro.exe' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'CalcPro.lnk') -Force -ErrorAction SilentlyContinue
Write-Host "[ok] CalcPro removido como calculadora padrao."
