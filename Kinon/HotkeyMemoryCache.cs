using System.Collections.Concurrent;
using System.Diagnostics;
using Kinon.Models;

namespace Kinon;

/// <summary>
/// 线程安全的内存缓存 — 使用 ConcurrentDictionary 存储快捷键计数，
/// 定时批量刷新到数据库。
/// </summary>
public class HotkeyMemoryCache
{
    private static readonly Lazy<HotkeyMemoryCache> _instance = new(() => new HotkeyMemoryCache());
    public static HotkeyMemoryCache Instance => _instance.Value;

    // key = $"{HotkeyString}|{ApplicationName}"
    private readonly ConcurrentDictionary<string, HotkeyEntry> _cache = new();

    private HotkeyMemoryCache() { }

    private static string BuildKey(string hotkeyString, string appName)
        => $"{hotkeyString}\0{appName}";

    /// <summary>
    /// 递增指定快捷键在指定程序中的计数。自动创建条目（如不存在）。
    /// 同时更新 LastUsed。
    /// </summary>
    public void Increment(string hotkeyString, string appName)
    {
        var key = BuildKey(hotkeyString, appName);
        _cache.AddOrUpdate(key,
            _ => new HotkeyEntry
            {
                HotkeyString = hotkeyString,
                ApplicationName = appName,
                ClickCount = 1,
                Description = null,
                LastUsed = DateTime.Now
            },
            (_, existing) =>
            {
                existing.ClickCount++;
                existing.LastUsed = DateTime.Now;
                return existing;
            });
    }

    /// <summary>
    /// 获取当前内存中所有条目的快照。
    /// </summary>
    public List<HotkeyEntry> GetAll()
    {
        return _cache.Values.Select(e => e.Clone()).ToList();
    }

    /// <summary>
    /// 返回所有条目并清空缓存（供定时刷新使用）。
    /// 调用方应在调用后立即将返回的条目写入数据库并调用 <see cref="MergeFromDatabase"/> 恢复。
    /// </summary>
    public List<HotkeyEntry> GetAndReset()
    {
        var snapshot = _cache.Values.Select(e => e.Clone()).ToList();
        _cache.Clear();
        return snapshot;
    }

    /// <summary>
    /// 获取内存中指定键的条目（不存在则返回 null）。
    /// </summary>
    public HotkeyEntry? GetEntry(string hotkeyString, string appName)
    {
        var key = BuildKey(hotkeyString, appName);
        return _cache.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>
    /// 从数据库加载已有数据到缓存。
    /// 仅添加缓存中不存在的条目；若条目已在缓存中（例如新 Increment 已创建），
    /// 则将数据库中的 ClickCount 加到已有计数之上。
    /// </summary>
    public void MergeFromDatabase(List<HotkeyEntry> databaseEntries)
    {
        foreach (var entry in databaseEntries)
        {
            var key = BuildKey(entry.HotkeyString, entry.ApplicationName);

            // TryAdd: only adds if key doesn't exist → use DB entry as-is
            if (!_cache.TryAdd(key, entry.Clone()))
            {
                // Key already exists in cache (new increments happened after clear).
                // Add DB total click count to the existing (new) clicks.
                _cache.AddOrUpdate(key,
                    _ => entry.Clone(),
                    (_, existing) =>
                    {
                        var combined = entry.Clone();
                        combined.ClickCount += existing.ClickCount;
                        return combined;
                    });
            }
        }
    }

    /// <summary>
    /// 获取缓存的条目总数（调试用）。
    /// </summary>
    public int Count => _cache.Count;
}
