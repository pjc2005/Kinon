# Kinon

快捷键查看工具 — 基于无边框窗口模式的全局键盘钩子。

## Architecture
- .NET 8 WinForms desktop app (Windows only)
- Win32 API via P/Invoke: SetWindowsHookEx, GetForegroundWindow, ShowWindow
- SQLite via Microsoft.Data.Sqlite for persistent storage
- ConcurrentDictionary for in-memory hotkey cache, batch-flushed every 30s
- TopMost + ShowWithoutActivation overlay form (no focus stealing)

## Project Structure
```
Kinon/
├── Program.cs                   # Entry point, starts hook + proxy + app context
├── KeyboardHook.cs              # WH_KEYBOARD_LL global hook
├── OverlayForm.cs               # TopMost no-focus overlay with hotkey list
├── ConfigForm.cs                # Settings/management window
├── HotkeyMemoryCache.cs         # Thread-safe memory cache, batch flush
├── Models/
│   └── HotkeyEntry.cs           # Data model
├── Database/
│   └── HotkeyContext.cs         # SQLite CRUD + batch insert/update
├── Config/
│   └── AppSettings.cs           # User settings (hotkey combo, flush interval, etc.)
└── Actions/
    └── ActionExecutor.cs        # Execute custom actions (shutdown, run, open URL)
```

## Code Standards
- File-scoped namespaces
- Use P/Invoke with DllImport for Win32 APIs (NO第三方 wrapper libraries)
- All public methods have XML doc comments
- WinForms event handlers use `async void` pattern where needed
- SQL parameterization everywhere (no string concatenation)
- Use `using` statements for all IDisposable (SqliteConnection, etc.)
- Thread safety: use lock() or ConcurrentDictionary for shared state
- Batch writes: timer-based flush every 30 seconds from HotkeyMemoryCache to SQLite

## Key Dependencies
- Microsoft.Data.Sqlite (NuGet)
- System.Drawing.Common (included in Windows TFM)
- .NET 8-windows TFM

## Configuration
- Hotkey to show/hide overlay (default: Ctrl+Shift+H)
- Flush interval (default: 30000ms)
- Window opacity (default: 0.85)
- Window position/size

## Database Schema
```sql
CREATE TABLE Hotkeys (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    HotkeyString TEXT NOT NULL,
    Description TEXT,
    ApplicationName TEXT NOT NULL DEFAULT '',
    ClickCount INTEGER NOT NULL DEFAULT 0,
    IsLearned INTEGER NOT NULL DEFAULT 0,
    ActionType TEXT NOT NULL DEFAULT '',
    ActionParam TEXT NOT NULL DEFAULT '',
    LastUsed TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IX_Hotkeys_HotkeyString ON Hotkeys(HotkeyString);
CREATE INDEX IX_Hotkeys_ApplicationName ON Hotkeys(ApplicationName);
```
