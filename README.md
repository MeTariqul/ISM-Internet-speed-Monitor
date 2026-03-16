# ISM - Internet Speed Monitor

A lightweight, Windows 11-compatible internet speed monitor that displays real-time download and upload speeds in a compact taskbar-style window. Similar to TrafficMonitor but focused specifically on network speed monitoring.

---

## 📥 Download

### Latest Release (v1.0.0)
| Version | Size | Requirements |
|---------|------|--------------|
| [ISM_v1.0.0.exe](releases/InternetSpeedMonitor.exe) | ~63 MB | None (self-contained) |

**Direct Download:** Click the link above or go to `releases/` folder and download `InternetSpeedMonitor.exe`

---

## ✨ Features

- 📊 **Real-time Speed Monitoring** - Displays current download and upload speeds
- 🎨 **Modern Dark Theme** - Sleek, professional appearance  
- 🔧 **Fully Customizable** - Adjust transparency, size, refresh rate, colors, and more
- 📌 **Always on Top** - Keep the speed monitor visible above all windows
- 🔒 **Lock Position** - Prevent accidental repositioning
- 🚀 **Start with Windows** - Auto-start when you log in
- 💾 **System Tray** - Minimize to tray for minimal resource usage
- 🎯 **Compact Design** - Small footprint, taskbar-friendly

---

## 🖥️ System Requirements

- **Operating System:** Windows 10 or Windows 11
- **Runtime:** None required (self-contained build)
- **Disk Space:** ~70 MB

---

## 🚀 Quick Start

1. **Download** the `InternetSpeedMonitor.exe` from the [releases folder](releases/)
2. **Run** the application (double-click)
3. **Done!** The speed monitor will appear on your screen

---

## 📖 User Guide

### Context Menu (Right-Click)
Right-click on the speed monitor to access:
- **Settings** - Customize appearance and behavior
- **Show** - Display the window
- **Hide** - Hide the window to tray
- **Exit** - Close the application

### Moving the Window
- **Drag** the window to reposition it
- If **Lock Position** is enabled in settings, dragging is disabled

### Customizing
Open **Settings** to customize:
- Transparency and opacity
- Window size
- Refresh rate
- Display units (Mbps/MB/s)
- Theme (Dark/Light/System)
- Corner radius

---

## ⚙️ Settings Details

| Setting | Description | Default |
|---------|-------------|---------|
| Always on Top | Keep window above all other windows | Enabled |
| Lock Window Position | Prevent moving/resizing | Enabled |
| Start Minimized | Start in system tray | Disabled |
| Start with Windows | Auto-start when Windows boots | Disabled |
| Hide to Tray on Close | Minimize to tray instead of exiting | Enabled |
| Transparency | Window opacity (10%-100%) | 100% |
| Background Opacity | Background transparency | 100% |
| Corner Radius | Window corner roundness | 8px |
| Unit | Display format: Mbps (bits) or MB/s (bytes) | Mbps |
| Refresh Rate | Update frequency | 1 second |

---

## 🔨 Build from Source

### Prerequisites
- .NET 8.0 SDK
- Windows 10/11
- Visual Studio 2022 or VS Code (optional)

### Build Commands

```bash
# Clone the repository
git clone https://github.com/MeTariqul/ISM-Internet-speed-Monitor.git
cd ISM-Internet-speed-Monitor

# Restore dependencies
dotnet restore

# Build Debug version
dotnet build

# Build Release version
dotnet build -c Release
```

### Publishing

#### Option 1: Self-Contained (Recommended for distribution)
```bash
# Creates single .exe with all dependencies (~63 MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./releases
```

#### Option 2: Framework-Dependent (Smaller size)
```bash
# Creates small .exe but requires .NET 8 runtime (~1.8 MB)
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

---

## 📁 Project Structure

```
ISM-Internet-speed-Monitor/
├── releases/                    # Pre-built executables
│   └── InternetSpeedMonitor.exe
├── InternetSpeedMonitor/         # Source code
│   ├── App.xaml                  # Application entry
│   ├── App.xaml.cs               # Application code
│   ├── MainWindow.xaml           # Main UI
│   ├── MainWindow.xaml.cs        # Main window logic
│   ├── SettingsWindow.xaml       # Settings dialog
│   ├── SettingsWindow.xaml.cs    # Settings logic
│   ├── Services/
│   │   ├── NetworkSpeedService.cs # Network monitoring
│   │   └── Settings.cs            # Settings management
│   ├── ISM.ico                   # Application icon
│   └── InternetSpeedMonitor.csproj
├── Internet speed monitor.sln    # Solution file
├── .gitignore                    # Git ignore rules
└── README.md                     # This file
```

---

## 📝 Configuration

Settings are stored in:
```
%APPDATA%\InternetSpeedMonitor\settings.json
```

You can manually edit this file to change settings, but it's recommended to use the Settings dialog within the application.

---

## 🤝 Contributing

Contributions are welcome! Please feel free to:
- Submit bug reports
- Request new features
- Create pull requests

---

## 📄 License

MIT License - See LICENSE file for details.

---

## 🙏 Acknowledgments

Inspired by [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) - A similar system monitoring application.

---

## 📞 Support

If you encounter any issues or have questions:
1. Check the Issues page
2. Create a new issue with details

---

**Version:** 1.0.0  
**Last Updated:** 2026-03-16
