using System.Diagnostics;
using System.Text.Json;

namespace Kinon.Config;

/// <summary>
/// 用户设置管理 — JSON 序列化到 %APPDATA%/Kinon/settings.json。
/// </summary>
public static class AppSettings
{
    public static Keys OverlayHotkey { get; set; } = Keys.Control | Keys.Shift | Keys.H;
    public static int FlushIntervalMs { get; set; } = 30000;
    public static double WindowOpacity { get; set; } = 0.85;
    public static int WindowWidth { get; set; } = 400;
    public static int WindowHeight { get; set; } = 600;
    public static int WindowX { get; set; } = -1;
    public static int WindowY { get; set; } = -1;
    public static bool IsFirstLaunch { get; set; } = true;

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kinon");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    /// <summary>
    /// 从 JSON 文件加载设置；文件不存在时使用默认值。
    /// </summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data == null) return;

            if (Enum.TryParse(data.OverlayHotkey, out Keys parsed))
                OverlayHotkey = parsed;
            if (data.FlushIntervalMs > 0)
                FlushIntervalMs = data.FlushIntervalMs;
            if (data.WindowOpacity is > 0.1 and <= 1.0)
                WindowOpacity = data.WindowOpacity;
            if (data.WindowWidth > 200)
                WindowWidth = data.WindowWidth;
            if (data.WindowHeight > 200)
                WindowHeight = data.WindowHeight;
            WindowX = data.WindowX;
            WindowY = data.WindowY;
            IsFirstLaunch = data.IsFirstLaunch;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存当前设置到 JSON 文件。
    /// </summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var data = new SettingsData
            {
                OverlayHotkey = OverlayHotkey.ToString(),
                FlushIntervalMs = FlushIntervalMs,
                WindowOpacity = WindowOpacity,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                WindowX = WindowX,
                WindowY = WindowY,
                IsFirstLaunch = IsFirstLaunch
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private class SettingsData
    {
        public string OverlayHotkey { get; set; } = "Control, Shift, H";
        public int FlushIntervalMs { get; set; } = 30000;
        public double WindowOpacity { get; set; } = 0.85;
        public int WindowWidth { get; set; } = 400;
        public int WindowHeight { get; set; } = 600;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public bool IsFirstLaunch { get; set; } = true;
    }
}
