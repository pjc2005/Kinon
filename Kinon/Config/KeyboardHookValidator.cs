using System.Runtime.InteropServices;

namespace Kinon.Config;

/// <summary>
/// 快捷键验证工具类。
/// </summary>
public static class KeyboardHookValidator
{
    /// <summary>
    /// 检查组合键字符串格式是否合法。合法格式：可选的修饰键 (Ctrl/Alt/Shift/Win) + "+" + 主键。
    /// 示例：Ctrl+Shift+H, Alt+F4, Win+E, F5, Ctrl+Win+S。
    /// </summary>
    public static bool IsValidCombo(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        // 最后一段是主键，前面的都是修饰键
        var mainKey = parts[^1];
        if (string.IsNullOrWhiteSpace(mainKey))
            return false;

        // 验证主键
        if (!IsValidMainKey(mainKey))
            return false;

        // 验证修饰键（去重）
        var mods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var mod = parts[i].ToLowerInvariant();
            if (mod is not ("ctrl" or "alt" or "shift" or "win"))
                return false;
            if (!mods.Add(mod))
                return false; // 重复修饰键
        }

        return true;
    }

    /// <summary>
    /// 检查组合键是否已被系统或其他应用注册为全局热键。
    /// 尝试注册该热键，若失败则说明已被占用。
    /// </summary>
    public static bool IsGlobalHotkeyAvailable(string hotkeyString)
    {
        try
        {
            var (mod, vk) = ParseHotkeyString(hotkeyString);
            if (vk == 0)
                return false;

            // 尝试注册；失败表示被占用
            if (RegisterHotKey(IntPtr.Zero, 100, mod, vk))
            {
                UnregisterHotKey(IntPtr.Zero, 100);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将 Keys 枚举转换为标准组合键字符串（如 "Ctrl+Shift+H"）。
    /// </summary>
    public static string KeysToHotkeyString(Keys keys)
    {
        if (keys == Keys.None)
            return string.Empty;

        var parts = new List<string>();

        if ((keys & Keys.Control) == Keys.Control)
            parts.Add("Ctrl");
        if ((keys & Keys.Alt) == Keys.Alt)
            parts.Add("Alt");
        if ((keys & Keys.Shift) == Keys.Shift)
            parts.Add("Shift");

        var mainKey = keys & Keys.KeyCode;
        if (mainKey != Keys.None)
        {
            var name = MainKeyToString(mainKey);
            if (!string.IsNullOrEmpty(name))
                parts.Add(name);
        }

        return string.Join("+", parts);
    }

    /// <summary>
    /// 将标准组合键字符串解析为 Keys 枚举。
    /// </summary>
    public static Keys HotkeyStringToKeys(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return Keys.None;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        Keys result = Keys.None;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            result |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" => Keys.Control,
                "alt" => Keys.Alt,
                "shift" => Keys.Shift,
                "win" => Keys.LWin,
                _ => Keys.None
            };
        }

        var mainKey = parts[^1].Trim();
        if (Enum.TryParse(mainKey, ignoreCase: true, out Keys parsed))
            result |= parsed & Keys.KeyCode;

        return result;
    }

    // ----- private helpers -----

    private static bool IsValidMainKey(string key)
    {
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
            return true;
        if (key.Length == 1 && key[0] is >= '0' and <= '9')
            return true;

        return key.ToLowerInvariant() switch
        {
            "f1" or "f2" or "f3" or "f4" or "f5" or "f6" or "f7" or "f8" or "f9" or "f10" or "f11" or "f12"
                => true,
            "space" or "enter" or "back" or "delete" or "tab" or "escape" or "home" or "end"
                => true,
            "up" or "down" or "left" or "right"
                => true,
            "printscreen" or "scrolllock" or "pause"
                => true,
            "insert" or "capslock" or "numlock"
                => true,
            "num0" or "num1" or "num2" or "num3" or "num4" or "num5" or "num6" or "num7" or "num8" or "num9"
                => true,
            _ => false
        };
    }

    private static string MainKeyToString(Keys key)
    {
        return key switch
        {
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Back => "Back",
            Keys.Delete => "Delete",
            Keys.Tab => "Tab",
            Keys.Escape => "Escape",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.PrintScreen => "PrintScreen",
            Keys.Scroll => "ScrollLock",
            Keys.Pause => "Pause",
            Keys.Insert => "Insert",
            Keys.CapsLock => "CapsLock",
            Keys.NumLock => "NumLock",
            Keys.NumPad0 => "Num0",
            Keys.NumPad1 => "Num1",
            Keys.NumPad2 => "Num2",
            Keys.NumPad3 => "Num3",
            Keys.NumPad4 => "Num4",
            Keys.NumPad5 => "Num5",
            Keys.NumPad6 => "Num6",
            Keys.NumPad7 => "Num7",
            Keys.NumPad8 => "Num8",
            Keys.NumPad9 => "Num9",
            >= Keys.F1 and <= Keys.F12 => $"F{key - Keys.F1 + 1}",
            >= Keys.A and <= Keys.Z => ((char)('A' + (key - Keys.A))).ToString(),
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
                "alt" => 1,     // MOD_ALT
                "ctrl" => 2,    // MOD_CONTROL
                "shift" => 4,   // MOD_SHIFT
                "win" => 8,     // MOD_WIN
                _ => 0
            };
        }

        var mainKey = parts[^1].Trim().ToUpperInvariant();
        vk = mainKey switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "BACK" => 0x08,
            "DELETE" => 0x2E,
            "TAB" => 0x09,
            "ESCAPE" => 0x1B,
            "HOME" => 0x24,
            "END" => 0x23,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "INSERT" => 0x2D,
            "CAPSLOCK" => 0x14,
            "NUMLOCK" => 0x90,
            "PRINTSCREEN" => 0x2C,
            "SCROLLLOCK" => 0x91,
            "PAUSE" => 0x13,
            "NUM0" => 0x60, "NUM1" => 0x61, "NUM2" => 0x62, "NUM3" => 0x63,
            "NUM4" => 0x64, "NUM5" => 0x65, "NUM6" => 0x66, "NUM7" => 0x67,
            "NUM8" => 0x68, "NUM9" => 0x69,
            _ when mainKey.Length == 1 && mainKey[0] >= 'A' && mainKey[0] <= 'Z' => (uint)(0x41 + mainKey[0] - 'A'),
            _ when mainKey.Length == 1 && mainKey[0] >= '0' && mainKey[0] <= '9' => (uint)(0x30 + mainKey[0] - '0'),
            _ when mainKey.StartsWith("F") && int.TryParse(mainKey[1..], out int fn) && fn >= 1 && fn <= 12 => (uint)(0x6F + fn),
            _ => 0
        };

        return (mod, vk);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
