using System.Diagnostics;
using Kinon.Database;
using Kinon.Models;

namespace Kinon.Config;

/// <summary>
/// 配置管理窗口 — 添加/编辑/删除热键，设置动作类型。
/// </summary>
public sealed class ConfigForm : Form
{
    // --- Controls ---
    private DataGridView _grid;
    private Button _addBtn;
    private Button _deleteBtn;
    private Button _saveBtn;
    private Button _cancelBtn;

    // Detail panel controls
    private Panel _detailPanel;
    private TextBox _hotkeyInput;
    private Button _captureBtn;
    private bool _isCapturing;
    private TextBox _descInput;
    private TextBox _appInput;
    private ComboBox _actionTypeCombo;
    private TextBox _actionParamInput;
    private Label _conflictLabel;

    // State
    private List<HotkeyEntry> _entries = new();
    private HotkeyEntry? _selectedEntry;
    private bool _isModified;

    public ConfigForm()
    {
        InitializeComponent();
        LoadData();
    }

    private void InitializeComponent()
    {
        Text = "Kinon - 快捷键管理";
        Size = new Size(680, 520);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        // --- Top toolbar ---
        var toolbar = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = Color.FromArgb(30, 30, 30) };

        _addBtn = new Button
        {
            Text = "+ 添加",
            Location = new Point(8, 5),
            Size = new Size(70, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.LightGreen,
            BackColor = Color.FromArgb(40, 40, 40),
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(60, 60, 60) }
        };
        _addBtn.Click += OnAddClick;

        _deleteBtn = new Button
        {
            Text = "✕ 删除",
            Location = new Point(84, 5),
            Size = new Size(70, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Salmon,
            BackColor = Color.FromArgb(40, 40, 40),
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(60, 60, 60) }
        };
        _deleteBtn.Click += OnDeleteClick;

