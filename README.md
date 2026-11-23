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
- Local profiles and optional launch at Windows sign-in.
- English, German, Spanish, French, and Portuguese. AuraLite follows the Windows display language by default. The language menu changes the interface immediately and remembers your choice.
- Direct access to the existing Windows HID interface. No separate RGB service, driver installation, OpenRGB, or Armoury Crate required.

## Getting started

1. Download and extract **AuraLite-Windows-x64.zip**. Keep its files together and run **AuraLite.exe**. The ZIP includes the .NET and Windows App SDK runtimes.
2. Select **Detect controller** to read the controller firmware and channel configuration.
3. Select an ARGB channel. For static lighting, set the color, brightness, and number of LEDs on that output.
4. Select **Review reports and apply**. AuraLite shows the exact HID output reports for your approval.

Profiles are stored in `%LOCALAPPDATA%\AuraLite\profiles.json`. Loading a profile fills the controls. Launching at sign-in opens the app. Applying a profile to hardware still requires approval.

## Hardware validation

| Item | Result |
| --- | --- |
| Tested system | ASUS TUF Gaming B850-Plus WiFi, Windows 11 x64 |
| Controller | ASUS USB `0B05:19AF`, HID interface `MI_02` |
| Firmware | `AULA3-AR32-0309`, three ARGB channels detected |
| Static lighting | One pixel confirmed on ARGB 1 |
| Color cycle | Confirmed on ARGB 1, including after AuraLite exited |
| Rainbow and reboot persistence | Pending live verification |

AuraLite uses the documented firmware and configuration requests before offering a lighting change. State-changing reports are gated by a preview. The app does not send flash-save commands. A successful write confirms delivery through Windows, and the lighting itself provides visual confirmation.

### Command-line probe

Run the separately downloadable probe from PowerShell. `list` inspects Windows HID metadata. `status` reads firmware and channel counts.

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
