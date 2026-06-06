namespace Kinon.Models;

/// <summary>
/// 快捷键条目数据模型，映射数据库 Hotkeys 表。
/// </summary>
public class HotkeyEntry
{
    public int Id { get; set; }
    public string HotkeyString { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public int ClickCount { get; set; }
    public bool IsLearned { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string ActionParam { get; set; } = string.Empty;
    public DateTime? LastUsed { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not HotkeyEntry other)
            return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id;

    public HotkeyEntry Clone() => new()
    {
        Id = Id,
        HotkeyString = HotkeyString,
        Description = Description,
        ApplicationName = ApplicationName,
        ClickCount = ClickCount,
        IsLearned = IsLearned,
        ActionType = ActionType,
        ActionParam = ActionParam,
        LastUsed = LastUsed
    };
}
