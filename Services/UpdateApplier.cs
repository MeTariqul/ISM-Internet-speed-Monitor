using System.Diagnostics;
using System.IO;

namespace SpeedMonitor.Services;

/// <summary>
/// Replaces the running exe with the staged update and starts it again.
///
/// Windows locks a running executable against deletion but allows renaming it. So the old
/// file is renamed to "*.old", the new one moved into its place, and a hidden cmd helper
/// waits for this process to exit before launching the new build and deleting the old file.
/// The same helper approach is used by <see cref="SelfInstall"/> for uninstall.
/// </summary>
public static class UpdateApplier
{
    public const string StagedSuffix = ".update.exe";
    public const string OldSuffix = ".old";

    public static string StagedPathFor(string exePath) => exePath + StagedSuffix;

    /// <summary>
    /// True when this process is the shipped exe. Debug builds and "dotnet run" hosts are
    /// excluded so development machines never replace their own build output.
    /// </summary>
    public static bool CanSelfUpdate()
    {
#if DEBUG
        return false;
#else
        string? exe = Environment.ProcessPath;
        return exe is not null
            && Path.GetExtension(exe).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
#endif
    }

    /// <summary>Removes a stale "*.old" / "*.update.exe" left behind by an earlier update.</summary>
    public static void CleanupLeftovers()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null) return;

        TryDelete(exe + OldSuffix);
        TryDelete(exe + StagedSuffix);
    }

    /// <summary>
    /// Swaps <paramref name="stagedExe"/> into the running exe's place and schedules the
    /// restart. The caller must shut down right after a successful call.
    /// </summary>
    public static bool Apply(string stagedExe, out string error)
    {
        error = string.Empty;

        string? current = Environment.ProcessPath;
        if (current is null)
        {
            error = "Cannot determine the running exe.";
            return false;
        }

        if (!File.Exists(stagedExe))
        {
            error = "The downloaded update is missing.";
            return false;
        }

        string old = current + OldSuffix;

        try
        {
            TryDelete(old);
            File.Move(current, old);

            try
            {
                File.Move(stagedExe, current);
            }
            catch
            {
                // Put the old exe back so the app still starts next time.
                try { File.Move(old, current); } catch { /* the helper below cannot help here */ }
                throw;
            }

            StartRestartHelper(current, old);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void StartRestartHelper(string exePath, string oldPath)
    {
        // ~3 s for this process to exit, then start the new build, then delete the old one.
        string args =
            $"/c ping -n 4 127.0.0.1 >nul & start \"\" \"{exePath}\" & ping -n 3 127.0.0.1 >nul & del /f /q \"{oldPath}\"";

        Process.Start(new ProcessStartInfo("cmd.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* locked by a still-exiting process; the next start cleans it up */ }
    }
}
