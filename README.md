# ISM - Internet Speed Monitor

**Speed Monitor** – a DU Meter style network/system meter that draws your live
speeds **inside the Windows taskbar**, just to the left of the notification area:

```
             ↑ 1.24 MB/s   ↓ 340 KB/s                ^  wifi  v  100%   21:34
┌─────────────────────────────────────────────────────────────────────┐
│  taskbar (the readout sits on it, follows it, and never steals focus)│
└─────────────────────────────────────────────────────────────────────┘
```

Written with **WPF on .NET 8** — no admin rights, no driver, no installer. Just
double-click it, or install it properly in one click from the menu (see
*Install & uninstall* below).

## Download

**⬇ [Download ISM-Internet-Speed-Monitor.exe](https://github.com/MeTariqul/ISM-Internet-speed-Monitor/releases/download/v1.1.3/ISM-Internet-Speed-Monitor.exe)** (68 MB, single file)

Or grab it from the [Releases](https://github.com/MeTariqul/ISM-Internet-speed-Monitor/releases)
page, then double-click it — nothing else to install. The .NET runtime and every
dependency are baked in (self-contained, single file), so it works on any 64-bit
Windows 10/11 without the .NET runtime.

## What it shows

| Format (menu → *Taskbar text*) | Readout                                  |
|--------------------------------|------------------------------------------|
| Network (up / down) — default   | `↑ 340 KB/s   ↓ 1.24 MB/s`               |
| Disk (read / write)            | `R 45.2 MB/s   W 12.1 MB/s`              |
| Network + disk                 | `↑1.2M ↓340K R45M W12M` (compact)        |

With *Also show CPU / RAM* enabled, `CPU 12%  RAM 68%` is appended.

## Features

- **Painted on the taskbar** – a borderless, non-activating overlay sized to the
  taskbar height and positioned against the notification area, so it never
  collides with the tray or the clock. It re-checks the taskbar every 300 ms and
  follows it when it moves, resizes, changes edge (top/bottom) or auto-hides
  (the readout hides with it).
- **Reads on any theme** – the taskbar background is sampled and the text
  switches between light (with a soft shadow) and dark automatically.
- **Never steals focus** – `WS_EX_NOACTIVATE` plus per-tick z-order
  re-assertion, so the taskbar lifting itself on click can't bury the readout
  and clicking it never minimises your application.
- **Click or right-click the readout** (or the tray icon) for the settings menu:
  - show/hide the taskbar text
  - text format (network / disk / both) and *Also show CPU / RAM*
  - network units: Auto (B/KB/MB), KB/s, Mbps
  - **network adapter** selection (Auto = physical NICs only, or one specific
    adapter — handy for VPN traffic)
  - *Start with Windows* (HKCU `Run` key)
  - *Install ISM…* / *Uninstall ISM…* (optional, per-user — see below)
  - exit
- **Tray icon** – live speeds in the tooltip; left/right click opens the same
  menu.
- **Settings persistence** – units, format, adapter, visibility and autostart
  are written to `%AppData%\SpeedMonitor\settings.json` **immediately, on every
  menu change** (plus once more on exit), so nothing is lost even if the app is
  force-killed.
- **Single instance** – launching the exe again makes the running copy open its
  menu at the cursor.
- Per-monitor DPI aware (manifest `PerMonitorV2`).

## Install & uninstall (optional)

Double-clicking the exe runs it **portable** — nothing is written outside
`%AppData%`. If you'd rather have it behave like a regular program:

- **Install** – menu → *Install ISM…*, or run
  `ISM-Internet-Speed-Monitor.exe /install`. This copies the exe to
  `%LocalAppData%\Programs\ISM Internet Speed Monitor`, adds a Start Menu
  shortcut and registers **Settings → Apps → ISM - Internet Speed Monitor**.
  It is a per-user install, so no admin prompt ever appears.
- **Uninstall** – menu → *Uninstall ISM…*, Windows
  *Settings → Apps → Uninstall*, or `ISM-Internet-Speed-Monitor.exe
  /uninstall`. Removes the shortcut, the Apps entry, the installed copy and
  your settings.

Both switches accept `/quiet` for a silent, dialog-free run.

## Data sources

All readings come from Windows performance counters
(`System.Diagnostics.PerformanceCounter`), sampled once per second on a
background thread:

- `Network Interface` → `Bytes Received/sec` / `Bytes Sent/sec` (summed, or one adapter)
- `LogicalDisk` `_Total` → `Disk Read Bytes/sec` / `Disk Write Bytes/sec`
- `Processor` `_Total` → `% Processor Time`
- `Memory` → `Available MBytes` (RAM % is derived from total physical memory)

If a counter is unavailable the value shows `—` instead of crashing.

## Build & run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build -c Release
.\bin\Release\net8.0-windows\ISM-Internet-Speed-Monitor.exe
```

With a user-scope SDK, launch from a shell where `DOTNET_ROOT` points at your
dotnet folder.

### Publish the single-file .exe (what users download)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`publish\ISM-Internet-Speed-Monitor.exe` is the one and only file users need —
the .NET runtime and every dependency are inside it.

## Layout

```
App.xaml(.cs)                  startup, single-instance signal, tray wiring, exit
TaskbarWindow.xaml(.cs)        the overlay: placement, z-order, theme sampling
Services/TaskbarMenu.cs        the shared settings menu (WinForms, hosts in tray)
Services/TaskbarLocator.cs     finds the taskbar + notification area rectangles
Services/TrayIconService.cs    notification-area icon + tooltip
Services/AppSettings.cs        JSON settings + autostart registry
Services/SelfInstall.cs        optional install / uninstall (shortcut + Apps entry)
Services/NativeMethods.cs      Win32 interop (SetWindowPos, styles, enumeration)
ViewModels/MonitorViewModel.cs formats the readout texts for the bindings
Metrics/MetricsSampler.cs      1 Hz sampling loop over performance counters
Metrics/SpeedFormat.cs         unit formatting (Auto / KB / Mbps)
Metrics/Sample.cs              one reading
```

The earlier v1.0 implementation (window + settings dialog) is preserved in
`legacy/` together with its solution and old release binary.

## Troubleshooting

- **"You must install .NET"** – the app is framework-dependent; install the
  .NET 8 runtime or publish self-contained (above).
- **No text on the taskbar** – menu → *Show text in taskbar*. If the taskbar is
  auto-hidden the readout appears with it.
- **Text overlapping the tray** – some third-party trays are unusually wide;
  the readout is positioned from the real `TrayNotifyWnd` rectangle and falls
  back to a 220 px estimate when it can't be found.
- **Adapter shows `—`** – menu → *Network adapter* → *Auto* (or pick one
  explicitly).
- **VPN traffic looks doubled** – *Auto* mode skips virtual/VPN adapters so the
  physical NIC is not counted twice; select the VPN adapter explicitly to
  monitor it.
- **Crash diagnostics** – appended to `%AppData%\SpeedMonitor\error.log`.

## About / Credits

**ISM - Internet Speed Monitor (Speed Monitor)** was created by
**[MeTariqul](https://github.com/MeTariqul)**.

- Author: [MeTariqul](https://github.com/MeTariqul)
- Project: <https://github.com/MeTariqul/ISM-Internet-speed-Monitor>

Free and open source — use it, modify it and share it. If you redistribute it,
please keep this credit. The same credit is shown in the app itself under
*menu → About / Credits* and in the .exe file properties.
