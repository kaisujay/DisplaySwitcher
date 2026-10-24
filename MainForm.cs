using System.Drawing;
using System.Text.Json;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DisplaySwitcher;

public sealed class MainForm : Form
{
    private readonly ComboBox _laptop = NewCombo();
    private readonly ComboBox _monitor = NewCombo();
    private readonly ComboBox _tv = NewCombo();
    private readonly Label _status = new();
    private readonly Label _detected = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DisplaySwitcher", "roles.json");
    private List<DisplayConfigNative.DisplayEndpoint> _displays = new();

    private sealed class RoleSettings
    {
        public string? Laptop { get; set; }
        public string? Monitor { get; set; }
        public string? Tv { get; set; }
    }

    public MainForm()
    {
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "DisplaySwitcher.ico"));

        Text = "Display Switcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 650);
        Size = new Size(700, 740);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        var logo = new PictureBox
        {
            Image = Image.FromFile(
        Path.Combine(AppContext.BaseDirectory, "DisplaySwitcher.png")),
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 10, 0)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1,
            RowCount = 7, AutoSize = false
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Display Switcher", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 20), ForeColor = Color.FromArgb(25, 38, 56),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        _detected.Text = "Detecting connected displays…";
        _detected.Dock = DockStyle.Fill;
        _detected.TextAlign = ContentAlignment.MiddleLeft;
        _detected.ForeColor = Color.DimGray;
        root.Controls.Add(_detected, 0, 1);

        var roles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 5, 0, 5) };
        roles.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        roles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) roles.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        AddRole(roles, "Laptop screen", _laptop, 0);
        AddRole(roles, "External monitor", _monitor, 1);
        AddRole(roles, "TV (HDMI)", _tv, 2);
        root.Controls.Add(roles, 0, 2);

        var refresh = MakeButton("Refresh connected displays", Color.FromArgb(225, 231, 239), Color.FromArgb(34, 49, 66));
        refresh.Height = 46;
        refresh.Margin = new Padding(0, 2, 0, 2);
        refresh.Click += (_, _) => RefreshDisplays();
        root.Controls.Add(refresh, 0, 3);

        // Role-based modes only; roles are assigned automatically when Windows exposes the displays.
        // Keep the four role-based modes below the role assignments.
        root.RowCount = 9;
        root.RowStyles.Clear();
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddModeButton(root, "1   Monitor Only", "Monitor only", () => ApplyMode(Mode.MonitorOnly), 4);
        AddModeButton(root, "2   Monitor + Laptop", "Monitor + laptop", () => ApplyMode(Mode.MonitorLaptop), 5);
        AddModeButton(root, "3   All Three Displays", "All three displays", () => ApplyMode(Mode.AllThree), 6);
        root.Controls.Add(MakeModeControl("4   TV Only", () => ApplyMode(Mode.TvOnly)), 0, 7);

        // Status message area.
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.TopLeft;
        _status.Padding = new Padding(0, 8, 0, 0);
        _status.ForeColor = Color.FromArgb(60, 75, 92);
        root.Controls.Add(_status, 0, 8);
        Load += (_, _) => RefreshDisplays();
    }

    private enum Mode { MonitorOnly, MonitorLaptop, AllThree, TvOnly }

    private static ComboBox NewCombo() => new()
    {
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
        IntegralHeight = false, Font = new Font("Segoe UI", 10), Margin = new Padding(4, 6, 4, 6)
    };

    private static void AddRole(TableLayoutPanel panel, string label, ComboBox combo, int row)
    {
        var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        panel.Controls.Add(l, 0, row);
        panel.Controls.Add(combo, 1, row);
    }

    private static Button MakeButton(string text, Color back, Color fore) => new()
    {
        Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
        BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 10),
        Margin = new Padding(0, 4, 0, 4), Cursor = Cursors.Hand, Height = 42
    };

    private static void AddModeButton(TableLayoutPanel panel, string text, string accessibleName, Action action, int row)
    {
        var button = MakeButton(text, Color.FromArgb(21, 105, 170), Color.White);
        button.AccessibleName = accessibleName;
        button.Click += (_, _) => action();
        panel.Controls.Add(button, 0, row);
    }

    private Control MakeModeControl(string text, Action action)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var button = MakeButton(text, Color.FromArgb(21, 105, 170), Color.White);
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
        return panel;
    }

    private void RefreshDisplays()
    {
        try
        {
            var saved = LoadSettings();
            _displays = DisplayConfigNative.EnumerateDisplays();
            var laptopDefault = FindDefault(DisplayConfigNative.OUTPUT_TECHNOLOGY_INTERNAL,
                DisplayConfigNative.OUTPUT_TECHNOLOGY_LVDS,
                DisplayConfigNative.OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED,
                DisplayConfigNative.OUTPUT_TECHNOLOGY_UDI_EMBEDDED);
            var monitorDefault = FindDefault(DisplayConfigNative.OUTPUT_TECHNOLOGY_DISPLAYPORT,
                DisplayConfigNative.OUTPUT_TECHNOLOGY_HDMI);
            var tvDefault = FindDefault(DisplayConfigNative.OUTPUT_TECHNOLOGY_HDMI);

            // Prefer previously saved roles; otherwise infer laptop/internal, DP monitor, and HDMI TV.
            // Keep defaults distinct so one target is never assigned to two roles.
            var laptop = ResolveSavedOrDefault(saved.Laptop, laptopDefault, null);
            var monitor = ResolveSavedOrDefault(saved.Monitor, monitorDefault, laptop?.Key);
            var tv = ResolveSavedOrDefault(saved.Tv, tvDefault, laptop?.Key, monitor?.Key);
            FillCombo(_laptop, laptop?.Key);
            FillCombo(_monitor, monitor?.Key);
            FillCombo(_tv, tv?.Key);

            _detected.Text = $"{_displays.Count} display target(s) detected. Roles are auto-assigned where possible.";
            _status.Text = tv is null
                ? "Laptop/monitor roles were detected where possible. Turn on the TV and click Refresh if HDMI is not listed."
                : "Roles auto-assigned. If the mapping is incorrect, choose the right display and click Save display roles.";
            SaveRolesButtonIfNeeded();
        }
        catch (Exception ex)
        {
            _detected.Text = "Could not enumerate displays.";
            ShowError("Display detection failed", ex);
        }
    }

    private DisplayConfigNative.DisplayEndpoint? FindDefault(params uint[] technologies) =>
        _displays.Where(d => technologies.Contains(d.OutputTechnology))
            .OrderByDescending(d => d.IsActive)
            .FirstOrDefault();

    private DisplayConfigNative.DisplayEndpoint? ResolveSavedOrDefault(
        string? savedKey, DisplayConfigNative.DisplayEndpoint? preferred, params string?[] excludedKeys)
    {
        // Passing a bare null to a params parameter can make the entire array null.
        // Normalize it before calling LINQ Contains.
        excludedKeys ??= Array.Empty<string?>();

        if (!string.IsNullOrWhiteSpace(savedKey))
        {
            var saved = _displays.FirstOrDefault(d => string.Equals(d.Key, savedKey, StringComparison.OrdinalIgnoreCase));
            if (saved is not null && !excludedKeys.Contains(saved.Key, StringComparer.OrdinalIgnoreCase)) return saved;
        }
        if (preferred is not null && !excludedKeys.Contains(preferred.Key, StringComparer.OrdinalIgnoreCase)) return preferred;
        return null;
    }

    private void FillCombo(ComboBox combo, string? savedKey)
    {
        combo.Items.Clear();
        combo.Items.Add(new DisplayChoice(null, "— No matching display detected —"));
        foreach (var d in _displays)
        {
            string state = d.IsActive ? " [active]" : d.IsAvailable ? " [available]" : " [not currently available]";
            combo.Items.Add(new DisplayChoice(d, d.Name + state));
        }
        int selected = 0;
        if (!string.IsNullOrWhiteSpace(savedKey))
        {
            for (int i = 1; i < combo.Items.Count; i++)
                if (combo.Items[i] is DisplayChoice c && c.Display?.Key == savedKey) { selected = i; break; }
        }
        combo.SelectedIndex = selected;
    }

    private sealed record DisplayChoice(DisplayConfigNative.DisplayEndpoint? Display, string Label)
    {
        public override string ToString() => Label;
    }

    private DisplayConfigNative.DisplayEndpoint? Selected(ComboBox combo) =>
        (combo.SelectedItem as DisplayChoice)?.Display;

    private void ApplyMode(Mode mode)
    {
        try
        {
            var laptop = Selected(_laptop);
            var monitor = Selected(_monitor);
            var tv = Selected(_tv);

            // Only require the roles used by this mode. For example, Monitor Only must
            // still work when the TV is powered off or not currently reported by Windows.
            if (mode is Mode.MonitorOnly or Mode.MonitorLaptop or Mode.AllThree && monitor is null)
                throw new InvalidOperationException("The external monitor was not detected. Turn it on and click Refresh connected displays.");
            if (mode is Mode.MonitorLaptop or Mode.AllThree && laptop is null)
                throw new InvalidOperationException("The laptop screen was not detected. Click Refresh connected displays.");
            if (mode is Mode.AllThree or Mode.TvOnly && tv is null)
                throw new InvalidOperationException("The TV was not detected. Turn it on, select its HDMI input, then click Refresh connected displays.");

            var selectedRoles = new[] { laptop, monitor, tv }.Where(d => d is not null).Cast<DisplayConfigNative.DisplayEndpoint>().ToArray();
            if (selectedRoles.Select(d => d.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedRoles.Length)
                throw new InvalidOperationException("Laptop, external monitor, and TV must be assigned to different display targets.");

            var chosen = mode switch
            {
                Mode.MonitorOnly => new[] { monitor! },
                Mode.MonitorLaptop => new[] { monitor!, laptop! },
                Mode.AllThree => new[] { monitor!, laptop!, tv! },
                Mode.TvOnly => new[] { tv! },
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };

            if (chosen.Any(d => !d.IsAvailable))
                throw new InvalidOperationException("At least one selected display is reported as unavailable. Turn on the TV, choose its HDMI input, then click Refresh connected displays.");

            // Re-enumerate to avoid using stale path data after Windows changes topology.
            var fresh = DisplayConfigNative.EnumerateDisplays();
            var freshByKey = fresh.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
            var freshChosen = chosen.Select(d => freshByKey.TryGetValue(d.Key, out var current) ? current : d).ToArray();
            DisplayConfigNative.ApplyDisplays(freshChosen);
            _status.Text = $"Applied: {mode switch { Mode.MonitorOnly => "Monitor Only", Mode.MonitorLaptop => "Monitor + Laptop", Mode.AllThree => "All Three Displays", _ => "TV Only" }}.";
            _status.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            ShowError("Could not apply display mode", ex);
        }
    }

    private RoleSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new RoleSettings();
            return JsonSerializer.Deserialize<RoleSettings>(File.ReadAllText(_settingsPath)) ?? new RoleSettings();
        }
        catch { return new RoleSettings(); }
    }

    private void SaveRolesButtonIfNeeded()
    {
        // Add the explicit Save button once; role mappings are stable device paths, not display numbers.
        if (Controls.Find("saveRolesButton", true).Length != 0) return;
        var save = MakeButton("Save display roles", Color.FromArgb(225, 231, 239), Color.FromArgb(34, 49, 66));
        save.Name = "saveRolesButton";
        save.Height = 42;
        save.Dock = DockStyle.Bottom;
        save.Margin = new Padding(0, 8, 0, 0);
        save.Click += (_, _) =>
        {
            try
            {
                var l = Selected(_laptop); var m = Selected(_monitor); var t = Selected(_tv);
                if (l is null || m is null || t is null)
                    throw new InvalidOperationException("Select all three display roles before saving.");
                if (new[] { l.Key, m.Key, t.Key }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
                    throw new InvalidOperationException("Each role must refer to a different display.");
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new RoleSettings
                {
                    Laptop = l.Key, Monitor = m.Key, Tv = t.Key
                }, new JsonSerializerOptions { WriteIndented = true }));
                _status.Text = "Display roles saved.";
                _status.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex) { ShowError("Could not save display roles", ex); }
        };

        // Place it in the form's bottom edge, outside the main mode table.
        Controls.Add(save);
        save.BringToFront();
    }

    private void ShowError(string title, Exception ex)
    {
        _status.Text = ex.Message;
        _status.ForeColor = Color.DarkRed;
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
