namespace Kinon;

/// <summary>
/// 内存缓存 — 线程安全字典，定时刷新到数据库。
/// </summary>
public static class HotkeyMemoryCache
{
    // TODO: ConcurrentDictionary<string, int>, 定时刷新, GetAndReset
}
