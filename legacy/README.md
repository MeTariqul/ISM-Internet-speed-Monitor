# legacy/ - frozen v1.0 reference implementation

This folder contains the original `InternetSpeedMonitor` project. It is kept only as a
historical reference and is **not part of the active WPF solution**.

- `SpeedMonitor.sln` (repository root) intentionally does **not** include this project.
- `SpeedMonitor.csproj` removes `legacy/**` from every item group, so the active app can
  never accidentally compile or link the old code.
- Do not add new features here. Port anything still useful into `Services/`, `Metrics/`,
  or `ViewModels/` in the active project instead.

To inspect the old build in isolation:

```powershell
dotnet build "legacy\Internet speed monitor.sln"
```
