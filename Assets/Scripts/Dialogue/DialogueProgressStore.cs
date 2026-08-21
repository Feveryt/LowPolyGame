using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话完成状态的轻量持久化服务。
/// SaveManager 尚未具备实际序列化实现时，本服务使用 PlayerPrefs 保持对话进度可用。
/// </summary>
public static class DialogueProgressStore
{
    // 本机持久化对话状态的键名。
    private const string SaveKey = "LowPolyGame.DialogueProgress";

    /// <summary>查询对话是否已经完整播放过一次。</summary>
    public static bool IsCompleted(string dialogueId)
    {
        return !string.IsNullOrWhiteSpace(dialogueId) && Load().completedDialogueIds.Contains(dialogueId);
    }

    /// <summary>记录首次完成的对话，并立即写入本机存档。</summary>
    public static void MarkCompleted(string dialogueId)
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
            return;

        DialogueProgressData data = Load();
        if (data.completedDialogueIds.Contains(dialogueId))
            return;

        data.completedDialogueIds.Add(dialogueId);
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>删除所有对话完成标记，供开发阶段重新测试。</summary>
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    // 读取并修复旧版本或空数据中的完成列表。
    private static DialogueProgressData Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        DialogueProgressData data = string.IsNullOrWhiteSpace(json)
            ? new DialogueProgressData()
            : JsonUtility.FromJson<DialogueProgressData>(json);
        data ??= new DialogueProgressData();
        data.completedDialogueIds ??= new List<string>();
        return data;
    }
}
