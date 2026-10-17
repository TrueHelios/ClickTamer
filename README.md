# ClickTamer 1.0

ClickTamer keeps a silent WASAPI audio stream open to help reduce clicks that can occur on some DACs when the sample rate changes.

## Features

- Silent audio playback with automatic recovery if the stream stops.
- Automatic refresh of the device list when Windows reports an audio-device change.
- Manual device refresh as a fallback.
- Light and dark themes, with Russian and English UI.
- Optional startup with Windows and launch-to-tray behavior.
- Remembers the selected device and suppression state.

## Requirements

- Windows 10 or later.
- .NET 10 SDK to build the project.

## Build

Open this folder in a terminal and run:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The executable is written to `bin\Release\net10.0-windows\win-x64\publish\ClickTamer.exe`.

## Files and settings

- Settings: `%APPDATA%\ClickTamer\config.json`
- Log: `%LOCALAPPDATA%\ClickTamer\startup.log`
- Application icon: `ClickTamer.ico`
- Source images: `assets/ClickTamer.png` and `assets/ClickTamer-1024.png`

On first launch, suppression and both startup options are off. The app saves each choice when you change it and uses only its own settings and log files.

## Startup behavior

The startup option creates a shortcut in the current user's Windows Startup folder; it does not copy the executable. If the application is moved, run it once from the new location so the shortcut can be updated.

The “Start minimized” setting applies to both manual launches and Windows startup. When enabled, the main window stays hidden until opened from the tray.

## Version

The application version is `1.0`. Version fields are set in `ClickTamer.csproj`.
## Theme

On first launch, ClickTamer follows the Windows app theme. The theme follows Windows until you switch it manually; after that, the selected theme is saved and stays in use.
