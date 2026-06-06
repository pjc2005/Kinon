using System.Diagnostics;
using Kinon.Models;

namespace Kinon.Actions;

/// <summary>
/// 动作执行器 — 根据 ActionType 执行对应操作。
/// </summary>
public static class ActionExecutor
{
    /// <summary>
    /// 在独立线程执行条目关联的动作。
    /// </summary>
    public static void Execute(HotkeyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ActionType))
            return;

        var type = entry.ActionType.Trim().ToLowerInvariant();
        var param = entry.ActionParam ?? string.Empty;

        _ = Task.Run(() =>
        {
            try
            {
                switch (type)
                {
                    case "shutdown":
                        RunProcess("shutdown", string.IsNullOrWhiteSpace(param) ? "/s /t 1800" : param);
                        break;

                    case "run":
                        if (!string.IsNullOrWhiteSpace(param))
                            RunProcess(param, string.Empty, useShell: true);
                        break;

                    case "url":
                        if (!string.IsNullOrWhiteSpace(param))
                            RunProcess(param, string.Empty, useShell: true);
                        break;

                    case "keys":
                        if (!string.IsNullOrWhiteSpace(param))
                            SendKeys.SendWait(param);
                        break;

                    // "" or unknown → no-op
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ActionExecutor failed for '{type}': {ex.Message}");
            }
        });
    }

    private static void RunProcess(string fileName, string arguments, bool useShell = false)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = useShell,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }
}
