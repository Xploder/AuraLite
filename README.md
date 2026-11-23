<div align="center">

<img src="App/Assets/AuraLite.png" width="96" alt="AuraLite icon" />

# AuraLite

A native Windows 11 ARGB controller for compatible ASUS Aura USB motherboards.

[Download for Windows](https://github.com/Xploder/AuraLite/releases/latest) · [Build from source](#build-from-source) · [Hardware compatibility](docs/COMPATIBILITY.md)

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
2. AuraLite detects the controller automatically. Use **Detect controller** at the bottom to retry.
3. Select an ARGB channel, then adjust the color, effect, brightness, or LED count. AuraLite applies the changes as you edit. A burst of slider or picker input is grouped into one hardware update per affected channel. Firmware effects use the controller's own brightness.

Minimizing or closing the window keeps AuraLite running in the Windows notification area. Click the AuraLite icon to reopen it, or right-click it and choose **Quit** to exit.

Choose **New** to name and save the current settings. To update or delete the selected profile, open the profile actions menu. Profiles are stored in `%LOCALAPPDATA%\AuraLite\profiles.json`. Selecting a profile applies its channel, LED count, color, and effect when the controller is available. By default, the last selected profile loads into the editor at launch. Use **Startup profile** to choose a fixed profile instead. The **Restore lighting at launch and after sleep** checkbox is off by default. When enabled, AuraLite applies the startup profile after detecting the controller. After sleep, it restores the last lighting it successfully sent to the matching controller. For wake events, leave AuraLite running or minimized. To reapply after a Windows restart without opening the app manually, also enable **Open AuraLite at Windows sign-in**. The controller on the tested board falls back to its rainbow effect across sleep or reboot, so this is a software restore.

## Hardware compatibility

AuraLite supports compatible ASUS Aura USB HID controllers in the `0B05:19AF` family. It checks the HID report format, firmware and channel configuration when detecting a device. A motherboard advertised with Aura Sync can use a different controller. The compatibility guide lists reported ASUS models, related USB IDs, and a Windows check you can run yourself. [Compatibility and hardware IDs](docs/COMPATIBILITY.md)

Static ARGB lighting and the hardware color cycle were verified with firmware `AULA3-AR32-0309`. Firmware effects may differ across boards. AuraLite rechecks the detected controller before applying a change and does not use flash-save commands.

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
