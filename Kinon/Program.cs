using System.Diagnostics;
using Kinon.Actions;
using Kinon.Config;
using Kinon.Database;

namespace Kinon;

static class Program
{
    private static KeyboardHook? _keyboardHook;
    private static OverlayForm? _overlayForm;
    private static System.Windows.Forms.Timer? _flushTimer;
    private static bool _isExiting;

    /// <summary>
    /// 应用程序入口点。
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Global exception handler
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var msg = $"Kinon 未处理异常:\n{ex?.GetType()}: {ex?.Message}\n{ex?.StackTrace}";
            File.AppendAllText(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Kinon", "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n---\n");
            MessageBox.Show(msg, "Kinon - 崩溃", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        try
        {
            Run();
        }
        catch (Exception ex)
        {
            var msg = $"Kinon 启动失败:\n{ex.GetType()}: {ex.Message}\n{ex.StackTrace}";
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kinon"));
            File.WriteAllText(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Kinon", "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
            MessageBox.Show(msg, "Kinon - 启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Run()
    {
        ApplicationConfiguration.Initialize();

        // 1) Load settings
        AppSettings.Load();
        Debug.WriteLine($"Settings loaded. FlushInterval: {AppSettings.FlushIntervalMs}ms");

        // 2) Initialize database
        try
        {
            HotkeyContext.Initialize();
            Debug.WriteLine($"Database initialized: {HotkeyContext.GetDbPathForDisplay()}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database init failed: {ex.Message}");
        }

        // 3) Pre-load cache from database
        try
        {
            var allEntries = HotkeyContext.GetAll();
            HotkeyMemoryCache.Instance.MergeFromDatabase(allEntries);
            Debug.WriteLine($"Loaded {allEntries.Count} entries into cache from DB");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cache pre-load failed: {ex.Message}");
        }

        // 4) Start keyboard hook
        _keyboardHook = new KeyboardHook();
        _keyboardHook.OnHotkeyCaptured += OnHotkeyCaptured;
        try
        {
            _keyboardHook.Start();
            Debug.WriteLine("Keyboard hook started");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Keyboard hook start failed: {ex.Message}");
            MessageBox.Show($"无法安装全局键盘钩子。\n\n{ex.Message}\n\n请以管理员身份运行或检查系统安全软件设置。",
                "Kinon - 错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // 5) First-launch setup wizard
        if (AppSettings.IsFirstLaunch)
        {
            using var setup = new Config.SetupWizardForm();
            setup.ShowDialog();
            AppSettings.Save();
        }

        // 6) Create overlay form (not passed to Run — stays hidden)
        _overlayForm = new OverlayForm();

        // 7) Start flush timer (periodically persist cache to database)
        _flushTimer = new System.Windows.Forms.Timer();
        _flushTimer.Interval = AppSettings.FlushIntervalMs;
        _flushTimer.Tick += OnFlushTimerTick;
        _flushTimer.Start();
        Debug.WriteLine($"Flush timer started (interval: {AppSettings.FlushIntervalMs}ms)");

        // 8) Handle application exit
        Application.ApplicationExit += OnApplicationExit;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();

        // 9) Run message loop (no main form — tray icon keeps process alive)
        Application.Run();
    }

    /// <summary>
    /// 键盘钩子回调 — 处理按键捕获和覆盖窗口切换。
    /// </summary>
    private static void OnHotkeyCaptured(string hotkey, string appName)
    {
        // Skip events from our own process
        if (string.Equals(appName, "Kinon", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            // Check if this is the toggle hotkey
            var toggleString = KeyboardHookValidator.KeysToHotkeyString(AppSettings.OverlayHotkey);
            if (string.Equals(hotkey, toggleString, StringComparison.OrdinalIgnoreCase))
            {
                if (_overlayForm != null && !_overlayForm.IsDisposed)
                {
                    _overlayForm.BeginInvoke(() => _overlayForm.ToggleVisible());
                }
                return;
            }

            // If overlay is visible, don't record (user is interacting with overlay)
            if (_overlayForm != null && _overlayForm.Visible)
                return;

            // Record the hotkey press
            HotkeyMemoryCache.Instance.Increment(hotkey, appName);

            // Check if there's an associated action to execute
            var entry = HotkeyMemoryCache.Instance.GetEntry(hotkey, appName);
            if (entry == null)
            {
                // Try to find from database
                var dbEntries = HotkeyContext.GetByApplication(appName)
                    .Concat(HotkeyContext.GetByApplication(""))
                    .FirstOrDefault(e =>
                        string.Equals(e.HotkeyString, hotkey, StringComparison.OrdinalIgnoreCase));
                if (dbEntries != null && !string.IsNullOrEmpty(dbEntries.ActionType))
                {
                    ActionExecutor.Execute(dbEntries);
                }
            }
            else if (!string.IsNullOrEmpty(entry.ActionType))
            {
                ActionExecutor.Execute(entry);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnHotkeyCaptured error: {ex.Message}");
        }
    }

    /// <summary>
    /// 定时刷新 — 将内存缓存写入数据库。
    /// </summary>
    private static void OnFlushTimerTick(object? sender, EventArgs e)
    {
        if (_isExiting) return;

        try
        {
            var entries = HotkeyMemoryCache.Instance.GetAndReset();
            if (entries.Count > 0)
            {
                HotkeyContext.BatchUpsert(entries);
                HotkeyMemoryCache.Instance.MergeFromDatabase(HotkeyContext.GetAll());
                Debug.WriteLine($"Flushed {entries.Count} entries to database");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Flush timer error: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用退出清理。
    /// </summary>
    private static void OnApplicationExit(object? sender, EventArgs e)
    {
        Cleanup();
    }

    private static void Cleanup()
    {
        if (_isExiting) return;
        _isExiting = true;

        Debug.WriteLine("Cleaning up...");

        // Stop flush timer
        if (_flushTimer != null)
        {
            _flushTimer.Stop();
            _flushTimer.Dispose();
            _flushTimer = null;
        }

        // Flush remaining cache entries
        try
        {
            var pending = HotkeyMemoryCache.Instance.GetAndReset();
            if (pending.Count > 0)
            {
                HotkeyContext.BatchUpsert(pending);
                Debug.WriteLine($"Flushed {pending.Count} remaining entries on exit");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Final flush error: {ex.Message}");
        }

        // Save window position
        try
        {
            AppSettings.Save();
            Debug.WriteLine("Settings saved");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings save error: {ex.Message}");
        }

        // Stop keyboard hook
        _keyboardHook?.Dispose();
        _keyboardHook = null;

        // Clean up overlay
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.Dispose();
            _overlayForm = null;
        }

        Debug.WriteLine("Cleanup complete");
    }
}
