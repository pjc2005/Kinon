using System.Runtime.InteropServices;
using Kinon.Database;

namespace Kinon.Config;

/// <summary>
/// 首次启动配置向导 — 设置呼出快捷键等。
/// </summary>
public sealed class SetupWizardForm : Form
{
    private readonly Label _titleLabel;
    private readonly Label _stepLabel;
    private readonly Label _hotkeyLabel;
    private readonly TextBox _hotkeyInput;
    private readonly Button _captureBtn;
    private readonly Label _conflictLabel;
    private readonly CheckBox _startupCheck;
    private readonly Label _opacityLabel;
    private readonly TrackBar _opacityTrack;
    private readonly Label _opacityValue;
    private readonly Button _finishBtn;
    private readonly Button _skipBtn;

    private bool _isCapturing;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public SetupWizardForm()
    {
        Text = "Kinon - 首次启动设置";
        Size = new Size(460, 360);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        // Header
        _titleLabel = new Label
        {
            Text = "欢迎使用 Kinon 快捷键查看工具",
            Location = new Point(20, 16),
            Size = new Size(420, 28),
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _stepLabel = new Label
        {
            Text = "请设置以下选项，之后随时可以在配置界面修改。",
            Location = new Point(20, 48),
            Size = new Size(420, 20),
            ForeColor = Color.LightGray
        };

        // --- Hotkey setting ---
        _hotkeyLabel = new Label
        {
            Text = "呼出快捷键:",
            Location = new Point(20, 88),
            Size = new Size(100, 24),
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _hotkeyInput = new TextBox
        {
            Location = new Point(120, 86),
            Size = new Size(180, 24),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 50),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            Text = KeyboardHookValidator.KeysToHotkeyString(AppSettings.OverlayHotkey)
        };

        _captureBtn = new Button
        {
            Text = "更改",
            Location = new Point(306, 85),
            Size = new Size(60, 24),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.LightBlue,
            BackColor = Color.FromArgb(50, 50, 70),
            FlatAppearance = { BorderSize = 0 }
        };
        _captureBtn.Click += OnCaptureClick;

        _conflictLabel = new Label
        {
            Text = "",
            Location = new Point(120, 112),
            Size = new Size(260, 18),
            ForeColor = Color.Orange,
            Font = new Font("Segoe UI", 8F)
        };

        // --- Opacity setting ---
        _opacityLabel = new Label
        {
            Text = "弹窗透明度:",
            Location = new Point(20, 140),
            Size = new Size(100, 24),
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _opacityTrack = new TrackBar
        {
            Location = new Point(120, 140),
            Size = new Size(200, 24),
            Minimum = 20,
            Maximum = 100,
            Value = (int)(AppSettings.WindowOpacity * 100),
            TickFrequency = 10,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };
        _opacityTrack.ValueChanged += (_, _) =>
        {
            _opacityValue.Text = $"{_opacityTrack.Value}%";
            AppSettings.WindowOpacity = _opacityTrack.Value / 100.0;
        };

        _opacityValue = new Label
        {
            Text = $"{_opacityTrack.Value}%",
            Location = new Point(326, 140),
            Size = new Size(50, 24),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // --- Auto-start checkbox ---
        _startupCheck = new CheckBox
        {
            Text = "开机自动启动",
            Location = new Point(20, 180),
            Size = new Size(180, 24),
            ForeColor = Color.LightGray,
            BackColor = Color.Transparent,
            UseVisualStyleBackColor = false
        };

        // --- Separator ---
        var sep = new Label
        {
            Location = new Point(20, 220),
            Size = new Size(400, 1),
            BorderStyle = BorderStyle.Fixed3D
        };

        // --- Summary ---
        var summaryLabel = new Label
        {
            Text = "设置完成后，按 Ctrl+Shift+H 即可呼出快捷键弹窗。",
            Location = new Point(20, 236),
            Size = new Size(420, 20),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5F)
        };

        // Buttons
        _skipBtn = new Button
        {
            Text = "跳过",
            Location = new Point(280, 280),
            Size = new Size(70, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.LightGray,
            BackColor = Color.FromArgb(50, 50, 50),
            FlatAppearance = { BorderSize = 0 }
        };
        _skipBtn.Click += (_, _) => Close();

        _finishBtn = new Button
        {
            Text = "完成设置",
            Location = new Point(356, 280),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 80, 30),
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(40, 100, 40) }
        };
        _finishBtn.Click += OnFinishClick;

        // Key preview for hotkey capture
        KeyPreview = true;
        KeyDown += OnFormKeyDown;

        Controls.AddRange(new Control[]
        {
            _titleLabel, _stepLabel,
            _hotkeyLabel, _hotkeyInput, _captureBtn, _conflictLabel,
            _opacityLabel, _opacityTrack, _opacityValue,
            _startupCheck,
            sep, summaryLabel,
            _skipBtn, _finishBtn
        });
    }

    private void OnCaptureClick(object? sender, EventArgs e)
    {
        _isCapturing = !_isCapturing;
        if (_isCapturing)
        {
            _captureBtn.Text = "取消";
            _captureBtn.ForeColor = Color.Salmon;
            _hotkeyInput.Text = "";
            _hotkeyInput.BackColor = Color.FromArgb(80, 40, 40);
            _conflictLabel.Text = "按下你的快捷键组合...";
            _conflictLabel.ForeColor = Color.LightBlue;
        }
        else
        {
            StopCapturing();
        }
    }

    private void StopCapturing()
    {
        _isCapturing = false;
        _captureBtn.Text = "更改";
        _captureBtn.ForeColor = Color.LightBlue;
        _hotkeyInput.BackColor = Color.FromArgb(50, 50, 50);
        _conflictLabel.Text = "";
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isCapturing) return;

        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode == Keys.Escape)
        {
            StopCapturing();
            return;
        }

