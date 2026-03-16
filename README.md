# ISM - Internet Speed Monitor

A lightweight, Windows 11-compatible internet speed monitor that displays real-time download and upload speeds in a compact taskbar-style window.

## Download

### Latest Release
- **[ISM_v1.0.exe (~63 MB)](releases/ISM/InternetSpeedMonitor.exe)** - Self-contained executable (no .NET runtime required)

> Simply download and run! Works on Windows 10/11.

## Features

- 📊 **Real-time Speed Monitoring** - Displays current download and upload speeds
- 🎨 **Modern Dark Theme** - Sleek, professional appearance
- 🔧 **Customizable Settings** - Adjust transparency, size, refresh rate, and more
- 📌 **Always on Top** - Keep the speed monitor visible
- 🔒 **Lock Position** - Prevent accidental repositioning
- 🚀 **Start with Windows** - Auto-start when you log in
- 💾 **System Tray** - Minimize to tray for minimal resource usage

## Requirements

- Windows 10/11
- No additional requirements (self-contained build)

## Installation

### Option 1: Download Release (Recommended)
Simply download `ISM_v1.0.exe` from the [releases folder](releases/ISM/) and run it.

### Option 2: Build from Source
Requires .NET 8.0 SDK:

```bash
# Framework-dependent (~1.8 MB, requires .NET 8)
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish

# Self-contained (~63 MB, no runtime needed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./releases/ISM
```

## Usage

1. **Run the application** - Double-click `InternetSpeedMonitor.exe`
2. **Right-click** on the speed monitor to access the context menu:
   - Settings - Customize appearance and behavior
   - Show/Hide - Toggle visibility
   - Exit - Close the application
3. **Drag** the window to reposition (if not locked)
4. **Resize** using the edges (if not locked)

## Settings

| Setting | Description |
|---------|-------------|
| Always on Top | Keep window above all other windows |
| Lock Window Position | Prevent moving/resizing |
| Start Minimized | Start in system tray |
| Start with Windows | Auto-start when Windows boots |
| Hide to Tray on Close | Minimize to tray instead of exiting |
| Transparency | Window opacity (10%-100%) |
| Background Opacity | Background transparency |
| Corner Radius | Window corner roundness |
| Unit | Mbps (bits) or MB/s (bytes) |
| Refresh Rate | Update frequency (0.5s - 5s) |

## Building from Source

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build Debug
dotnet build

# Build Release
dotnet build -c Release

# Publish (Framework-dependent, ~1.8 MB)
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish

# Publish (Self-contained, ~63 MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./releases/ISM
```

## File Structure

```
InternetSpeedMonitor/
├── App.xaml                 # Application entry
├── App.xaml.cs              # Application code
├── MainWindow.xaml          # Main speed monitor UI
├── MainWindow.xaml.cs       # Main window logic
├── SettingsWindow.xaml      # Settings dialog UI
├── SettingsWindow.xaml.cs  # Settings logic
├── Services/
│   └── Settings.cs          # Settings management
├── AssemblyInfo.cs          # Assembly metadata
├── InternetSpeedMonitor.csproj # Project file
└── ISM.ico                  # Application icon
```

## Configuration

Settings are stored in:
```
%APPDATA%\InternetSpeedMonitor\settings.json
```

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
