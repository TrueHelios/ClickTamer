using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClickTamer;

internal sealed class AppConfig
{
    public string DeviceId { get; set; } = "";
    public bool Autostart { get; set; }
    public bool Minimized { get; set; }

    public bool SuppressionEnabled { get; set; } = false;
    public bool DarkTheme { get; set; } = false;
    public bool ThemeSelectedManually { get; set; }

    public string Language { get; set; } = "RU";
}

internal sealed class DeviceChoice
{
    public string Id { get; }
    public string Name { get; }
    public DeviceChoice(string id, string name) { Id = id; Name = name; }
    public override string ToString() => Name;
}

internal sealed class ThemeAwareComboBox : ComboBox
{
    public void RecreateForColorMode()
    {
        if (IsHandleCreated && !IsDisposed)
            RecreateHandle();
    }
}

// Use custom painting for consistent button colors on Windows 10 and 11.
internal sealed class CenteredContextMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is ToolStripMenuItem)
        {
            var textBounds = e.TextRectangle;
            e.TextRectangle = new Rectangle(0, textBounds.Y, e.Item.Width, textBounds.Height);
            e.TextFormat |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        }

        base.OnRenderItemText(e);
    }
}

internal sealed class FlatUiButton : Button
{
    internal Color BorderColor = Color.FromArgb(135, 135, 135);
    internal Color HoverColor = Color.FromArgb(225, 225, 225);
    internal Color PressedColor = Color.FromArgb(210, 210, 210);
    private bool _hovered;
    private bool _pressed;

