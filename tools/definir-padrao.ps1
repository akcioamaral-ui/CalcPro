# Define o CalcPro como calculadora do usuario atual (sem precisar de administrador).
#  - Tecla "Calculadora" do teclado abre o CalcPro
#  - Atalho no Menu Iniciar com tecla de atalho Ctrl+Alt+C
#  - Win+R -> calcpro
# Para desfazer: tools\remover-padrao.ps1
param([string]$Exe = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\CalcPro.exe'))
$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path

# 1) Tecla Calculadora (APPCOMMAND_LAUNCH_APP2 = AppKey 18)
$k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\AppKey\18'
New-Item -Path $k -Force | Out-Null
Set-ItemProperty -Path $k -Name 'ShellExecute' -Value $Exe
Write-Host "[ok] Tecla Calculadora -> $Exe"

# 2) Win+R "calcpro"
$ap = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\calcpro.exe'
New-Item -Path $ap -Force | Out-Null
Set-ItemProperty -Path $ap -Name '(default)' -Value $Exe
Set-ItemProperty -Path $ap -Name 'Path' -Value (Split-Path $Exe)
Write-Host "[ok] Win+R -> calcpro"

# 3) Atalho no Menu Iniciar com Ctrl+Alt+C
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'CalcPro.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk)
$s.TargetPath = $Exe
$s.WorkingDirectory = Split-Path $Exe
$s.IconLocation = "$Exe,0"
$s.Description = 'CalcPro - Calculadora Cientifica'
$s.Hotkey = 'CTRL+ALT+C'
$s.Save()
Write-Host "[ok] Menu Iniciar: $lnk  (atalho Ctrl+Alt+C)"
