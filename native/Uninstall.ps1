$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.pippeek.window'
if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
# Keep files for review; only unregister this component. No app data is removed.
Write-Output 'PIP Peek window pinning has been unregistered.'
