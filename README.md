# PawDesk

PawDesk is a small personal-use Windows desktop pet tool built with .NET 8 and WPF.

This is a hobby project I made for my own desktop. It is public mainly for backup and sharing, not a polished product or commercial app.

## Current Features

- Transparent borderless desktop pet window
- Default built-in pet when no image is configured
- Drag to move
- Mouse wheel zoom
- Position and size persistence
- Right-click menu
- System tray menu
- Settings window
- Transparent PNG import copied into `%APPDATA%\PawDesk\pets`
- Built-in local background removal for ordinary PNG/JPG/WebP images through ONNX Runtime
- Breathing animation
- Click bounce animation
- Random sway
- Low-frequency mouse-near reaction
- Small bounded random movement
- Current-user startup toggle through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Basic error logging under `%APPDATA%\PawDesk\logs`

## Not Implemented Yet

- Multi-pet display
- Cloud sync or account system

Transparent PNG files are used directly. Ordinary images are processed locally with the bundled `silueta.onnx` model; no image is uploaded to a server.

## Build

From this directory:

```powershell
..\.dotnet\dotnet.exe build PawDesk.sln
```

## Test

```powershell
..\.dotnet\dotnet.exe test PawDesk.sln
```

## Run

```powershell
..\.dotnet\dotnet.exe run --project src\PawDesk.App\PawDesk.App.csproj
```

For quick local testing, run the published executable directly instead of downloading it again:

```powershell
src\PawDesk.App\bin\Release\net8.0-windows\win-x64\publish\PawDesk.App.exe
```

The app stores user data in:

```text
%APPDATA%\PawDesk\
```

## Publish

```powershell
..\.dotnet\dotnet.exe publish src\PawDesk.App\PawDesk.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## Versioning

This repository uses simple Git tags for releases:

- `v0.1.x` for personal test builds
- Create a new tag and GitHub Release for each shared exe
- Do not overwrite old release assets except for a mistaken upload

The output is written under:

```text
src\PawDesk.App\bin\Release\net8.0-windows\win-x64\publish\
```

The release output contains `PawDesk.App.exe`. The ONNX model is embedded in the executable and is extracted on first use to:

```text
%APPDATA%\PawDesk\models\silueta.onnx
```

## Known Issues

- Random movement currently uses the primary work area for screen bounds.
- Tray startup checkbox state is initialized when the tray menu is created.
- Background removal quality depends on the image. Busy backgrounds, similar foreground/background colors, and fluffy edges can need manual cleanup.
