# Compila o CalcPro com o compilador C# que já vem no Windows (.NET Framework 4.x)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null
if (-not (Test-Path "$root\src\app.ico")) { & "$root\tools\make-icon.ps1" }

$src = @('Engine.cs', 'Format.cs', 'Controls.cs', 'MainForm.cs', 'Program.cs') | ForEach-Object { Join-Path "$root\src" $_ }
& $csc /nologo /target:winexe /optimize+ /utf8output /codepage:65001 `
    "/out:$out\CalcPro.exe" "/win32icon:$root\src\app.ico" "/win32manifest:$root\src\app.manifest" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll @src
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação" }

# testes do motor
New-Item -ItemType Directory -Force "$root\build" | Out-Null
& $csc /nologo /target:exe /codepage:65001 /r:System.Core.dll "/out:$root\build\Tests.exe" "$root\src\Engine.cs" "$root\src\Format.cs" "$root\tests\Tests.cs"
if ($LASTEXITCODE -eq 0) { & "$root\build\Tests.exe" }
Write-Host "Pronto: $out\CalcPro.exe"
