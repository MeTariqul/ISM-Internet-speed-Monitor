using SpeedMonitor.Metrics;
using SpeedMonitor.ViewModels;
using WinForms = System.Windows.Forms;

namespace SpeedMonitor.Services;

/// <summary>
/// The single settings menu, shared by the tray icon and the taskbar overlay.
/// Built with WinForms so the tray can host it directly.
/// </summary>
public sealed class TaskbarMenu : IDisposable
{
    private readonly AppSettings _settings;
    private readonly MonitorViewModel _viewModel;
    private readonly MetricsSampler _sampler;
    private readonly LicenseDialogService _licenseDialog;
    private readonly Action<bool> _applyTextVisibility;
    private readonly Action _exit;
    private readonly Action _checkForUpdates;

    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _showText;
    private readonly WinForms.ToolStripMenuItem _autostart;
    private readonly WinForms.ToolStripMenuItem _autoUpdate;
    private readonly WinForms.ToolStripMenuItem _showSystem;
    private readonly WinForms.ToolStripMenuItem _formatNetwork;
    private readonly WinForms.ToolStripMenuItem _formatDisk;
    private readonly WinForms.ToolStripMenuItem _formatBoth;
    private readonly WinForms.ToolStripMenuItem[] _unitItems;
    private readonly WinForms.ToolStripMenuItem _adapters;
    private readonly WinForms.ToolStripMenuItem _installItem;

    public TaskbarMenu(
        AppSettings settings,
        MonitorViewModel viewModel,
        MetricsSampler sampler,
        LicenseDialogService licenseDialog,
        Action<bool> applyTextVisibility,
        Action exit,
        Action checkForUpdates)
    {
        _settings = settings;
        _viewModel = viewModel;
        _sampler = sampler;
        _licenseDialog = licenseDialog;
        _applyTextVisibility = applyTextVisibility;
        _exit = exit;
        _checkForUpdates = checkForUpdates;

        _menu = new WinForms.ContextMenuStrip { ShowImageMargin = true };
        _menu.Opening += (_, _) => Sync();
        KeepOpenOnItemClick(_menu);

        _showText = MakeItem("Show text in taskbar", ShowText_Click, checkable: true);
        _autostart = MakeItem("Start with Windows", Autostart_Click, checkable: true);
        _autoUpdate = MakeItem("Update automatically", AutoUpdate_Click, checkable: true);

        var format = new WinForms.ToolStripMenuItem("Taskbar text");
        _formatNetwork = MakeItem("Network (up / down)", FormatNetwork_Click, checkable: true);
        _formatDisk = MakeItem("Disk (read / write)", FormatDisk_Click, checkable: true);
        _formatBoth = MakeItem("Network + disk", FormatBoth_Click, checkable: true);
        format.DropDownItems.AddRange(
            new WinForms.ToolStripItem[] { _formatNetwork, _formatDisk, _formatBoth });
        KeepOpenOnItemClick(format.DropDown);

        _showSystem = MakeItem("Also show CPU / RAM", ShowSystem_Click, checkable: true);

        var units = new WinForms.ToolStripMenuItem("Network units");
        _unitItems = new (string Label, string Key)[]
        {
            ("Auto (B / KB / MB)", "Auto"),
            ("Kilobytes per second", "KB"),
            ("Megabits per second", "Mbps"),
        }.Select(choice =>
        {
            WinForms.ToolStripMenuItem item = MakeItem(choice.Label, Unit_Click, checkable: true);
            item.Tag = choice.Key;
            return item;
        }).ToArray();
        units.DropDownItems.AddRange(_unitItems);
        KeepOpenOnItemClick(units.DropDown);

        _adapters = new WinForms.ToolStripMenuItem("Network adapter");
        KeepOpenOnItemClick(_adapters.DropDown);
        _installItem = MakeItem("Install ISM...", Install_Click, checkable: false);

        _menu.Items.Add(_showText);
        _menu.Items.Add(_autostart);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(format);
        _menu.Items.Add(_showSystem);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(units);
        _menu.Items.Add(_adapters);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(_autoUpdate);
        _menu.Items.Add(MakeItem("Check for updates...", (_, _) => _checkForUpdates(), checkable: false));
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(_installItem);
        _menu.Items.Add(MakeItem("License...", (_, _) => _licenseDialog.ShowLicense(), checkable: false));
        _menu.Items.Add(MakeItem("About / Credits", About_Click, checkable: false));
        _menu.Items.Add(MakeItem("Exit", (_, _) => _exit(), checkable: false));

        Sync();
    }

    public WinForms.ContextMenuStrip Menu => _menu;

