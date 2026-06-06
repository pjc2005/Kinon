using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Kinon.Database;
using Kinon.Models;

namespace Kinon;

/// <summary>
/// 置顶无焦点弹窗 — 显示快捷键列表，支持搜索、分组、已记住标记。
/// </summary>
public sealed class OverlayForm : Form
{
    // --- P/Invoke for dragging borderless window ---
    private const int WM_NCHITTEST = 0x84;
    private const int HTCAPTION = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // --- WinForms controls ---
    private TextBox _searchBox;
    private ComboBox _appFilter;
    private CheckBox _groupByApp;
    private DataGridView _grid;
    private Label _statusLabel;
    private NotifyIcon _trayIcon;
    private ContextMenuStrip _trayMenu;
    private System.Windows.Forms.Timer _refreshTimer;
    private System.Windows.Forms.Timer _searchTimer;
    private bool _dragActive;
    private Point _dragStart;

    private string _searchKeyword = string.Empty;
    private bool _groupMode;

    public OverlayForm()
    {
        InitializeComponent();
        ApplyDarkTheme();
        SetupTrayIcon();
        SetupTimers();
    }

    // --- Win32 overrides ---

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (not in taskbar)
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (don't steal focus)
            return cp;
        }
    }

    // --- Component setup ---

    private void InitializeComponent()
    {
        // Form
        Text = "Kinon - 快捷键查看";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(
            Config.AppSettings.WindowWidth,
            Config.AppSettings.WindowHeight
        );

        // Position (bottom-right by default)
        var screen = Screen.PrimaryScreen?.WorkingArea;
        if (screen.HasValue)
        {
            var wx = Config.AppSettings.WindowX;
            var wy = Config.AppSettings.WindowY;
            if (wx >= 0 && wy >= 0 && wx + Width <= screen.Value.Width && wy + Height <= screen.Value.Height)
            {
                Location = new Point(wx, wy);
            }
            else
            {
                Location = new Point(
                    screen.Value.Right - Width - 20,
                    screen.Value.Bottom - Height - 20
                );
            }
        }

        // --- Top bar (search + filter) ---
        var topPanel = new Panel
        {
            Height = 36,
            Dock = DockStyle.Top,
            Padding = new Padding(4),
            BackColor = Color.FromArgb(30, 30, 30)
        };

        _searchBox = new TextBox
        {
            Location = new Point(4, 4),
            Size = new Size(180, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle
        };
        _searchBox.TextChanged += OnSearchTextChanged;

        // Placeholder text via enter/leave events
        _searchBox.Enter += (_, _) => { if (_searchBox.Text == " 搜索...") { _searchBox.Text = ""; _searchBox.ForeColor = Color.White; } };
        _searchBox.Leave += (_, _) => { if (string.IsNullOrEmpty(_searchBox.Text.Trim())) { _searchBox.Text = " 搜索..."; _searchBox.ForeColor = Color.Gray; } };
        _searchBox.Text = " 搜索...";
        _searchBox.ForeColor = Color.Gray;

        _appFilter = new ComboBox
        {
            Location = new Point(190, 4),
            Size = new Size(120, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat
        };
        _appFilter.SelectedIndexChanged += OnFilterChanged;

        _groupByApp = new CheckBox
        {
            Text = "分组",
            Location = new Point(316, 5),
            Size = new Size(60, 22),
            ForeColor = Color.LightGray,
            UseVisualStyleBackColor = false,
            BackColor = Color.Transparent
        };
        _groupByApp.CheckedChanged += OnGroupModeChanged;

        topPanel.Controls.Add(_searchBox);
        topPanel.Controls.Add(_appFilter);
        topPanel.Controls.Add(_groupByApp);

        // --- Close button (top-right) ---
        var closeBtn = new Button
        {
            Text = "×",
            Location = new Point(Width - 30, 2),
            Size = new Size(26, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Gray,
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(80, 40, 40) },
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => Hide();
        topPanel.Controls.Add(closeBtn);

        // --- Config button ---
        var configBtn = new Button
        {
            Text = "⚙",
            Location = new Point(Width - 58, 2),
            Size = new Size(26, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Gray,
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(50, 50, 80) },
            Cursor = Cursors.Hand
        };
        configBtn.Click += OnConfigClick;
        topPanel.Controls.Add(configBtn);

        // --- DataGridView ---
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.FromArgb(25, 25, 25),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(50, 50, 50),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            },
            CellBorderStyle = DataGridViewCellBorderStyle.None,
            RowTemplate = { Height = 24 },
            EnableHeadersVisualStyles = false
        };
        _grid.CellContentClick += OnGridCellContentClick;
        _grid.CellValueChanged += OnGridCellValueChanged;
        _grid.CurrentCellDirtyStateChanged += OnGridCurrentCellDirtyStateChanged;

        // Columns
        var chkCol = new DataGridViewCheckBoxColumn
        {
            Name = "colLearned",
            HeaderText = "已记住",
            DataPropertyName = "IsLearned",
            Width = 50,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            FlatStyle = FlatStyle.Standard,
            CellTemplate = new CustomCheckBoxCell()
        };
        var hotkeyCol = new DataGridViewTextBoxColumn
        {
            Name = "colHotkey",
            HeaderText = "快捷键",
            DataPropertyName = "HotkeyString",
            FillWeight = 25,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
        var descCol = new DataGridViewTextBoxColumn
        {
            Name = "colDesc",
            HeaderText = "说明",
            DataPropertyName = "Description",
            FillWeight = 30,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
        var appCol = new DataGridViewTextBoxColumn
        {
            Name = "colApp",
            HeaderText = "程序",
            DataPropertyName = "ApplicationName",
            FillWeight = 25,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
        var countCol = new DataGridViewTextBoxColumn
        {
            Name = "colCount",
            HeaderText = "次数",
            DataPropertyName = "ClickCount",
            FillWeight = 10,
            SortMode = DataGridViewColumnSortMode.Automatic
        };

        _grid.Columns.AddRange(chkCol, hotkeyCol, descCol, appCol, countCol);

        // --- Status label ---
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.Gray,
            BackColor = Color.FromArgb(30, 30, 30),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            Font = new Font("Segoe UI", 8F)
        };

        // Add controls
        Controls.Add(_grid);
        Controls.Add(_statusLabel);
        Controls.Add(topPanel);

        // Events
        KeyPreview = true;
        KeyDown += OnFormKeyDown;
        MouseDown += OnFormMouseDown;
        MouseMove += OnFormMouseMove;
        MouseUp += OnFormMouseUp;
        Deactivate += (_, _) => { if (!_grid.ContainsFocus) Hide(); };
        FormClosing += OnFormClosing;
    }

    private void SetupTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("显示/隐藏", null, (_, _) => ToggleVisible());
        _trayMenu.Items.Add("配置", null, (_, _) => OpenConfig());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("退出", null, (_, _) => Application.Exit());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Kinon - 快捷键查看",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ToggleVisible();
    }

    private void SetupTimers()
    {
        // Refresh timer (5s)
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 5000;
        _refreshTimer.Tick += (_, _) => RefreshData();
        _refreshTimer.Start();

        // Search debounce timer (300ms)
        _searchTimer = new System.Windows.Forms.Timer();
        _searchTimer.Interval = 300;
        _searchTimer.Tick += OnSearchTimerTick;
    }

    // --- Dark theme ---

    private void ApplyDarkTheme()
    {
        BackColor = Color.FromArgb(20, 20, 20);
        TransparencyKey = Color.Empty;
        Opacity = Config.AppSettings.WindowOpacity;
    }

    // --- Data loading & display ---

    private BindingList<HotkeyRow> _rows = new();

    public void RefreshData()
    {
        try
        {
            var dbEntries = HotkeyContext.GetAll();
            var cacheEntries = HotkeyMemoryCache.Instance.GetAll();

            // Merge: DB is baseline, cache adds click counts and new entries
            var merged = new Dictionary<string, HotkeyEntry>(StringComparer.OrdinalIgnoreCase);

            // Add DB entries
            foreach (var entry in dbEntries)
            {
                var key = $"{entry.HotkeyString}|{entry.ApplicationName}";
                merged[key] = entry;
            }

            // Merge cache entries (new entries + pending click counts)
            foreach (var entry in cacheEntries)
            {
                var key = $"{entry.HotkeyString}|{entry.ApplicationName}";
                if (merged.TryGetValue(key, out var existing))
                {
                    existing.ClickCount += entry.ClickCount;
                    if (entry.LastUsed.HasValue && (existing.LastUsed == null || entry.LastUsed > existing.LastUsed))
                        existing.LastUsed = entry.LastUsed;
                }
                else
                {
                    merged[key] = entry;
                }
            }

            // Apply search filter
            var filtered = merged.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(_searchKeyword))
            {
                var kw = _searchKeyword.ToLowerInvariant();
                filtered = filtered.Where(e =>
                    (e.HotkeyString?.ToLowerInvariant().Contains(kw) ?? false) ||
                    (e.Description?.ToLowerInvariant().Contains(kw) ?? false)
                );
            }

            // Apply app filter
            var selectedApp = _appFilter.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedApp) && selectedApp != "所有程序")
            {
                filtered = filtered.Where(e =>
                    string.Equals(e.ApplicationName, selectedApp, StringComparison.OrdinalIgnoreCase));
            }

            // Sort: IsLearned=false first (top), then by ClickCount descending
            var sorted = filtered
                .OrderBy(e => e.IsLearned ? 1 : 0)
                .ThenByDescending(e => e.ClickCount)
                .ToList();

            // If group mode, also sort by ApplicationName then ClickCount descending
            if (_groupMode)
            {
                sorted = filtered
                    .OrderBy(e => e.IsLearned ? 1 : 0)
                    .ThenBy(e => e.ApplicationName)
                    .ThenByDescending(e => e.ClickCount)
                    .ToList();
            }

            // Update UI on UI thread
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateGrid(sorted));
            }
            else
            {
                UpdateGrid(sorted);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OverlayForm.RefreshData error: {ex.Message}");
        }
    }

    private void UpdateGrid(List<HotkeyEntry> entries)
    {
        _grid.SuspendLayout();

        // Store current state
        var prevFirst = _grid.FirstDisplayedScrollingRowIndex;

        _rows = new BindingList<HotkeyRow>();
        var idSet = new HashSet<int>();
        int nextId = -1;

        foreach (var entry in entries)
        {
            int rowId = entry.Id > 0 ? entry.Id : nextId--;
            if (!idSet.Add(rowId))
            {
                // Duplicate Id (from cache entries without DB Id)
                rowId = nextId--;
                idSet.Add(rowId);
            }

            _rows.Add(new HotkeyRow
            {
                RowId = rowId,
                IsLearned = entry.IsLearned,
                HotkeyString = entry.HotkeyString,
                Description = entry.Description ?? "",
                ApplicationName = entry.ApplicationName,
                ClickCount = entry.ClickCount,
                Entry = entry
            });
        }

        // Unbind and rebind to refresh the grid
        _grid.DataSource = null;
        _grid.DataSource = _rows;

        // Restore scroll position
        if (prevFirst >= 0 && prevFirst < _grid.RowCount)
        {
            try { _grid.FirstDisplayedScrollingRowIndex = prevFirst; } catch { }
        }

        _grid.ResumeLayout();

        // Update status
        var cacheCount = HotkeyMemoryCache.Instance.Count;
        var totalCount = entries.Count;
        if (cacheCount > 0 && totalCount > 0)
            _statusLabel.Text = $"共 {totalCount} 条   |   缓存待刷 {cacheCount} 条";
        else
            _statusLabel.Text = $"共 {totalCount} 条";

        // Update app filter dropdown
        UpdateAppFilter(entries);
    }

    private void UpdateAppFilter(List<HotkeyEntry> entries)
    {
        var apps = entries
            .Select(e => e.ApplicationName)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a)
            .ToList();

        var current = _appFilter.SelectedItem?.ToString();
        _appFilter.Items.Clear();
        _appFilter.Items.Add("所有程序");
        foreach (var app in apps)
            _appFilter.Items.Add(app);

        // Restore selection
        if (current != null && _appFilter.Items.Contains(current))
            _appFilter.SelectedItem = current;
        else
            _appFilter.SelectedIndex = 0;
    }

    // --- Search debounce ---

    private void OnSearchTextChanged(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnSearchTimerTick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        _searchKeyword = _searchBox.Text.Trim();
        if (_searchKeyword == " 搜索..." || _searchKeyword == "")
            _searchKeyword = string.Empty;
        RefreshData();
    }

    // --- Filters ---

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        RefreshData();
    }

    private void OnGroupModeChanged(object? sender, EventArgs e)
    {
        _groupMode = _groupByApp.Checked;
        RefreshData();
    }

    // --- Grid cell events ---

    private void OnGridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == _grid.Columns["colLearned"]?.Index && e.RowIndex >= 0)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnGridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_grid.CurrentCell is DataGridViewCheckBoxCell)
        {
            _grid.EndEdit();
        }
    }

    private void OnGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == _grid.Columns["colLearned"]?.Index && e.RowIndex >= 0)
        {
            if (_rows != null && e.RowIndex < _rows.Count)
            {
                var row = _rows[e.RowIndex];
                var entry = row.Entry;
                entry.IsLearned = row.IsLearned;

                // Update DB
                Task.Run(() =>
                {
                    try
                    {
                        if (entry.Id > 0)
                            HotkeyContext.Update(entry);
                        else
                            HotkeyContext.Insert(entry);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to update IsLearned: {ex.Message}");
                    }
                });
            }
        }
    }

    // --- Visibility management ---

    private bool _firstActivate = true;

    protected override void SetVisibleCore(bool value)
    {
        if (_firstActivate)
        {
            _firstActivate = false;
            // Load initial data before first show
            RefreshData();
            value = false; // Start hidden
        }
        base.SetVisibleCore(value);
    }

    public void ToggleVisible()
    {
        if (Visible)
        {
            Hide();
        }
        else
        {
            RefreshData();
            Show();
            BringToFront();
        }
    }

    // --- Keyboard events ---

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            RefreshData();
            e.Handled = true;
        }
    }

    // --- Drag support for borderless form ---

    private void OnFormMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragActive = true;
            _dragStart = e.Location;
        }
    }

    private void OnFormMouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragActive && e.Button == MouseButtons.Left)
        {
            Left += e.X - _dragStart.X;
            Top += e.Y - _dragStart.Y;

            // Save position
            Config.AppSettings.WindowX = Left;
            Config.AppSettings.WindowY = Top;
            Config.AppSettings.Save();
        }
    }

    private void OnFormMouseUp(object? sender, MouseEventArgs e)
    {
        _dragActive = false;
    }

    // --- Config button ---

    private void OnConfigClick(object? sender, EventArgs e)
    {
        OpenConfig();
    }

    private void OpenConfig()
    {
        // Hide overlay temporarily
        var wasVisible = Visible;
        Hide();

        using var configForm = new Config.ConfigForm();
        configForm.ShowDialog();

        if (wasVisible)
        {
            RefreshData();
            Show();
        }
    }

    // --- Form closing ---

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.ApplicationExitCall)
            return;
        if (e.CloseReason == CloseReason.UserClosing)
        {
            // X button → hide, not close
            e.Cancel = true;
            Hide();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _searchTimer?.Stop();
            _searchTimer?.Dispose();
            _trayIcon?.Dispose();
            _trayMenu?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// --- Data row for grid ---
public class HotkeyRow
{
    public int RowId { get; set; }
    public bool IsLearned { get; set; }
    public string HotkeyString { get; set; } = "";
    public string Description { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public int ClickCount { get; set; }

    [Browsable(false)]
    public HotkeyEntry Entry { get; set; } = new();
}

// --- Custom check box cell for dark theme ---
public class CustomCheckBoxCell : DataGridViewCheckBoxCell
{
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates elementState, object? value, object? formattedValue, string? errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
        DataGridViewPaintParts paintParts)
    {
        // Use default painting
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState, value, formattedValue,
            errorText, cellStyle, advancedBorderStyle, paintParts);
    }
}
