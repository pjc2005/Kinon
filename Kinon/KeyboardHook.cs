using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kinon;

/// <summary>
/// 全局低级键盘钩子 (WH_KEYBOARD_LL) — 捕获系统范围按键组合。
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    // --- Win32 constants ---
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Modifier virtual key codes
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    // --- P/Invoke ---
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // --- KBDLLHOOKSTRUCT ---
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // --- Instance state ---
    private IntPtr _hookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc; // must keep alive to prevent GC

    /// <summary>
    /// 按键捕获事件。参数：(hotkeyString, applicationName)
    /// </summary>
    public event Action<string, string>? OnHotkeyCaptured;

    /// <summary>
    /// 安装全局键盘钩子。
    /// </summary>
    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
            return; // already started

        _hookProc = HookCallbackInner;

        using var curProc = Process.GetCurrentProcess();
        var curModule = curProc.MainModule;

        var moduleHandle = GetModuleHandle(curModule?.ModuleName);
        if (moduleHandle == IntPtr.Zero)
            moduleHandle = Marshal.GetHINSTANCE(typeof(KeyboardHook).Module);

        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _hookProc = null;
            throw new InvalidOperationException($"安装全局键盘钩子失败。错误代码: {error}");
        }
    }

    /// <summary>
    /// 卸载全局键盘钩子。
    /// </summary>
    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _hookProc = null;
    }

    private IntPtr HookCallbackInner(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // nCode < 0 表示钩子必须传递给 CallNextHookEx
        if (nCode < 0)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // 只处理按键按下事件
        if (wParam != (IntPtr)WM_KEYDOWN && wParam != (IntPtr)WM_SYSKEYDOWN)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var vkCode = hookStruct.vkCode;

        // 跳过纯修饰键
        if (IsModifierKey(vkCode))
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        try
        {
            var modifiers = GetModifierString();
            var keyName = GetKeyName(vkCode);

            var hotkey = string.IsNullOrEmpty(modifiers) ? keyName : $"{modifiers}+{keyName}";
            var appName = GetForegroundProcessName();

            OnHotkeyCaptured?.Invoke(hotkey, appName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"KeyboardHook callback error: {ex.Message}");
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsModifierKey(uint vkCode)
    {
        return vkCode switch
        {
            VK_CONTROL or VK_MENU or VK_SHIFT or VK_LWIN or VK_RWIN => true,
            _ => false
        };
    }

    private static string GetModifierString()
    {
        var mods = new List<string>(4);
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods.Add("Ctrl");
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0) mods.Add("Alt");
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods.Add("Shift");
        if (((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0) || ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0))
            mods.Add("Win");
        return string.Join("+", mods);
    }

    /// <summary>
    /// 将虚拟键码映射为可读的名称。不包含修饰键前缀。
    /// </summary>
    private static string GetKeyName(uint vkCode)
    {
        // F1-F24
        if (vkCode >= 0x70 && vkCode <= 0x87)
            return $"F{vkCode - 0x70 + 1}";

        // A-Z
        if (vkCode >= 0x41 && vkCode <= 0x5A)
            return ((char)vkCode).ToString();

        // 0-9 (top row)
        if (vkCode >= 0x30 && vkCode <= 0x39)
            return ((char)vkCode).ToString();

        // NumPad 0-9
        if (vkCode >= 0x60 && vkCode <= 0x69)
            return $"Num{vkCode - 0x60}";

        // OEM keys and special keys
        return vkCode switch
        {
            0x08 => "Back",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            0x20 => "Space",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",
            0x14 => "CapsLock",
            0x2C => "PrintScreen",
            0x91 => "ScrollLock",
            0x13 => "Pause",
            0x90 => "NumLock",
            0x6A => "Multiply",
            0x6B => "Add",
            0x6D => "Subtract",
            0x6E => "Decimal",
            0x6F => "Divide",
            0xBA => "OemSemicolon",
            0xBB => "OemPlus",
            0xBC => "OemComma",
            0xBD => "OemMinus",
            0xBE => "OemPeriod",
            0xBF => "OemQuestion",
            0xC0 => "OemTilde",
            0xDB => "OemOpenBracket",
            0xDC => "OemPipe",
            0xDD => "OemCloseBracket",
            0xDE => "OemQuotes",
            _ => $"VK_{vkCode:X2}"
        };
    }

    /// <summary>
    /// 获取当前前台窗口的进程名。
    /// </summary>
    private static string GetForegroundProcessName()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return "Unknown";

            var threadId = GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0)
                return "Unknown";

            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
