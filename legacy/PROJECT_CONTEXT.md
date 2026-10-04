# ISM - Internet Speed Monitor
# Project Context Document
# Last Updated: 2026-03-16

================================================================================
PROJECT OVERVIEW
================================================================================

Project Name: ISM - Internet Speed Monitor
Type: Windows Desktop Application (.NET 8 WPF)
Version: 1.0.0
Description: A lightweight, Windows 11-compatible internet speed monitor that 
              displays real-time download and upload speeds in a compact 
              taskbar-style window.

GitHub Repository: https://github.com/MeTariqul/ISM-Internet-speed-Monitor

================================================================================
FEATURES
================================================================================

✓ Real-time Speed Monitoring - Download and Upload speeds
✓ Modern Dark Theme - Sleek, professional appearance
✓ Customizable Settings:
  - Transparency (10%-100%)
  - Background Opacity
  - Corner Radius
  - Window Size
  - Font Size
  - Refresh Rate (0.5s, 1s, 2s, 5s)
  - Display Unit (Mbps/MBps)
  - Theme Mode (Dark/Light/System)
✓ Always on Top option
✓ Lock Window Position
✓ Start with Windows
✓ Start Minimized to Tray
✓ Hide to Tray on Close
✓ System Tray Support
✓ Settings saved to %APPDATA%\InternetSpeedMonitor\settings.json

================================================================================
FILE STRUCTURE
================================================================================

InternetSpeedMonitor/
├── InternetSpeedMonitor.sln           # Solution file
├── README.md                         # Documentation
├── PROJECT_CONTEXT.md                # This file
├── .gitignore                       # Git ignore rules
│
├── InternetSpeedMonitor/             # Main project folder
│   ├── App.xaml                     # Application entry XAML
│   ├── App.xaml.cs                   # Application code (startup, exception handling)
│   ├── MainWindow.xaml               # Main speed monitor UI
│   ├── MainWindow.xaml.cs             # Main window logic (speed monitoring, tray)
│   ├── SettingsWindow.xaml            # Settings dialog UI
│   ├── SettingsWindow.xaml.cs         # Settings logic
│   ├── AssemblyInfo.cs               # Assembly metadata
│   ├── InternetSpeedMonitor.csproj   # Project file
│   ├── ISM.ico                       # Application icon
│   │
│   ├── Services/
│   │   ├── NetworkSpeedService.cs    # Network speed monitoring service
│   │   └── Settings.cs               # Settings management (load/save JSON)
│   │
│   └── Properties/
│       └── PublishProfiles/
│           ├── FrameworkDependent.pubxml
│           └── SelfContained.pubxml
│
└── releases/                         # Pre-built executables
    └── InternetSpeedMonitor.exe      # Self-contained (~63 MB)

================================================================================
KEY FILES AND THEIR PURPOSES
================================================================================

1. App.xaml.cs
   - Application entry point
   - Startup logic
   - Global exception handling
   - Single instance enforcement (mutex)

2. MainWindow.xaml/cs
   - Main speed monitor display
   - Network speed calculation
   - System tray icon
   - Context menu
   - Window dragging/resizing
   - Settings application

3. SettingsWindow.xaml/cs
   - Settings dialog UI
   - Checkbox/slider handlers for live preview
   - Save/Cancel functionality

4. Services/NetworkSpeedService.cs
   - Uses System.Net.NetworkInformation.NetworkInterface
   - Calculates bytes sent/received
   - Publishes SpeedUpdated events

5. Services/Settings.cs
   - AppSettings class with all settings properties
   - Load() - reads from JSON file
   - Save() - writes to JSON file
   - Default values defined

================================================================================
SETTINGS STRUCTURE
================================================================================

Location: %APPDATA%\InternetSpeedMonitor\settings.json

Settings Properties:
- WindowLeft, WindowTop (double) - Window position
- WindowWidth, WindowHeight (double) - Window size  
- FontSize (double) - Font size
- RefreshIntervalMs (int) - Update interval in ms
- UseBitsPerSecond (bool) - true=Mbps, false=MB/s
- ThemeMode (int) - 0=Dark, 1=Light, 2=System
- Opacity (double) - Window opacity
- BackgroundOpacity (double) - Background transparency
- CornerRadius (double) - Corner roundness
- IsPositionLocked (bool) - Lock window position
- AlwaysOnTop (bool) - Always on top
- StartMinimized (bool) - Start minimized
- StartWithWindows (bool) - Auto-start with Windows
- HideOnClose (bool) - Minimize to tray on close

================================================================================
BUILD INSTRUCTIONS
================================================================================

Prerequisites:
- .NET 8.0 SDK
- Windows 10/11

Commands:

# Restore dependencies
dotnet restore

# Build Debug
dotnet build

# Build Release
dotnet build -c Release

# Publish Self-Contained (Recommended - no runtime needed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./releases

# Publish Framework-Dependent (Smaller but requires .NET 8)
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish

================================================================================
DEPENDENCIES
================================================================================

NuGet Packages:
- H.NotifyIcon.Wpf (v2.1.3) - System tray icon support

================================================================================
VERSION HISTORY
================================================================================

v1.0.0 (2026-03-16)
- Initial release
- Real-time download/upload speed monitoring
- Dark theme UI
- Customizable settings
- System tray support
- Self-contained executable

================================================================================
NOTES FOR DEVELOPERS
================================================================================

1. When modifying settings, update this context file
2. Settings behavior: Changes apply immediately (live preview), but are only
   saved to file when user clicks Save button
3. No auto-save when moving/resizing window - only save on explicit Save
4. The .exe file is ~63 MB (self-contained) or ~1.8 MB (framework-dependent)
5. Use H.NotifyIcon.Wpf for system tray functionality
6. Network speed is calculated using NetworkInterface.GetIPv4Statistics()

================================================================================
