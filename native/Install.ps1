param([Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string]$ExtensionId)
$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'PIP Peek\Native'
New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$executable = Join-Path $installDirectory 'PeekWindowHost.exe'
& $compiler /nologo /target:winexe /platform:x64 /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll "/win32icon:$(Join-Path $PSScriptRoot '..\assets\pip-peek-ontop.ico')" "/out:$executable" (Join-Path $PSScriptRoot 'PeekWindowHost.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Could not build the PIP Peek window component.' }
$manifestPath = Join-Path $installDirectory 'com.pippeek.window.json'
$allowed = @("chrome-extension://$ExtensionId/")
if (Test-Path -LiteralPath $manifestPath) {
    $oldManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $allowed = @($oldManifest.allowed_origins + $allowed | Select-Object -Unique)
}
$manifest = @{ name='com.pippeek.window'; description='PIP Peek window pinning'; path=$executable; type='stdio'; allowed_origins=$allowed } | ConvertTo-Json
[System.IO.File]::WriteAllText($manifestPath, $manifest, (New-Object System.Text.UTF8Encoding($false)))
$key = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.pippeek.window'
New-Item -Path $key -Force | Out-Null
Set-Item -Path $key -Value $manifestPath
Write-Output 'PIP Peek always-on-top support is ready. Reload the extension and refresh the video page.'