    public FlatUiButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UseVisualStyleBackColor = false;
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override bool ShowFocusCues => false;

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? SystemColors.Control);
        var rect = RectangleF.Inflate(new RectangleF(0.75f, 0.75f, Math.Max(0, Width - 1.5f), Math.Max(0, Height - 1.5f)), -0.1f, -0.1f);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        Color fill = !Enabled ? SystemColors.Control : _pressed ? PressedColor : _hovered ? HoverColor : BackColor;
        using (var path = RoundedRect(rect, 3.5f))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(Enabled ? BorderColor : SystemColors.ControlDark, 1.15f))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (Image != null)
        {
            int x = (Width - Image.Width) / 2;
            int y = (Height - Image.Height) / 2;
            g.DrawImage(Image, x, y, Image.Width, Image.Height);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MainForm : Form
{
    private readonly bool _startedByAutostart;
    private bool _hideAtStartup;
    public bool StartHiddenAtLaunch => _hideAtStartup;
    private readonly string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickTamer", "config.json");
    private AppConfig _config = new();
    private readonly ThemeAwareComboBox _devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
    private readonly FlatUiButton _toggle = new() { Width = 200, Height = 34 };
    private readonly Label _deviceLabel = new();
    private readonly FlatUiButton _refresh = new() { Width = 34, Height = 30, AccessibleName = "Обновить список устройств", Text = "\uE72C", TextAlign = ContentAlignment.MiddleCenter, UseVisualStyleBackColor = false, FlatStyle = FlatStyle.Standard };
    private readonly ThemeCheckBox _autostart = new() { Text = "Запускать вместе с Windows", AutoSize = false };
    private readonly ThemeCheckBox _minimized = new() { Text = "Запускать свёрнутым", AutoSize = false };
    private readonly Panel _optionsPanel = new() { Dock = DockStyle.Fill, Margin = new Padding(0) };
    private readonly FlatUiButton _language = new() { Width = 52, Height = 30, Text = "EN", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly FlatUiButton _theme = new() { Width = 36, Height = 30, Text = "", AutoSize = false, ImageAlign = ContentAlignment.MiddleCenter, AccessibleName = "Переключить цветовую тему", UseVisualStyleBackColor = false };
    private readonly ToolTip _themeTip = new();
    private readonly FlatUiButton _exit = new() { Width = 76, Height = 30, Text = "Выход", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _status = new() { AutoSize = true, Text = "Остановлено" };
    private readonly NotifyIcon _tray = new();
    private Icon? _applicationIcon;
    private Icon? _trayIcon;
    private Image? _themeGlyph;
    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 500 };
    private readonly System.Windows.Forms.Timer _deviceRefreshTimer = new() { Interval = 350 };
    private MMDeviceEnumerator? _enumerator;
    private MMDeviceNotificationClient? _deviceNotificationClient;
    private bool _deviceRefreshPending;
    private bool _deviceMonitoringDisposed;
    private int _deviceRefreshDispatchPending;
    private WasapiPlayer? _audio;
    private MMDevice? _audioDevice;
    private bool _wanted;
    private volatile bool _closing;
    private bool _refreshing;
    private bool _recovering;
    private bool _restartRequested;
    private bool _suppressAutostartChange;
    private int _retryDelayMs = 1000;
    private DateTime _nextRetry = DateTime.MinValue;
    private string _languageCode = "RU";

    public MainForm(bool startedByAutostart)
    {
        _startedByAutostart = startedByAutostart;
        StartupLog.Write($"MainForm construction. AutoStart={_startedByAutostart}");
        Text = "ClickTamer";
        InitializeApplicationIcons();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 330);
        _tray.Text = "ClickTamer";
        _devices.DrawMode = DrawMode.OwnerDrawFixed;
        _devices.DrawItem += DrawDeviceItem;
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowWindow();
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new CenteredContextMenuRenderer()
        };
        var openItem = new ToolStripMenuItem("Открыть")
        {
            TextAlign = ContentAlignment.MiddleCenter
        };
        openItem.Click += (_, _) => ShowWindow();
        var exitItem = new ToolStripMenuItem("Выход")
        {
            TextAlign = ContentAlignment.MiddleCenter
        };
        exitItem.Click += (_, _) => CloseApplication();
        menu.Items.AddRange([openItem, exitItem]);
        _tray.ContextMenuStrip = menu;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // device label
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // device picker + refresh
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // toggle button
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // centered status, directly below toggle
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // options centered in remaining space
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // footer
        Controls.Add(root);

        _deviceLabel.Text = "Выбор устройства:";
        _deviceLabel.Dock = DockStyle.Fill;
        _deviceLabel.TextAlign = ContentAlignment.MiddleCenter;
        root.Controls.Add(_deviceLabel, 0, 0);

        var devicePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _refresh.Size = new Size(34, 30);
        _refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refresh.AccessibleName = "Обновить список устройств";
        _refresh.Font = new Font("Segoe MDL2 Assets", 10F, FontStyle.Regular, GraphicsUnit.Point);
        _refresh.Text = "\uE72C"; // Windows system Refresh glyph
        _refresh.TextAlign = ContentAlignment.MiddleCenter;
        _refresh.Margin = new Padding(0);
        _devices.DropDownStyle = ComboBoxStyle.DropDownList;
        _devices.Anchor = AnchorStyles.Top;
        _devices.Margin = new Padding(0);
        _devices.Width = 350;
        var refreshTip = new ToolTip();
        refreshTip.SetToolTip(_refresh, "Обновить список аудиоустройств");
        void PositionDeviceControls()
        {
            const int gap = 6;
            const int pickerWidth = 350;
            int totalWidth = pickerWidth + gap + _refresh.Width;
            int left = Math.Max(0, (devicePanel.ClientSize.Width - totalWidth) / 2);
            _devices.Location = new Point(left, 3);
            _devices.Width = pickerWidth;
            _refresh.Location = new Point(left + pickerWidth + gap, 2);
        }
        devicePanel.Resize += (_, _) => PositionDeviceControls();
        devicePanel.Controls.Add(_devices);
        devicePanel.Controls.Add(_refresh);
        PositionDeviceControls();
        _refresh.BringToFront();
        root.Controls.Add(devicePanel, 0, 1);

        var toggleRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Margin = new Padding(0) };
        toggleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toggleRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _toggle.Anchor = AnchorStyles.None;
        _toggle.Margin = new Padding(0, 7, 0, 0);
        toggleRow.Controls.Add(_toggle, 0, 0);
        root.Controls.Add(toggleRow, 0, 2);

        var statusRow = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _status.Dock = DockStyle.Fill;
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleCenter;
        statusRow.Controls.Add(_status);
        root.Controls.Add(statusRow, 0, 3);

        var optionsPanel = _optionsPanel;
        _autostart.AutoSize = false;
        _minimized.AutoSize = false;
        _autostart.Height = 26;
        _minimized.Height = 26;
        _autostart.TextAlign = ContentAlignment.MiddleLeft;
        _minimized.TextAlign = ContentAlignment.MiddleLeft;
        optionsPanel.Resize += (_, _) => CenterOptionRows();
        optionsPanel.Controls.Add(_autostart);
        optionsPanel.Controls.Add(_minimized);
        root.Controls.Add(optionsPanel, 0, 4);
        CenterOptionRows();

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0), Padding = new Padding(0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _language.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _language.Margin = new Padding(0, 0, 0, 2);
        _language.MinimumSize = new Size(52, 30);
        _theme.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _theme.Margin = new Padding(0, 0, 0, 2);
        _theme.MinimumSize = new Size(36, 30);
        _theme.Font = new Font("Segoe UI Symbol", 11F, FontStyle.Regular, GraphicsUnit.Point);
        _exit.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _exit.Margin = new Padding(0, 0, 0, 2);
        _exit.MinimumSize = new Size(76, 30);
        bottom.Controls.Add(_language, 0, 0);
        bottom.Controls.Add(_theme, 1, 0);
        bottom.Controls.Add(_exit, 2, 0);
        root.Controls.Add(bottom, 0, 5);

        LoadConfig();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _wanted = _config.SuppressionEnabled;

        // Use the saved value for both manual launches and Windows startup.
        _hideAtStartup = _config.Minimized;
        StartupLog.Write($"Configuration loaded. Autostart={_config.Autostart}, Minimized={_config.Minimized}, HideAtStartup={_hideAtStartup}, DeviceId='{_config.DeviceId}'");
        if (_hideAtStartup)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            _ = Handle;
        }
        _autostart.Checked = _config.Autostart;
        _minimized.Checked = _config.Minimized;
        _languageCode = _config.Language == "EN" ? "EN" : "RU";
        ApplyLanguage();
        ApplyTheme();
        _theme.Click += (_, _) =>
        {
            _config.DarkTheme = !_config.DarkTheme;
            _config.ThemeSelectedManually = true;
            SaveConfig();
            ApplyNativeColorMode();
            ApplyTheme();
            ApplyLanguage();
        };
        _toggle.Click += (_, _) => SetSuppressionWanted(!_wanted, showError: true);
        _refresh.Click += (_, _) => RefreshDevices();
        _devices.SelectedIndexChanged += (_, _) => { if (!_refreshing && _devices.SelectedItem is DeviceChoice d) { _config.DeviceId = d.Id; SaveConfig(); if (_wanted) { StopAudio(false); _wanted = true; TryStartAudio(false); } } };
        _autostart.CheckedChanged += (_, _) =>
        {
            if (_suppressAutostartChange) return;
            bool requested = _autostart.Checked;
            try
            {
                StartupShortcutManager.SetEnabled(requested, Application.ExecutablePath);
                _config.Autostart = requested;
                SaveConfig();
            }
            catch (Exception ex)
            {
                StartupLog.Write("User could not change autostart option", ex);
                _suppressAutostartChange = true;
                _autostart.Checked = !requested;
                _suppressAutostartChange = false;
                SetStatus("Автозапуск не настроен — см. журнал", "Startup setup failed — see log");
            }
        };
        _minimized.CheckedChanged += (_, _) =>
        {
            bool requested = _minimized.Checked;
            _config.Minimized = requested;
            SaveConfig();

            // The saved setting remains in effect even if updating the shortcut fails.
            if (_config.Autostart)
            {
                try
                {
                    StartupShortcutManager.SetEnabled(true, Application.ExecutablePath);
                }
                catch (Exception ex)
                {
                    StartupLog.Write("Could not update minimized-start shortcut arguments; saved setting remains active.", ex);
                    SetStatus("Не удалось обновить ярлык автозапуска — см. журнал", "Could not update startup shortcut — see log");
                }
            }
        };
        _language.Click += (_, _) => { _languageCode = _languageCode == "RU" ? "EN" : "RU"; _config.Language = _languageCode; SaveConfig(); ApplyLanguage(); };
        _exit.Click += (_, _) => CloseApplication();
        FormClosing += (_, e) =>
        {
            // Hide on a normal close; allow Windows shutdown and logoff to finish.
            if (!_closing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                return;
            }

            if (!_closing)
            {
                _closing = true;
                _wanted = false;
                _watchdog.Stop();
                StopAudio();
                try { _tray.Visible = false; _tray.Dispose(); } catch { }
                DisposeDeviceMonitoring();
            }
        };

        if (!_startedByAutostart)
        {
            try
            {
                StartupShortcutManager.SetEnabled(_config.Autostart, Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                StartupLog.Write("Could not reconcile Windows startup registration", ex);
                SetStatus("Не удалось обновить автозапуск — см. журнал", "Could not update startup — see log");
            }
        }

        _watchdog.Tick += (_, _) => WatchdogTick();
        _deviceRefreshTimer.Tick += DeviceRefreshTimerTick;
        _devices.DropDownClosed += (_, _) =>
        {
            if (_deviceRefreshPending && !_closing && !_deviceRefreshTimer.Enabled)
                _deviceRefreshTimer.Start();
        };

        _watchdog.Start();
        _ = Handle;
        InitializeDeviceNotifications();
        RefreshDevices();
        if (_wanted)
        {
            StartupLog.Write($"Restoring saved suppression state after launch. StartedByAutostart={_startedByAutostart}, DeviceId='{_config.DeviceId}'.");
            EventHandler? startAudioWhenIdle = null;
            startAudioWhenIdle = (_, _) =>
            {
                Application.Idle -= startAudioWhenIdle;
                if (!_closing && _wanted) TryStartAudio(false);
            };
            Application.Idle += startAudioWhenIdle;
        }
    }

    private void SetSuppressionWanted(bool enabled, bool showError)
    {
        _wanted = enabled;
        _config.SuppressionEnabled = enabled;
        SaveConfig();

        _toggle.Text = enabled
            ? T("Выключить подавление", "Disable suppression")
            : T("Включить подавление", "Enable suppression");

        if (enabled)
        {
            _retryDelayMs = 1000;
            _nextRetry = DateTime.MinValue;
            TryStartAudio(showError);
        }
        else
        {
            // A stopped stream should only be restarted while suppression is enabled.
            StopAudio(turnOff: false);
        }
    }

    private void InitializeApplicationIcons()
    {
        try { _applicationIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch (Exception ex) { StartupLog.Write("Could not load the embedded ClickTamer icon; using a fallback.", ex); }

        Icon appIcon = _applicationIcon ?? (Icon)SystemIcons.Application.Clone();
        _applicationIcon = appIcon;
        Icon = appIcon; // Window title bar and taskbar button.
        Icon trayIcon = (Icon)appIcon.Clone();
        _trayIcon = trayIcon;
        _tray.Icon = trayIcon; // System tray.
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath)) ?? new AppConfig();
                if (!_config.ThemeSelectedManually)
                    _config.DarkTheme = SystemThemePreference.IsDark;
            }
            else
            {
                _config = new AppConfig
                {
                    DarkTheme = SystemThemePreference.IsDark,
                    ThemeSelectedManually = false
                };
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write("Could not load ClickTamer settings; defaults will be used.", ex);
            _config = new AppConfig
            {
                DarkTheme = SystemThemePreference.IsDark,
                ThemeSelectedManually = false
            };
        }
    }
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _config.ThemeSelectedManually)
            return;

        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (_closing || IsDisposed || _config.ThemeSelectedManually)
                    return;

                bool dark = SystemThemePreference.IsDark;
                if (_config.DarkTheme == dark)
                    return;

                _config.DarkTheme = dark;
                SaveConfig();
                ApplyNativeColorMode();
                ApplyTheme();
                ApplyLanguage();
            }));
        }
        catch (InvalidOperationException)
        {
            // The window may be closing while Windows broadcasts the change.
        }
    }

    private void ApplyNativeColorMode()
    {
        Application.SetColorMode(_config.DarkTheme && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? SystemColorMode.Dark
            : SystemColorMode.Classic);
        _devices.RecreateForColorMode();
    }

    private void SaveConfig()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!); File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true })); }
        catch { SetStatus("Не удалось сохранить настройки", "Could not save settings"); }
    }
    private void CenterOptionRows()
    {
        const int verticalGap = 16;
        const int boxAndSpacingWidth = 23; // 14px checkbox + 8px label gap + 1px inset
        const int trailingPadding = 4;

        int MeasureRowWidth(string text, Font font)
        {
            var measured = TextRenderer.MeasureText(
                text, font, new Size(1200, 26),
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return Math.Max(100, boxAndSpacingWidth + measured.Width + trailingPadding);
        }

        _autostart.Width = MeasureRowWidth(_autostart.Text, _autostart.Font);
        _minimized.Width = MeasureRowWidth(_minimized.Text, _minimized.Font);

        int groupHeight = _autostart.Height + verticalGap + _minimized.Height;
        int top = Math.Max(0, (_optionsPanel.ClientSize.Height - groupHeight) / 2);
        int autoLeft = Math.Max(0, (_optionsPanel.ClientSize.Width - _autostart.Width) / 2);
        int minLeft = Math.Max(0, (_optionsPanel.ClientSize.Width - _minimized.Width) / 2);
        _autostart.Location = new Point(autoLeft, top);
        _minimized.Location = new Point(minLeft, top + _autostart.Height + verticalGap);
    }

    private void ApplyLanguage()
    {
        _language.Text = _languageCode == "RU" ? "EN" : "RU";
        UpdateThemeGlyph();
        _theme.AccessibleName = _config.DarkTheme ? T("Переключить на светлую тему", "Switch to light theme") : T("Переключить на тёмную тему", "Switch to dark theme");
        _themeTip.SetToolTip(_theme, _config.DarkTheme ? T("Светлая тема", "Light theme") : T("Тёмная тема", "Dark theme"));
        _deviceLabel.Text = T("Выбор устройства:", "Select device:");
        _toggle.Text = _wanted ? T("Выключить подавление", "Disable suppression") : T("Включить подавление", "Enable suppression");
        _autostart.Text = T("Запускать вместе с Windows", "Run with Windows");
        _minimized.Text = T("Запускать свёрнутым", "Start minimized");
        CenterOptionRows();
        _exit.Text = T("Выход", "Exit");
        if (_tray.ContextMenuStrip?.Items.Count >= 2) { _tray.ContextMenuStrip.Items[0].Text = T("Открыть", "Open"); _tray.ContextMenuStrip.Items[1].Text = T("Выход", "Exit"); }
    }
    private bool IsDarkTheme => _config.DarkTheme;

    private void UpdateThemeGlyph()
    {
        Color glyphColor = IsDarkTheme
            ? Color.FromArgb(235, 235, 235)
            : Color.FromArgb(65, 65, 65);

        Image next = CreateThemeGlyph(showSun: IsDarkTheme, glyphColor);
        Image? previous = _themeGlyph;
        _themeGlyph = next;
        _theme.Image = next;
        _theme.ImageAlign = ContentAlignment.MiddleCenter;
        _theme.Text = "";
        previous?.Dispose();
    }

    private static Bitmap CreateThemeGlyph(bool showSun, Color color)
    {
        const int size = 20;
        var bitmap = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var pen = new Pen(color, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var brush = new SolidBrush(color);

        if (showSun)
        {
            float cx = size / 2f;
            float cy = size / 2f;
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                float x1 = cx + (float)Math.Cos(angle) * 6.0f;
                float y1 = cy + (float)Math.Sin(angle) * 6.0f;
                float x2 = cx + (float)Math.Cos(angle) * 8.2f;
                float y2 = cy + (float)Math.Sin(angle) * 8.2f;
                g.DrawLine(pen, x1, y1, x2, y2);
            }
            g.FillEllipse(brush, cx - 3.5f, cy - 3.5f, 7f, 7f);
        }
        else
        {
            using var crescent = new GraphicsPath();
            crescent.StartFigure();
            crescent.AddBezier(
                new PointF(15.1f, 3.0f),
                new PointF(8.9f, 3.0f),
                new PointF(5.9f, 7.2f),
                new PointF(6.1f, 10.4f));
            crescent.AddBezier(
                new PointF(6.1f, 10.4f),
                new PointF(6.4f, 15.8f),
                new PointF(11.1f, 18.4f),
                new PointF(15.2f, 16.0f));
            crescent.AddBezier(
                new PointF(15.2f, 16.0f),
                new PointF(10.5f, 14.7f),
                new PointF(10.1f, 6.0f),
                new PointF(15.1f, 3.0f));
            crescent.CloseFigure();
            g.FillPath(brush, crescent);
        }

        return bitmap;
    }

    private void ApplyTheme()
    {
        Color background = IsDarkTheme ? Color.FromArgb(32, 33, 36) : SystemColors.Control;
        Color foreground = IsDarkTheme ? Color.FromArgb(232, 234, 237) : SystemColors.ControlText;
        Color inputBackground = IsDarkTheme ? Color.FromArgb(43, 45, 49) : SystemColors.Window;
        Color buttonBackground = IsDarkTheme ? Color.FromArgb(55, 57, 61) : Color.FromArgb(248, 248, 248);
        Color border = IsDarkTheme ? Color.FromArgb(95, 99, 104) : SystemColors.ControlDark;

        BackColor = background;
        ForeColor = foreground;
        ApplyThemeToControls(Controls, background, foreground, inputBackground, buttonBackground, border);

        _theme.BackColor = IsDarkTheme ? Color.FromArgb(55, 57, 61) : Color.FromArgb(248, 248, 248);
        _theme.ForeColor = foreground;
        _theme.UseVisualStyleBackColor = false;
        _theme.FlatStyle = FlatStyle.Flat;
        _theme.FlatAppearance.BorderSize = 0;
        _theme.FlatAppearance.BorderColor = Color.FromArgb(0, border.R, border.G, border.B);
        _theme.FlatAppearance.MouseOverBackColor = IsDarkTheme ? Color.FromArgb(75, 79, 84) : SystemColors.ControlLight;
        _theme.FlatAppearance.MouseDownBackColor = IsDarkTheme ? Color.FromArgb(90, 94, 100) : SystemColors.ControlDark;

        UpdateStatusColor();

        if (IsHandleCreated) ApplyTitleBarTheme();
        Invalidate(true);
    }

    private void ApplyThemeToControls(Control.ControlCollection controls, Color background, Color foreground, Color inputBackground, Color buttonBackground, Color border)
    {
        foreach (Control control in controls)
        {
            control.ForeColor = foreground;

            if (control is ComboBox combo)
            {
                combo.BackColor = inputBackground;
                combo.ForeColor = foreground;
                combo.FlatStyle = FlatStyle.Standard;
            }
            else if (control is Button button)
            {
                button.BackColor = IsDarkTheme ? Color.FromArgb(55, 57, 61) : Color.FromArgb(246, 246, 246);
                button.ForeColor = foreground;
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.BorderColor = Color.FromArgb(0, border.R, border.G, border.B);
                button.FlatAppearance.MouseOverBackColor = IsDarkTheme ? Color.FromArgb(75, 79, 84) : Color.FromArgb(232, 232, 232);
                button.FlatAppearance.MouseDownBackColor = IsDarkTheme ? Color.FromArgb(90, 94, 100) : Color.FromArgb(218, 218, 218);
                if (button is FlatUiButton customButton)
                {
                    customButton.BorderColor = IsDarkTheme ? Color.FromArgb(105, 108, 113) : Color.FromArgb(135, 135, 135);
                    customButton.HoverColor = IsDarkTheme ? Color.FromArgb(75, 79, 84) : Color.FromArgb(232, 232, 232);
                    customButton.PressedColor = IsDarkTheme ? Color.FromArgb(90, 94, 100) : Color.FromArgb(218, 218, 218);
                    customButton.Invalidate();
                }
            }
            else
            {
                control.BackColor = background;
                if (control is ThemeCheckBox themedCheckBox)
                {
                    themedCheckBox.CheckFillColor = IsDarkTheme ? Color.FromArgb(128, 132, 137) : Color.FromArgb(112, 116, 121);
                    themedCheckBox.CheckMarkColor = Color.White;
                    themedCheckBox.BoxFillColor = inputBackground;
                    themedCheckBox.BoxBorderColor = border;
                    themedCheckBox.Invalidate();
                }
                else if (control is CheckBox checkBox)
                    checkBox.FlatStyle = IsDarkTheme ? FlatStyle.Flat : FlatStyle.Standard;
            }

            if (control.HasChildren)
                ApplyThemeToControls(control.Controls, background, foreground, inputBackground, buttonBackground, border);
        }
    }

    private void DrawDeviceItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;

        Color background = IsDarkTheme ? Color.FromArgb(43, 45, 49) : SystemColors.Window;
        Color foreground = IsDarkTheme ? Color.FromArgb(232, 234, 237) : SystemColors.WindowText;
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected && _devices.DroppedDown)
        {
            background = IsDarkTheme ? Color.FromArgb(65, 69, 74) : SystemColors.Highlight;
            foreground = IsDarkTheme ? Color.FromArgb(255, 255, 255) : SystemColors.HighlightText;
        }

        using (var brush = new SolidBrush(background))
            e.Graphics.FillRectangle(brush, e.Bounds);

        // Owner-draw also handles the selected item when the list is closed.
        string text = e.Index >= 0 && e.Index < _devices.Items.Count
            ? _devices.Items[e.Index]?.ToString() ?? string.Empty
            : _devices.SelectedItem?.ToString() ?? string.Empty;

        TextRenderer.DrawText(
            e.Graphics,
            text,
            e.Font ?? _devices.Font,
            Rectangle.Inflate(e.Bounds, -5, 0),
            foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            e.DrawFocusRectangle();
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            int enabled = IsDarkTheme ? 1 : 0;
            // Windows 10 and 11 use different attribute IDs on some builds.
            int result = DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            if (result != 0)
                _ = DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        }
        catch
        {
            // Keep the system default if DWM rejects the request.
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
    }

    private string T(string ru, string en) => _languageCode == "RU" ? ru : en;
    private void SetStatus(string ru, string en)
    {
        _status.Text = T(ru, en);
        UpdateStatusColor();
    }

    private void UpdateStatusColor()
    {
        bool active = _status.Text is "Подавление щелчков активно" or "Click suppression is active";
        bool disabled = _status.Text is "Подавление щелчков отключено" or "Click suppression is disabled";

        if (active)
            _status.ForeColor = IsDarkTheme ? Color.FromArgb(129, 199, 132) : Color.ForestGreen;
        else if (disabled)
            _status.ForeColor = IsDarkTheme ? Color.FromArgb(255, 123, 123) : Color.Firebrick;
        else
            _status.ForeColor = IsDarkTheme ? Color.FromArgb(232, 234, 237) : SystemColors.ControlText;
    }

    private void InitializeDeviceNotifications()
    {
        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            _deviceNotificationClient = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
            _deviceNotificationClient.DeviceAdded += (_, _) => QueueDeviceRefresh();
            _deviceNotificationClient.DeviceRemoved += (_, _) => QueueDeviceRefresh();
            _deviceNotificationClient.DeviceStateChanged += (_, _) => QueueDeviceRefresh();
            StartupLog.Write("Automatic audio-device notifications registered.");
        }
        catch (Exception ex)
        {
            // The manual refresh button remains available if notifications fail.
            StartupLog.Write("Automatic audio-device notifications could not be registered; manual refresh remains available.", ex);
            try { _deviceNotificationClient?.Dispose(); } catch { }
            _deviceNotificationClient = null;
        }
    }

    private void QueueDeviceRefresh()
    {
        if (_closing)
            return;

        if (System.Threading.Interlocked.Exchange(ref _deviceRefreshDispatchPending, 1) != 0)
            return;

        try
        {
            BeginInvoke((Action)(() =>
            {
                System.Threading.Interlocked.Exchange(ref _deviceRefreshDispatchPending, 0);
                if (_closing || IsDisposed || Disposing)
                    return;

                _deviceRefreshPending = true;
                _deviceRefreshTimer.Stop();
                _deviceRefreshTimer.Start();
            }));
        }
        catch (InvalidOperationException)
        {
            System.Threading.Interlocked.Exchange(ref _deviceRefreshDispatchPending, 0);
        }
    }

    private void DeviceRefreshTimerTick(object? sender, EventArgs e)
    {
        _deviceRefreshTimer.Stop();
        if (_closing || IsDisposed || Disposing)
        {
            _deviceRefreshPending = false;
            return;
        }

        if (!_deviceRefreshPending || _devices.DroppedDown)
            return;

        _deviceRefreshPending = false;
        RefreshDevices();
    }

    private void DisposeDeviceMonitoring()
    {
        if (_deviceMonitoringDisposed)
            return;

        _deviceMonitoringDisposed = true;
        try { _deviceRefreshTimer.Stop(); } catch { }
        _deviceRefreshPending = false;

        try { _deviceNotificationClient?.Dispose(); }
        catch (Exception ex) { StartupLog.Write("Could not unregister audio-device notifications cleanly.", ex); }
        finally { _deviceNotificationClient = null; }

        try { _enumerator?.Dispose(); }
        catch (Exception ex) { StartupLog.Write("Could not dispose the audio-device enumerator cleanly.", ex); }
        finally { _enumerator = null; }

        _deviceRefreshTimer.Dispose();
    }

    private void RefreshDevices()
    {
        if (_refreshing || _closing) return;
        _deviceRefreshTimer.Stop();
        _deviceRefreshPending = false;
        _refreshing = true;
        string wantedId = (_devices.SelectedItem as DeviceChoice)?.Id ?? _config.DeviceId;
        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            var found = new List<DeviceChoice>();
            foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                found.Add(new DeviceChoice(d.ID, d.FriendlyName));
                d.Dispose();
            }
            _devices.BeginUpdate(); _devices.Items.Clear();
            foreach (var d in found) _devices.Items.Add(d);
            var match = found.FindIndex(d => d.Id == wantedId);
            if (match >= 0) _devices.SelectedIndex = match;
            else if (found.Count > 0 && string.IsNullOrWhiteSpace(_config.DeviceId)) _devices.SelectedIndex = 0;
            _devices.EndUpdate();
            if (_devices.SelectedItem is DeviceChoice selected) { _config.DeviceId = selected.Id; SaveConfig(); }
            if (found.Count == 0)
                SetStatus("Аудиоустройства не найдены", "No audio devices found");
            else if (_wanted && _audio == null)
                SetStatus("Ожидание аудиопотока", "Waiting for audio stream");
            else if (!_wanted)
                SetStatus("Подавление щелчков отключено", "Click suppression is disabled");
        }
        catch { SetStatus("Ошибка списка устройств", "Device list error"); }
        finally { _refreshing = false; }
    }

    private void TryStartAudio(bool showError)
    {
        if (!_wanted || _closing || _recovering || DateTime.Now < _nextRetry) return;

        _recovering = true;
        WasapiPlayer? candidate = null;
        MMDevice? candidateDevice = null;
        try
        {
            DisposeAudioInstance();

            string id = (_devices.SelectedItem as DeviceChoice)?.Id ?? _config.DeviceId;
            if (string.IsNullOrWhiteSpace(id))
            {
                SetStatus("Выберите аудиоустройство", "Select an audio device");
                _nextRetry = DateTime.Now.AddSeconds(2);
                return;
            }

            _enumerator ??= new MMDeviceEnumerator();
            candidateDevice = _enumerator.GetDevice(id);

            // The player uses this endpoint until the stream is disposed.
            candidate = new WasapiPlayerBuilder()
                .WithDevice(candidateDevice)
                .WithSharedMode()
                .WithEventSync()
                .WithLatency(100)
                .Build();

            WaveFormat format = candidate.DeviceMixFormat;
            StartupLog.Write($"Creating silent WASAPI stream. DeviceId='{id}', format='{format}', shareMode=Shared.");
            candidate.Init(new ZeroWaveProvider(format));

            _audio = candidate;
            _audioDevice = candidateDevice;
            candidate.PlaybackStopped += AudioPlaybackStopped;
            candidate.Play();

            candidate = null;
            candidateDevice = null;

            _restartRequested = false;
            _retryDelayMs = 1000;
            _nextRetry = DateTime.MinValue;
            SetStatus("Подавление щелчков активно", "Click suppression is active");
            _toggle.Text = T("Выключить подавление", "Disable suppression");
            StartupLog.Write("Silent WASAPI stream started successfully.");
        }
        catch (Exception ex)
        {
            if (candidate != null)
            {
                candidate.PlaybackStopped -= AudioPlaybackStopped;
                try { candidate.Stop(); } catch { }
                try { candidate.Dispose(); } catch { }
                if (ReferenceEquals(_audio, candidate)) _audio = null;
            }
            else if (_audio != null)
            {
                var failedPlayer = _audio;
                _audio = null;
                failedPlayer.PlaybackStopped -= AudioPlaybackStopped;
                try { failedPlayer.Stop(); } catch { }
                try { failedPlayer.Dispose(); } catch { }
            }

            MMDevice? deviceToDispose = _audioDevice;
            _audioDevice = null;
            if (deviceToDispose != null)
            {
                if (ReferenceEquals(deviceToDispose, candidateDevice)) candidateDevice = null;
                try { deviceToDispose.Dispose(); } catch { }
            }
            try { candidateDevice?.Dispose(); } catch { }

            _restartRequested = false;
            SetStatus("Ошибка аудио — повторная попытка", "Audio error — retrying");
            _nextRetry = DateTime.Now.AddMilliseconds(_retryDelayMs);
            _retryDelayMs = Math.Min(_retryDelayMs * 2, 30000);
            StartupLog.Write("Could not start silent WASAPI stream; retry scheduled.", ex);

            if (showError)
            {
                MessageBox.Show(
                    T("Не удалось запустить аудиопоток. ClickTamer будет повторять попытки автоматически.",
                      "Could not start audio. ClickTamer will retry automatically.") + "\n\n" + ex.Message,
                    "ClickTamer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _recovering = false;
        }
    }

    private void AudioPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_closing || !_wanted) return;

        // Handle recovery on the UI thread, outside the audio callback.
        try
        {
            if (!IsHandleCreated || IsDisposed || Disposing)
            {
                StartupLog.Write("PlaybackStopped arrived after the window handle was unavailable.");
                return;
            }

            BeginInvoke(new Action(() =>
            {
                if (_closing || !_wanted || !ReferenceEquals(sender, _audio)) return;

                if (e.Exception != null)
                    StartupLog.Write("WASAPI PlaybackStopped reported an exception; a reconnect will be attempted.", e.Exception);
                else
                    StartupLog.Write("WASAPI PlaybackStopped fired; a reconnect will be attempted.");

                _restartRequested = true;
                _nextRetry = DateTime.Now.AddMilliseconds(250);
                SetStatus("Поток остановлен — переподключение", "Stream stopped — reconnecting");
            }));
        }
        catch (Exception ex)
        {
            StartupLog.Write("Could not schedule WASAPI recovery on the UI thread.", ex);
        }
    }

    private void DisposeAudioInstance()
    {
        var old = _audio;
        var oldDevice = _audioDevice;
        _audio = null;
        _audioDevice = null;

        if (old != null)
        {
            // Unsubscribe first so stopping the player cannot queue another retry.
            old.PlaybackStopped -= AudioPlaybackStopped;
            try { old.Stop(); } catch { }
            try { old.Dispose(); } catch { }
        }

        // Dispose the endpoint after the player, which still holds a reference to it.
        try { oldDevice?.Dispose(); } catch { }
    }

    private void StopAudio(bool turnOff = true)
    {
        if (turnOff) _wanted = false;
        _restartRequested = false;
        DisposeAudioInstance();

        if (!_closing && !_wanted)
        {
            _toggle.Text = T("Включить подавление", "Enable suppression");
            SetStatus("Подавление щелчков отключено", "Click suppression is disabled");
        }
    }

    private void WatchdogTick()
    {
        if (_closing || !_wanted || _recovering) return;
        if (DateTime.Now < _nextRetry) return;

        if (_restartRequested || _audio == null)
        {
            StartupLog.Write(_restartRequested
                ? "Watchdog is rebuilding the audio client after PlaybackStopped."
                : "Watchdog found no audio client; retrying connection.");

            _restartRequested = false;
            TryStartAudio(false);
            return;
        }

        try
        {
            if (_audio.PlaybackState != PlaybackState.Playing)
            {
                StartupLog.Write($"Watchdog detected non-playing audio client (state={_audio.PlaybackState}); scheduling rebuild.");
                _restartRequested = true;
                _nextRetry = DateTime.Now.AddMilliseconds(250);
                SetStatus("Поток остановлен — переподключение", "Stream stopped — reconnecting");
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write("Watchdog could not query playback state; scheduling a fresh WASAPI client.", ex);
            _restartRequested = true;
            _nextRetry = DateTime.Now.AddMilliseconds(250);
            SetStatus("Поток остановлен — переподключение", "Stream stopped — reconnecting");
        }
    }
    private void ShowWindow()
    {
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        if (!Visible) Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
    private void CloseApplication()
    {
        _closing = true; _wanted = false; _watchdog.Stop(); StopAudio();
        try { _tray.Visible = false; _tray.Dispose(); } catch { }
        DisposeDeviceMonitoring();
        Close(); Application.Exit();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            DisposeDeviceMonitoring();
            try { _trayIcon?.Dispose(); } catch { }
            try { _applicationIcon?.Dispose(); } catch { }
            try { _themeGlyph?.Dispose(); } catch { }
            _themeGlyph = null;
            _trayIcon = null;
            _applicationIcon = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        StartupLog.Write($"Main window shown. AutoStart={_startedByAutostart}, ConfigMinimized={_config.Minimized}, StartHiddenAtLaunch={_hideAtStartup}");
    }
}

internal sealed class ThemeCheckBox : CheckBox
{
    internal Color BoxFillColor = Color.White;
    internal Color BoxBorderColor = Color.Gray;
    internal Color CheckFillColor = Color.FromArgb(112, 116, 121);
    internal Color CheckMarkColor = Color.White;
    private bool _hovered;

    public ThemeCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        BackColor = SystemColors.Control;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        int boxSize = 14;
        int boxX = 1;
        int boxY = (Height - boxSize) / 2;
        var box = new Rectangle(boxX, boxY, boxSize, boxSize);
        Color boxFill = Checked ? CheckFillColor : BoxFillColor;
        if (_hovered && !Checked) boxFill = ControlPaint.Light(boxFill, 0.04f);
        using (var brush = new SolidBrush(boxFill))
            e.Graphics.FillRectangle(brush, box);
        using (var pen = new Pen(BoxBorderColor, 1f))
            e.Graphics.DrawRectangle(pen, box);

        if (Checked)
        {
            using var pen = new Pen(CheckMarkColor, 2f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            e.Graphics.DrawLines(pen, new[]
            {
                new Point(boxX + 3, boxY + 7),
                new Point(boxX + 6, boxY + 10),
                new Point(boxX + 11, boxY + 4)
            });
        }

        var textRect = new Rectangle(boxX + boxSize + 8, 0, Math.Max(0, Width - boxX - boxSize - 8), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), ForeColor, BackColor);
    }
}

internal sealed class ZeroWaveProvider : IWaveProvider
{
    public WaveFormat WaveFormat { get; }

    public ZeroWaveProvider(WaveFormat format) => WaveFormat = format;

    public int Read(Span<byte> buffer)
    {
        // 8-bit PCM is unsigned, so its silence value is 0x80 rather than zero.
        if (WaveFormat.Encoding == WaveFormatEncoding.Pcm && WaveFormat.BitsPerSample == 8)
            buffer.Fill(0x80);
        else
            buffer.Clear();

        return buffer.Length;
    }
}