        _saveBtn = new Button
        {
            Text = "保存",
            Location = new Point(Width - 160, 5),
            Size = new Size(70, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 80, 30),
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(40, 100, 40) }
        };
        _saveBtn.Click += OnSaveClick;

        _cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(Width - 84, 5),
            Size = new Size(70, 26),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.LightGray,
            BackColor = Color.FromArgb(50, 50, 50),
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(60, 60, 60) }
        };
        _cancelBtn.Click += (_, _) => Close();

        toolbar.Controls.Add(_addBtn);
        toolbar.Controls.Add(_deleteBtn);
        toolbar.Controls.Add(_saveBtn);
        toolbar.Controls.Add(_cancelBtn);

        // --- Grid ---
        _grid = new DataGridView
        {
            Location = new Point(8, 42),
            Size = new Size(648, 200),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.Fixed3D,
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
            RowTemplate = { Height = 22 },
            EnableHeadersVisualStyles = false
        };
        _grid.SelectionChanged += OnGridSelectionChanged;

        _grid.Columns.Add("colHotkey", "快捷键");
        _grid.Columns.Add("colDesc", "说明");
        _grid.Columns.Add("colApp", "程序");
        _grid.Columns.Add("colAction", "动作");
        _grid.Columns.Add("colParam", "参数");

        // --- Detail panel ---
        _detailPanel = new Panel
        {
            Location = new Point(8, 250),
            Size = new Size(648, 220),
            BackColor = Color.FromArgb(30, 30, 30),
            BorderStyle = BorderStyle.FixedSingle
        };

        int labelW = 70;
        int inputX = labelW + 8;
        int inputW = 300;
        int rowH = 28;
        int y = 12;

        // Hotkey input
        var hotkeyLabel = new Label { Text = "快捷键:", Location = new Point(8, y + 4), Size = new Size(labelW, 20), ForeColor = Color.LightGray };
        _hotkeyInput = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(180, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };

        _captureBtn = new Button
        {
            Text = "捕获",
            Location = new Point(inputX + 186, y),
            Size = new Size(50, 24),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.LightBlue,
            BackColor = Color.FromArgb(50, 50, 70),
            FlatAppearance = { BorderSize = 0 }
        };
        _captureBtn.Click += OnCaptureClick;

        _conflictLabel = new Label
        {
            Text = "",
            Location = new Point(inputX + 242, y + 4),
            Size = new Size(180, 20),
            ForeColor = Color.Orange
        };

        y += rowH + 4;

        // Description
        var descLabel = new Label { Text = "说明:", Location = new Point(8, y + 4), Size = new Size(labelW, 20), ForeColor = Color.LightGray };
        _descInput = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputW, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle
        };
        _descInput.TextChanged += (_, _) => _isModified = true;

        y += rowH + 4;

        // Application
        var appLabel = new Label { Text = "程序:", Location = new Point(8, y + 4), Size = new Size(labelW, 20), ForeColor = Color.LightGray };
        _appInput = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputW, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle
        };
        _appInput.TextChanged += (_, _) => _isModified = true;

        y += rowH + 4;

        // Action type
        var actionLabel = new Label { Text = "动作:", Location = new Point(8, y + 4), Size = new Size(labelW, 20), ForeColor = Color.LightGray };
        _actionTypeCombo = new ComboBox
        {
            Location = new Point(inputX, y),
            Size = new Size(150, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat
        };
        _actionTypeCombo.Items.AddRange(new[] { "无操作", "关机", "运行程序", "模拟按键", "打开网页" });
        _actionTypeCombo.SelectedIndex = 0;
        _actionTypeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateActionParamHint();
            _isModified = true;
        };

        y += rowH + 4;

        // Action param
        var paramLabel = new Label { Text = "参数:", Location = new Point(8, y + 4), Size = new Size(labelW, 20), ForeColor = Color.LightGray };
        _actionParamInput = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputW, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle
        };
        _actionParamInput.TextChanged += (_, _) => _isModified = true;

        // Hint label for param
        var paramHint = new Label
        {
            Text = "提示: 关机的默认参数为 /s /t 1800",
            Location = new Point(inputX + 4, y + 28),
            Size = new Size(300, 16),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 7.5F)
        };

        // Add controls
        _detailPanel.Controls.AddRange(new Control[]
        {
            hotkeyLabel, _hotkeyInput, _captureBtn, _conflictLabel,
            descLabel, _descInput,
            appLabel, _appInput,
            actionLabel, _actionTypeCombo,
            paramLabel, _actionParamInput, paramHint
        });

        Controls.Add(toolbar);
        Controls.Add(_grid);
        Controls.Add(_detailPanel);

        // Key preview
        KeyPreview = true;
        KeyDown += OnFormKeyDown;
    }

    // --- Data ---

    private void LoadData()
    {
        _entries = HotkeyContext.GetAll();
        ReloadGrid();
    }

    private void ReloadGrid()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var entry in _entries)
        {
            var row = new DataGridViewRow();
            row.CreateCells(_grid,
                entry.HotkeyString,
                entry.Description ?? "",
                entry.ApplicationName,
                FormatActionType(entry.ActionType),
                entry.ActionParam
            );
            row.Tag = entry;
            _grid.Rows.Add(row);
        }

        _grid.ResumeLayout();
        UpdateStatus();
    }

    // --- Selection ---

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count > 0)
        {
            _selectedEntry = _grid.SelectedRows[0].Tag as HotkeyEntry;
            if (_selectedEntry != null)
                ShowEntryDetails(_selectedEntry);
            else
                ClearDetails();
        }
        else
        {
            _selectedEntry = null;
            ClearDetails();
        }
    }

    private void ShowEntryDetails(HotkeyEntry entry)
    {
        _hotkeyInput.Text = entry.HotkeyString;
        _descInput.Text = entry.Description ?? "";
        _appInput.Text = entry.ApplicationName;
        _actionTypeCombo.SelectedIndex = entry.ActionType.ToLowerInvariant() switch
        {
            "shutdown" => 1,
            "run" => 2,
            "keys" => 3,
            "url" => 4,
            _ => 0
        };
        _actionParamInput.Text = entry.ActionParam;
        _conflictLabel.Text = "";
    }

    private void ClearDetails()
    {
        _hotkeyInput.Text = "";
        _descInput.Text = "";
        _appInput.Text = "";
        _actionTypeCombo.SelectedIndex = 0;
        _actionParamInput.Text = "";
        _conflictLabel.Text = "";
    }

    // --- Add ---

    private void OnAddClick(object? sender, EventArgs e)
    {
        var newEntry = new HotkeyEntry
        {
            HotkeyString = "",
            Description = "",
            ApplicationName = "",
            ActionType = "",
            ActionParam = ""
        };

        _entries.Add(newEntry);
        ReloadGrid();

        // Select the new row
        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[_grid.Rows.Count - 1].Selected = true;
        }

        // Focus the capture button for easy hotkey entry
        _captureBtn.Focus();
        _isModified = true;
    }

    // --- Delete ---

    private void OnDeleteClick(object? sender, EventArgs e)
    {
        if (_selectedEntry == null)
            return;

        if (_selectedEntry.Id > 0)
        {
            var result = MessageBox.Show(
                $"确定删除快捷键 \"{_selectedEntry.HotkeyString}\" 吗？",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        _entries.Remove(_selectedEntry);
        if (_selectedEntry.Id > 0)
            HotkeyContext.Delete(_selectedEntry.Id);

        _selectedEntry = null;
        ReloadGrid();
        ClearDetails();
        _isModified = true;
    }

    // --- Save ---

    private void OnSaveClick(object? sender, EventArgs e)
    {
        // Save current editing
        SaveCurrentEntry();

        // Write all changes to DB
        try
        {
            foreach (var entry in _entries)
            {
                if (entry.Id > 0)
                    HotkeyContext.Update(entry);
                else
                    HotkeyContext.Insert(entry);
            }

            _isModified = false;
            MessageBox.Show("保存成功。", "Kinon", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveCurrentEntry()
    {
        if (_selectedEntry == null)
            return;

        var hotkey = _hotkeyInput.Text.Trim();
        if (string.IsNullOrEmpty(hotkey))
        {
            // Clear if no hotkey entered
        }

        _selectedEntry.HotkeyString = hotkey;
        _selectedEntry.Description = _descInput.Text.Trim();
        _selectedEntry.ApplicationName = _appInput.Text.Trim();
        _selectedEntry.ActionType = _actionTypeCombo.SelectedIndex switch
        {
            1 => "shutdown",
            2 => "run",
            3 => "keys",
            4 => "url",
            _ => ""
        };
        _selectedEntry.ActionParam = _actionParamInput.Text.Trim();

        // Check conflict
        CheckConflict(_selectedEntry);
    }

    private void CheckConflict(HotkeyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.HotkeyString))
        {
            _conflictLabel.Text = "";
            return;
        }

        var conflicting = _entries.Find(e =>
            e.Id != entry.Id &&
            string.Equals(e.HotkeyString, entry.HotkeyString, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.ApplicationName, entry.ApplicationName, StringComparison.OrdinalIgnoreCase));

        _conflictLabel.Text = conflicting != null
            ? "⚠ 与已有热键冲突"
            : "";
    }

    // --- Hotkey capture ---

    private void OnCaptureClick(object? sender, EventArgs e)
    {
        _isCapturing = !_isCapturing;

        if (_isCapturing)
        {
            _captureBtn.Text = "按 Esc 取消";
            _captureBtn.ForeColor = Color.Salmon;
            _hotkeyInput.Text = "";
            _hotkeyInput.BackColor = Color.FromArgb(80, 40, 40);
            _hotkeyInput.Focus();
        }
        else
        {
            StopCapturing();
        }
    }

    private void StopCapturing()
    {
        _isCapturing = false;
        _captureBtn.Text = "捕获";
        _captureBtn.ForeColor = Color.LightBlue;
        _hotkeyInput.BackColor = Color.FromArgb(50, 50, 50);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isCapturing)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                StopCapturing();
                return;
            }

            // Build modifier string
            var mods = new List<string>();
            if (e.Control) mods.Add("Ctrl");
            if (e.Alt) mods.Add("Alt");
            if (e.Shift) mods.Add("Shift");

            // Get main key
            var mainKey = e.KeyCode;
            // Filter out modifier keys as main key
            if (mainKey is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
                return;

            var keyName = KeyCodeToString(mainKey);
            var hotkey = mods.Count > 0 ? $"{string.Join("+", mods)}+{keyName}" : keyName;

            _hotkeyInput.Text = hotkey;
            _isModified = true;

            // Save to current entry
            if (_selectedEntry != null)
            {
                _selectedEntry.HotkeyString = hotkey;
                CheckConflict(_selectedEntry);
                // Update grid
                if (_grid.SelectedRows.Count > 0)
                {
                    var idx = _grid.SelectedRows[0].Index;
                    if (idx >= 0 && idx < _grid.Rows.Count)
                    {
                        _grid.Rows[idx].Cells["colHotkey"].Value = hotkey;
                    }
                }
            }

            StopCapturing();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_isModified)
            {
                var result = MessageBox.Show("有未保存的修改，确定退出？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    Close();
            }
            else
            {
                Close();
            }
        }
    }

    // --- Helpers ---

    private static string KeyCodeToString(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z)
            return ((char)('A' + (key - Keys.A))).ToString();
        if (key >= Keys.D0 && key <= Keys.D9)
            return ((char)('0' + (key - Keys.D0))).ToString();
        if (key >= Keys.F1 && key <= Keys.F12)
            return $"F{key - Keys.F1 + 1}";

        return key switch
        {
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Back => "Back",
            Keys.Delete => "Delete",
            Keys.Tab => "Tab",
            Keys.Escape => "Escape",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.Insert => "Insert",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.PrintScreen => "PrintScreen",
            Keys.Scroll => "ScrollLock",
            Keys.Pause => "Pause",
            Keys.CapsLock => "CapsLock",
            Keys.NumLock => "NumLock",
            Keys.NumPad0 => "Num0", Keys.NumPad1 => "Num1", Keys.NumPad2 => "Num2", Keys.NumPad3 => "Num3",
            Keys.NumPad4 => "Num4", Keys.NumPad5 => "Num5", Keys.NumPad6 => "Num6", Keys.NumPad7 => "Num7",
            Keys.NumPad8 => "Num8", Keys.NumPad9 => "Num9",
            Keys.Multiply => "Multiply",
            Keys.Add => "Add",
            Keys.Subtract => "Subtract",
            Keys.Decimal => "Decimal",
            Keys.Divide => "Divide",
            Keys.OemSemicolon => ";",
            Keys.Oemplus => "=",
            Keys.Oemcomma => ",",
            Keys.OemMinus => "-",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.Oemtilde => "`",
            Keys.OemOpenBrackets => "[",
            Keys.OemPipe => "\\",
            Keys.OemCloseBrackets => "]",
            Keys.OemQuotes => "'",
            Keys.LWin or Keys.RWin => "Win",
            _ => key.ToString()
        };
    }

    private static string FormatActionType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "shutdown" => "关机",
            "run" => "运行程序",
            "keys" => "模拟按键",
            "url" => "打开网页",
            _ => "无操作"
        };
    }

    private void UpdateActionParamHint()
    {
        var hint = _detailPanel.Controls.OfType<Label>()
            .FirstOrDefault(l => l.Text.StartsWith("提示"));
        if (hint == null) return;

        hint.Text = _actionTypeCombo.SelectedIndex switch
        {
            1 => "提示: 默认参数为 /s /t 1800",
            2 => "提示: 输入可执行文件路径，如 notepad.exe",
            3 => "提示: 输入按键序列，如 ^c (Ctrl+C)",
            4 => "提示: 输入完整 URL，如 https://example.com",
            _ => ""
        };
    }

    private void UpdateStatus()
    {
        Text = $"Kinon - 快捷键管理 ({_entries.Count} 条)";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _grid?.Dispose();
            _detailPanel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
