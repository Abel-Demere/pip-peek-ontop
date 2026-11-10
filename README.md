# PIP Peek On Top

Optional Windows x64 support for keeping a PIP Peek Standard window above other apps. This public repository contains only the native helper and pin artwork, not the browser extension.

## Install

Download [PIP-Peek-ontop-Setup.exe](https://github.com/Abel-Demere/pip-peek-ontop/releases/download/v1.0.2/PIP-Peek-ontop-Setup.exe), run it once, then return to PIP Peek and press the pin button. Version 1.0.2 has dedicated pin artwork and Windows version metadata. It is unsigned. Downloading does not install the helper automatically.

Setup discovers PIP Peek in standard Chrome Default/Profile folders. It installs under `%LOCALAPPDATA%\PIP Peek\Native` and registers `com.pippeek.window` under the current user's Chrome NativeMessagingHosts registry key. It preserves existing allowed extension origins. No administrator access, browser restart or startup service is required. Custom Chrome user-data folders require the developer extension-ID option.

Only a validated Chrome/Chromium PIP Peek Player window can be pinned. Changing pin state does not move, resize or activate that window. Browser floating mode does not need this helper.

## Build

Run `powershell -NoProfile -ExecutionPolicy Bypass -File native/Build.ps1`. Output: `tools/out/PIP-Peek-ontop-Setup.exe`. The build uses the Windows .NET Framework C# compiler and embeds `assets/pip-peek-ontop.ico`.

## Unregister

The installed `PeekWindowHost.exe` accepts `--uninstall` to unregister native messaging. Installed files are retained. Original notices are in [LICENSE](LICENSE).