        // Filter out pure modifier keys
        if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey)
            return;

        // Build modifier string
        var mods = new List<string>();
        if (e.Control) mods.Add("Ctrl");
        if (e.Alt) mods.Add("Alt");
        if (e.Shift) mods.Add("Shift");

        var keyName = KeyCodeToString(e.KeyCode);
        var hotkey = mods.Count > 0 ? $"{string.Join("+", mods)}+{keyName}" : keyName;

        _hotkeyInput.Text = hotkey;

        // Validate the combo
        if (KeyboardHookValidator.IsValidCombo(hotkey))
        {
            // Check if available
            try
            {
                var (mod, vk) = ParseHotkeyString(hotkey);
                if (vk != 0 && RegisterHotKey(this.Handle, 999, mod, vk))
                {
                    UnregisterHotKey(this.Handle, 999);
                    _conflictLabel.Text = $"✓ 快捷键可用";
                    _conflictLabel.ForeColor = Color.LightGreen;
                }
                else
                {
                    _conflictLabel.Text = "⚠ 已被其他程序占用";
                    _conflictLabel.ForeColor = Color.Orange;
                }
            }
            catch
            {
                _conflictLabel.Text = "✓ 快捷键可用";
                _conflictLabel.ForeColor = Color.LightGreen;
            }

            // Apply to settings
            AppSettings.OverlayHotkey = KeyboardHookValidator.HotkeyStringToKeys(hotkey);
        }
        else
        {
            _conflictLabel.Text = "⚠ 快捷键格式无效";
            _conflictLabel.ForeColor = Color.Orange;
        }

        StopCapturing();
    }

    private void OnFinishClick(object? sender, EventArgs e)
    {
        // Set startup
        if (_startupCheck.Checked)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    var exePath = Application.ExecutablePath;
                    key.SetValue("Kinon", $"\"{exePath}\"");
                }
            }
            catch { /* silent */ }
        }

        // Mark first launch as done
        AppSettings.IsFirstLaunch = false;
        AppSettings.Save();

        Close();
    }

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
            _ => key.ToString()
        };
    }

    private static (uint mod, uint vk) ParseHotkeyString(string hotkeyString)
    {
        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        uint mod = 0;
        uint vk = 0;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            mod |= parts[i].ToLowerInvariant() switch
            {
                "alt" => 1,
                "ctrl" => 2,
                "shift" => 4,
                "win" => 8,
                _ => 0
            };
        }

        var mainKey = parts[^1].Trim().ToUpperInvariant();
        vk = mainKey switch
        {
            "SPACE" => 0x20, "ENTER" => 0x0D, "BACK" => 0x08,
            "DELETE" => 0x2E, "TAB" => 0x09, "ESCAPE" => 0x1B,
            "HOME" => 0x24, "END" => 0x23, "UP" => 0x26,
            "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
            "INSERT" => 0x2D, "CAPSLOCK" => 0x14, "NUMLOCK" => 0x90,
            "PRINTSCREEN" => 0x2C, "SCROLLLOCK" => 0x91, "PAUSE" => 0x13,
            _ when mainKey.Length == 1 && mainKey[0] >= 'A' && mainKey[0] <= 'Z' => (uint)(0x41 + mainKey[0] - 'A'),
            _ when mainKey.Length == 1 && mainKey[0] >= '0' && mainKey[0] <= '9' => (uint)(0x30 + mainKey[0] - '0'),
            _ when mainKey.StartsWith("F") && int.TryParse(mainKey[1..], out int fn) && fn >= 1 && fn <= 12 => (uint)(0x6F + fn),
            _ => 0
        };

        return (mod, vk);
    }
}
