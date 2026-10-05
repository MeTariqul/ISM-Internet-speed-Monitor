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
    private readonly Action<string?> _setAdapter;
    private readonly Action<bool> _applyTextVisibility;
    private readonly Action _exit;

    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _showText;
    private readonly WinForms.ToolStripMenuItem _autostart;
    private readonly WinForms.ToolStripMenuItem _showSystem;
    private readonly WinForms.ToolStripMenuItem _formatNetwork;
    private readonly WinForms.ToolStripMenuItem _formatDisk;
    private readonly WinForms.ToolStripMenuItem _formatBoth;
    private readonly WinForms.ToolStripMenuItem[] _unitItems;
    private readonly WinForms.ToolStripMenuItem _adapters;

    public TaskbarMenu(
        AppSettings settings,
        MonitorViewModel viewModel,
        Action<string?> setAdapter,
        Action<bool> applyTextVisibility,
        Action exit)
    {
        _settings = settings;
        _viewModel = viewModel;
        _setAdapter = setAdapter;
        _applyTextVisibility = applyTextVisibility;
        _exit = exit;

        _menu = new WinForms.ContextMenuStrip { ShowImageMargin = true };
        _menu.Opening += (_, _) => Sync();

        _showText = MakeItem("Show text in taskbar", ShowText_Click, checkable: true);
        _autostart = MakeItem("Start with Windows", Autostart_Click, checkable: true);

        var format = new WinForms.ToolStripMenuItem("Taskbar text");
        _formatNetwork = MakeItem("Network (up / down)", FormatNetwork_Click, checkable: true);
        _formatDisk = MakeItem("Disk (read / write)", FormatDisk_Click, checkable: true);
        _formatBoth = MakeItem("Network + disk", FormatBoth_Click, checkable: true);
        format.DropDownItems.AddRange(
            new WinForms.ToolStripItem[] { _formatNetwork, _formatDisk, _formatBoth });

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

        _adapters = new WinForms.ToolStripMenuItem("Network adapter");

        _menu.Items.Add(_showText);
        _menu.Items.Add(_autostart);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(format);
        _menu.Items.Add(_showSystem);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(units);
        _menu.Items.Add(_adapters);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(MakeItem("About / Credits", About_Click, checkable: false));
        _menu.Items.Add(MakeItem("Exit", (_, _) => _exit(), checkable: false));

        Sync();
    }

    public WinForms.ContextMenuStrip Menu => _menu;

    public void ShowAt(System.Drawing.Point screenPoint)
        // Opens up and to the left: the overlay always sits on the bottom edge of the screen.
        => _menu.Show(screenPoint, WinForms.ToolStripDropDownDirection.AboveLeft);

    private static WinForms.ToolStripMenuItem MakeItem(string text, EventHandler? onClick, bool checkable)
    {
        var item = new WinForms.ToolStripMenuItem(text) { CheckOnClick = checkable };
        if (onClick is not null) item.Click += onClick;
        return item;
    }

    private void Sync()
    {
        _showText.Checked = _settings.ShowTaskbarText;
        _autostart.Checked = AppSettings.IsAutostartEnabled();
        _showSystem.Checked = _settings.ShowSystem;
        _formatNetwork.Checked = string.Equals(_settings.TaskbarFormat, "Network", StringComparison.Ordinal);
        _formatDisk.Checked = string.Equals(_settings.TaskbarFormat, "Disk", StringComparison.Ordinal);
        _formatBoth.Checked = string.Equals(_settings.TaskbarFormat, "Both", StringComparison.Ordinal);

        foreach (WinForms.ToolStripMenuItem item in _unitItems)
        {
            item.Checked = string.Equals(item.Tag as string, _settings.Units, StringComparison.OrdinalIgnoreCase);
        }

        RebuildAdapters();
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

    private void FormatNetwork_Click(object? sender, EventArgs e) => SetFormat("Network");

    private void FormatDisk_Click(object? sender, EventArgs e) => SetFormat("Disk");

    private void FormatBoth_Click(object? sender, EventArgs e) => SetFormat("Both");

    private void SetFormat(string format)
    {
        _settings.TaskbarFormat = format;
        Sync();
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
        Sync();
        SaveNow();
    }

    private void Adapter_Click(object? sender, EventArgs e)
    {
        if (sender is not WinForms.ToolStripMenuItem { Tag: string key }) return;

        string? adapter = key.Length == 0 ? null : key;
        _settings.Adapter = adapter;
        _setAdapter(adapter);
        Sync();
        SaveNow();
    }

    /// <summary>Write settings to disk right away so a force-kill can never lose a change.</summary>
    private void SaveNow() => _settings.Save();

    private void About_Click(object? sender, EventArgs e)
    {
        System.Windows.MessageBox.Show(
            "ISM - Internet Speed Monitor (Speed Monitor) 1.0.0\r\n\r\n"
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
