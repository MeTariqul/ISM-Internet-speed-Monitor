using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SpeedMonitor.Services;

/// <summary>
/// Optional per-user install: copies the exe to %LocalAppData%\Programs, creates a Start Menu
/// shortcut and registers a "Settings → Apps" uninstall entry. Everything is HKCU-based, so no
/// admin rights are ever needed. The exe stays fully portable - installing is opt-in and
/// uninstalling removes every trace it created (including the autostart entry when it points
/// at the installed copy).
/// </summary>
public static class SelfInstall
{
    public const string AppExeName = "ISM-Internet-Speed-Monitor.exe";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ISMSpeedMonitor";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SpeedMonitor";
    private const string ShortcutFileName = "ISM - Internet Speed Monitor.lnk";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "ISM Internet Speed Monitor");

    public static string InstalledExe => Path.Combine(InstallDir, AppExeName);

    public static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Windows\Start Menu\Programs",
        ShortcutFileName);

    /// <summary>Path autostart should point at: the installed copy when present, otherwise the running file.</summary>
    public static string TargetPath =>
        File.Exists(InstalledExe) ? InstalledExe : (Environment.ProcessPath ?? InstalledExe);

    public static bool IsInstalled =>
        File.Exists(InstalledExe) || Registry.CurrentUser.OpenSubKey(UninstallKey) is not null;

    public static bool IsRunningFromInstall =>
        Environment.ProcessPath is { } me && PathsEqual(me, InstalledExe);

    // ------------------------------------------------------------------ install

    public static (bool Ok, string Message) Install()
    {
        try
        {
            string source = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine the path of the running exe.");

            Directory.CreateDirectory(InstallDir);

            if (!PathsEqual(source, InstalledExe))
            {
                // A previous copy may still be running (update/reinstall) - stop it so it isn't locked.
                StopInstancesRunning(InstalledExe);
                File.Copy(source, InstalledExe, overwrite: true);
            }

            CreateShortcut(ShortcutPath, InstalledExe);
            WriteUninstallEntry();
            AppSettings.SuppressSave = false; // a previous uninstall in this session must not block future saves
            return (true, InstalledExe);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ---------------------------------------------------------------- uninstall

    public static (bool Ok, string Message) Uninstall(bool deleteSettings)
    {
        try
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);

            // Only clear autostart if it currently points at the installed copy.
            using (RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                if (run?.GetValue(RunValueName) is string current && PathsEqual(current.Trim('"'), InstalledExe))
                {
                    run.DeleteValue(RunValueName, throwOnMissingValue: false);
                }
            }

            if (IsRunningFromInstall)
            {
                // We ARE the installed copy: delete ourselves right after this process exits.
                ScheduleSelfDelete();
            }
            else
            {
                StopInstancesRunning(InstalledExe);
                try
                {
                    if (File.Exists(InstalledExe)) File.Delete(InstalledExe);
                    if (Directory.Exists(InstallDir)) Directory.Delete(InstallDir, recursive: false);
                }
                catch (IOException)
                {
                    /* locked by something else - only the shortcut/entry are gone, which is what matters */
                }
            }

            if (deleteSettings)
            {
                AppSettings.SuppressSave = true;
                try
                {
                    string settings = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SpeedMonitor");
                    if (File.Exists(AppSettings.FilePath)) File.Delete(AppSettings.FilePath);
                    if (Directory.Exists(settings) && !Directory.EnumerateFileSystemEntries(settings).Any())
                        Directory.Delete(settings, recursive: false);
                }
                catch (IOException) { /* not fatal */ }
            }

            return (true, IsRunningFromInstall ? "self" : "ok");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void WriteUninstallEntry()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey)
            ?? throw new InvalidOperationException("Cannot create the uninstall registry key.");

        string version = (typeof(SelfInstall).Assembly.GetName().Version ?? new Version(1, 0)).ToString(3);
        long sizeKb = new FileInfo(InstalledExe).Length / 1024;

        key.SetValue("DisplayName", "ISM - Internet Speed Monitor");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "MeTariqul");
        key.SetValue("DisplayIcon", InstalledExe);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", $"\"{InstalledExe}\" /uninstall");
        key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" /uninstall /quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)Math.Min(sizeKb, int.MaxValue), RegistryValueKind.DWord);
        key.SetValue("URLInfoAbout", "https://github.com/MeTariqul/ISM-Internet-speed-Monitor");
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is unavailable.");

        object shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Cannot create WScript.Shell.");

        try
        {
            dynamic wsh = shell;
            dynamic link = wsh.CreateShortcut(shortcutPath);
            try
            {
                link.TargetPath = targetPath;
                link.WorkingDirectory = InstallDir;
                link.Description = "ISM - Internet Speed Monitor (taskbar speed meter)";
                link.IconLocation = targetPath + ",0";
                link.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void ScheduleSelfDelete()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;

        // cmd waits for this process to die, then removes the installed exe and (now empty) folder.
        string args = $"/c ping -n 3 127.0.0.1 >nul & del /f /q \"{exe}\" & rmdir /q \"{InstallDir}\"";
        var psi = new ProcessStartInfo("cmd.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try { Process.Start(psi); } catch { /* best effort */ }
    }

    private static void StopInstancesRunning(string exePath)
    {
        string processName = Path.GetFileNameWithoutExtension(AppExeName);
        foreach (Process p in Process.GetProcessesByName(processName))
        {
            try
            {
                if (p.MainModule?.FileName is { } file && PathsEqual(file, exePath)) p.Kill();
            }
            catch { /* access denied or already exiting */ }
            finally { p.Dispose(); }
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
