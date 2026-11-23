<div align="center">

<img src="App/Assets/AuraLite.png" width="96" alt="AuraLite icon" />

# AuraLite

A native Windows 11 RGB controller for the ASUS TUF Gaming B850-Plus WiFi.

[Download for Windows](https://github.com/Xploder/AuraLite/releases/latest) · [Build from source](#build-from-source) · [HID protocol notes](PROTOKOLL.md)

<img src="docs/auralite.jpg" width="520" alt="AuraLite in dark mode with its custom title bar, live controller status, color picker, lighting controls, profiles and language selector" />

</div>

## Features

- Native WinUI 3 window with a Mica backdrop, custom title bar, and per-monitor DPI awareness.
- ARGB channel selection, static color and brightness, and firmware-driven color cycle and rainbow modes.
- Local profiles, optional launch at Windows sign-in, and a notification-area icon.
- Automatic controller detection at launch and live updates for color, effect, brightness, and LED count.
- English, German, Spanish, French, and Portuguese. AuraLite follows the Windows display language by default. The language menu changes the interface immediately and remembers your choice.
- Direct access to the existing Windows HID interface. No separate RGB service, driver installation, OpenRGB, or Armoury Crate required.

## Getting started

1. Download and run **AuraLite.exe** on Windows 11 x64. The single executable includes the .NET and Windows App SDK runtimes and can be renamed.
2. AuraLite detects the controller automatically. The **Detect controller** button is available if you need to try again.
3. Select an ARGB channel, then adjust the color, effect, brightness, or LED count. AuraLite applies the changes as you edit. A burst of slider or picker input is grouped into one hardware update per affected channel. Firmware effects use the controller's own brightness.

Minimizing or closing the window keeps AuraLite running in the Windows notification area. Click the AuraLite icon to reopen it, or right-click it and choose **Quit** to exit.

Profiles are stored in `%LOCALAPPDATA%\AuraLite\profiles.json`. Selecting a profile applies its channel, LED count, color, and effect when the controller is available. By default, the last selected profile loads into the editor at launch. Use **Startup profile** to choose a fixed profile instead. The **Restore lighting at launch and after sleep** checkbox is off by default. When enabled, AuraLite applies the startup profile after detecting the controller. After sleep, it restores the last lighting it successfully sent to the matching controller. For wake events, leave AuraLite running or minimized. To reapply after a Windows restart without opening the app manually, also enable **Open AuraLite at Windows sign-in**. The controller on the tested board falls back to its rainbow effect across sleep or reboot, so this is a software restore.

## Hardware validation

| Item | Result |
| --- | --- |
| Tested system | ASUS TUF Gaming B850-Plus WiFi, Windows 11 x64 |
| Controller | ASUS USB `0B05:19AF`, HID interface `MI_02` |
| Firmware | `AULA3-AR32-0309`, three ARGB channels detected |
| Static lighting | One pixel confirmed on ARGB 1 |
| Color cycle | Confirmed on ARGB 1, including after AuraLite exited |
| Controller state after sleep or reboot | Reported to fall back to rainbow |

Before applying changes, AuraLite rechecks the detected controller and uses the documented lighting reports. Hardware writes are serialized, with no continuous refresh loop or flash-save command.

### Command-line probe

The optional developer probe can be built from source and run from PowerShell. `list` inspects Windows HID metadata. `status` reads firmware and channel counts.

```powershell
.\AuraLite.Probe.exe list
.\AuraLite.Probe.exe status
.\AuraLite.Probe.exe static 1 102030 1
.\AuraLite.Probe.exe effect 1 cycle
```

The last two commands are optional hardware tests. Review the report bytes, then type `SENDEN` in the interactive probe to authorize transmission. To check persistence, compare the observed lighting after exiting AuraLite, after a normal restart, and after fully disconnecting power.

## Build from source

Requires the .NET 10 SDK on Windows x64. Restore uses NuGet.

```powershell
dotnet run --project .\Tests\AuraLite.Tests.csproj -c Release
dotnet publish .\App\AuraLite.csproj -c Release -o .\publish\AuraLite
dotnet build .\Probe\AuraLite.Probe.csproj -c Release
```

The protocol tests exercise captured packet layouts using synthetic frames and do not access a controller. See the [protocol and reference license notes](PROTOKOLL.md) and [runtime notices](licenses/).