    public void ShowAt(System.Drawing.Point screenPoint)
        // Opens up and to the left: the overlay always sits on the bottom edge of the screen.
        => _menu.Show(screenPoint, WinForms.ToolStripDropDownDirection.AboveLeft);

    private WinForms.ToolStripMenuItem MakeItem(string text, EventHandler? onClick, bool checkable)
    {
        var item = new WinForms.ToolStripMenuItem(text) { CheckOnClick = checkable };

        // WinForms closes the dropdown *before* raising Click, so a toggle would dismiss the
        // menu before the handler could react. ItemClicked closes are cancelled in
        // KeepOpenOnItemClick and ordinary commands close the menu explicitly here instead.
        if (!checkable) item.Click += (_, _) => _menu.Close();

        // A toggle inside a submenu (units, format, adapter) collapses the submenu but leaves
        // the root menu open, exactly like the top-level toggles.
        item.Click += (_, _) =>
        {
            if (item.GetCurrentParent() is WinForms.ToolStripDropDown parent
                && !ReferenceEquals(parent, _menu))
            {
                parent.Close();
            }
        };

        if (onClick is not null) item.Click += onClick;
        return item;
    }

    /// <summary>
    /// Keeps a dropdown open when an item is clicked. Toggles stay open so several settings can
    /// be changed in a row; normal commands close the root menu themselves in <see cref="MakeItem"/>.
    /// Clicking outside the menu, pressing Escape, or choosing Exit still closes it.
    /// </summary>
    private static void KeepOpenOnItemClick(WinForms.ToolStripDropDown dropDown)
    {
        dropDown.Closing += (_, e) =>
        {
            if (e.CloseReason == WinForms.ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        };
    }

    private void Sync()
    {
        SyncChecks();
        RebuildAdapters();
    }

    /// <summary>Updates check marks without touching the item tree, safe while the menu is open.</summary>
    private void SyncChecks()
    {
        _showText.Checked = _settings.ShowTaskbarText;
        _autostart.Checked = AppSettings.IsAutostartEnabled();
        _autoUpdate.Checked = _settings.AutoUpdate;
        _showSystem.Checked = _settings.ShowSystem;
        _formatNetwork.Checked = string.Equals(_settings.TaskbarFormat, "Network", StringComparison.Ordinal);
        _formatDisk.Checked = string.Equals(_settings.TaskbarFormat, "Disk", StringComparison.Ordinal);
        _formatBoth.Checked = string.Equals(_settings.TaskbarFormat, "Both", StringComparison.Ordinal);

        foreach (WinForms.ToolStripMenuItem item in _unitItems)
        {
            item.Checked = string.Equals(item.Tag as string, _settings.Units, StringComparison.OrdinalIgnoreCase);
        }

        _installItem.Text = SelfInstall.IsInstalled ? "Uninstall ISM..." : "Install ISM...";

        UpdateAdapterChecks();
    }

    private void RebuildAdapters()
    {
        _adapters.DropDownItems.Clear();
        string? selected = _settings.Adapter;

        WinForms.ToolStripMenuItem auto = MakeItem("Auto (all adapters)", Adapter_Click, checkable: true);
        auto.Tag = string.Empty;
        _adapters.DropDownItems.Add(auto);

        foreach (string instance in MetricsSampler.GetAdapterInstances())
        {
            WinForms.ToolStripMenuItem item = MakeItem(instance, Adapter_Click, checkable: true);
            item.Tag = instance;
            _adapters.DropDownItems.Add(item);
        }

        UpdateAdapterChecks();
    }

    private void UpdateAdapterChecks()
    {
        string? selected = _settings.Adapter;

        foreach (WinForms.ToolStripItem raw in _adapters.DropDownItems)
        {
            if (raw is not WinForms.ToolStripMenuItem item) continue;
            string? tag = item.Tag as string;
            item.Checked = selected is null
                ? string.IsNullOrEmpty(tag)
                : string.Equals(tag, selected, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- handlers

    private void ShowText_Click(object? sender, EventArgs e)
    {
        _settings.ShowTaskbarText = _showText.Checked;
        _applyTextVisibility(_settings.ShowTaskbarText);
        SaveNow();
    }

    private void Autostart_Click(object? sender, EventArgs e)
    {
        AppSettings.ApplyAutostart(_autostart.Checked);
        _settings.StartWithWindows = _autostart.Checked;
        SaveNow();
    }

    private void ShowSystem_Click(object? sender, EventArgs e)
    {
        _settings.ShowSystem = _showSystem.Checked;
        _viewModel.Refresh();
        SaveNow();
    }

    private void AutoUpdate_Click(object? sender, EventArgs e)
    {
        _settings.AutoUpdate = _autoUpdate.Checked;
        SaveNow();
    }

    private void FormatNetwork_Click(object? sender, EventArgs e) => SetFormat("Network");

    private void FormatDisk_Click(object? sender, EventArgs e) => SetFormat("Disk");

    private void FormatBoth_Click(object? sender, EventArgs e) => SetFormat("Both");

    private void SetFormat(string format)
    {
        _settings.TaskbarFormat = format;
        SyncChecks();
        _viewModel.Refresh();
        SaveNow();
    }

    private void Unit_Click(object? sender, EventArgs e)
    {
        if (sender is not WinForms.ToolStripMenuItem { Tag: string key }) return;

        _viewModel.Units = key switch
        {
            "KB" => UnitMode.KiloBytes,
            "Mbps" => UnitMode.Megabits,
            _ => UnitMode.Auto,
        };
        SyncChecks();
        SaveNow();
    }

    private void Adapter_Click(object? sender, EventArgs e)
    {
        if (sender is not WinForms.ToolStripMenuItem { Tag: string key }) return;

        string? adapter = key.Length == 0 ? null : key;
        _settings.Adapter = adapter;
        _sampler.SetAdapter(adapter);
        SyncChecks();
        SaveNow();
    }

    /// <summary>Write settings to disk right away so a force-kill can never lose a change.</summary>
    private void SaveNow() => _settings.Save();

    private void Install_Click(object? sender, EventArgs e)
    {
        if (SelfInstall.IsInstalled)
        {
            ConfirmUninstall();
            return;
        }

        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            "Install ISM for your Windows account?\n\n"
            + $"  - Copy to\n    {SelfInstall.InstallDir}\n"
            + "  - Start Menu shortcut\n"
            + "  - Uninstall entry in Settings > Apps\n\n"
            + "No admin rights needed. Your settings stay where they are and you can\n"
            + "uninstall at any time from this menu or from Settings > Apps.",
            "Install - ISM Internet Speed Monitor",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (answer != System.Windows.MessageBoxResult.Yes) return;

        var (ok, message) = SelfInstall.Install();
        System.Windows.MessageBox.Show(
            ok
                ? $"Installed.\n\n{message}\n\n"
                  + "From now on, start it from the Start Menu; uninstall from this menu\n"
                  + "or from Settings > Apps."
                : "Install failed:\n\n" + message,
            "Install - ISM Internet Speed Monitor",
            System.Windows.MessageBoxButton.OK,
            ok ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
    }

    private void ConfirmUninstall()
    {
        bool runningFromInstall = SelfInstall.IsRunningFromInstall;

        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            "Uninstall ISM?\n\n"
            + "  - Start Menu shortcut\n"
            + "  - Settings > Apps entry\n"
            + "  - installed copy\n"
            + "  - your settings file\n"
            + (runningFromInstall
                ? "\nThe copy you are running IS the installed one - ISM will close.\n"
                : "\nThe portable copy you are running is not touched.\n"),
            "Uninstall - ISM Internet Speed Monitor",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (answer != System.Windows.MessageBoxResult.Yes) return;

        var (ok, message) = SelfInstall.Uninstall(deleteSettings: true);
        if (!ok)
        {
            System.Windows.MessageBox.Show(
                "Uninstall failed:\n\n" + message,
                "Uninstall - ISM Internet Speed Monitor",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        if (runningFromInstall)
        {
            _exit(); // the scheduled cmd helper removes the exe once this process is gone
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Uninstalled - shortcut, Apps entry and installed copy removed.",
                "Uninstall - ISM Internet Speed Monitor",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void About_Click(object? sender, EventArgs e)
    {
        string version = (typeof(TaskbarMenu).Assembly.GetName().Version ?? new Version(1, 0, 0)).ToString(3);
        System.Windows.MessageBox.Show(
            "ISM - Internet Speed Monitor (Speed Monitor) " + version + "\r\n\r\n"
            + "A DU Meter style monitor: live upload/download, disk read/write,\r\n"
            + "CPU and RAM readout drawn inside the Windows taskbar.\r\n\r\n"
            + "Created by: MeTariqul\r\n"
            + "GitHub: https://github.com/MeTariqul\r\n"
            + "Project: https://github.com/MeTariqul/ISM-Internet-speed-Monitor\r\n\r\n"
            + "Free and open source - use it, modify it, share it.\r\n"
            + "Please keep this credit when you redistribute it.",
            "About - ISM Internet Speed Monitor",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    public void Dispose() => _menu.Dispose();
}
