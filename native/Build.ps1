param([string]$OutputPath = (Join-Path $PSScriptRoot '..\tools\out\PIP-Peek-ontop-Setup.exe'))
$ErrorActionPreference = 'Stop'
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll "/win32icon:$(Join-Path $PSScriptRoot '..\assets\pip-peek-ontop.ico')" "/out:$OutputPath" (Join-Path $PSScriptRoot 'PeekWindowHost.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Could not build PIP Peek Setup.' }
Write-Output $OutputPath
