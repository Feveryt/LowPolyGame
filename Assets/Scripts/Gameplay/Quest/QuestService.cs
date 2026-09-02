using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>运行时任务服务，加载 Resources 配置并保存两条 Demo 任务的状态与目标进度。</summary>
public sealed class QuestService : MonoBehaviour
{
    private const string DefinitionsPath = "Quests";
    private const string SaveKey = "LowPolyGame.QuestProgress";
    private static QuestService instance;
    private readonly Dictionary<string, QuestDefinition> definitions = new Dictionary<string, QuestDefinition>();
    private QuestProgressData progress;

    /// <summary>任务状态或目标进度变化时通知 HUD 和剧情表现层。</summary>
    public event Action<string> QuestChanged;

    /// <summary>全局任务服务，首次访问时自动创建。</summary>
    public static QuestService Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject root = new GameObject(nameof(QuestService));
                instance = root.AddComponent<QuestService>();
            }
            return instance;
        }
    }

    // 加载定义和已保存的轻量运行时进度。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        foreach (QuestDefinition definition in Resources.LoadAll<QuestDefinition>(DefinitionsPath))
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.QuestId))
                definitions[definition.QuestId] = definition;
        }
        progress = Load();
    }

    // 清理静态实例，便于测试与场景重载。
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>返回任务当前状态；未知任务默认不可用。</summary>
    public QuestState GetState(string questId)
    {
        EnsureInitialized();
        QuestProgressEntry entry = GetOrCreateEntry(questId);
        return entry != null ? entry.state : QuestState.Available;
    }

    /// <summary>判断任务是否处于指定状态。</summary>
    public bool IsInState(string questId, QuestState state)
    {
        return !string.IsNullOrWhiteSpace(questId) && GetState(questId) == state;
    }

    /// <summary>只在任务可接取时激活它，重复调用不重置进度。</summary>
    public bool StartQuest(string questId)
    {
        EnsureInitialized();
        QuestProgressEntry entry = GetOrCreateEntry(questId);
        if (entry == null || entry.state != QuestState.Available)
            return false;
        entry.state = QuestState.Active;
        SaveAndNotify(questId);
        return true;
    }

    /// <summary>将可交互物上报的稳定 ID 用于推进当前目标。</summary>
    public bool NotifyInteraction(string targetId)
    {
        EnsureInitialized();
        return NotifyWorldEvent(QuestObjectiveType.Interaction, targetId);
    }

    /// <summary>将死亡敌人的稳定 ID 用于推进当前目标。</summary>
    public bool NotifyEnemyKilled(string targetId)
    {
        EnsureInitialized();
        return NotifyWorldEvent(QuestObjectiveType.EnemyKilled, targetId);
    }

    /// <summary>由对话动作推进一个指定目标，适用于交付等非场景事件。</summary>
    public bool AdvanceObjective(string questId, string objectiveId)
    {
        EnsureInitialized();
        QuestProgressEntry entry = GetOrCreateEntry(questId);
        QuestDefinition definition = GetDefinition(questId);
        if (entry == null || definition == null || entry.state != QuestState.Active)
            return false;
        int index = FindObjectiveIndex(definition, objectiveId);
        if (index != entry.currentObjectiveIndex)
            return false;
        CompleteCurrentObjective(questId, definition, entry);
        return true;
    }

    /// <summary>完成待交付任务，只有所有目标完成后才会生效。</summary>
    public bool SubmitQuest(string questId)
    {
        EnsureInitialized();
        QuestProgressEntry entry = GetOrCreateEntry(questId);
        if (entry == null || entry.state != QuestState.ReadyToTurnIn)
            return false;
        entry.state = QuestState.Completed;
        SaveAndNotify(questId);
        return true;
    }

    /// <summary>获取 HUD 应显示的任务标题与当前目标文本。</summary>
    public bool TryGetActiveObjective(out string title, out string objective)
    {
        EnsureInitialized();
        foreach (QuestDefinition definition in definitions.Values)
        {
            QuestProgressEntry entry = GetOrCreateEntry(definition.QuestId);
            if (entry == null || (entry.state != QuestState.Active && entry.state != QuestState.ReadyToTurnIn))
                continue;
            title = definition.Title;
            objective = entry.state == QuestState.ReadyToTurnIn
                ? "返回记录员处交付"
                : entry.currentObjectiveIndex < definition.Objectives.Count
                ? definition.Objectives[entry.currentObjectiveIndex].Text
                : definition.Description;
            return true;
        }
        title = string.Empty;
        objective = string.Empty;
        return false;
    }

    /// <summary>清除任务进度，供编辑器与自动化测试使用。</summary>
    public static void ClearProgress()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        instance?.ReloadProgress();
    }

    // 将世界事件与每条活跃任务的当前目标进行匹配。
    private bool NotifyWorldEvent(QuestObjectiveType type, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;
        foreach (QuestProgressEntry entry in progress.entries)
        {
            QuestDefinition definition = GetDefinition(entry.questId);
            if (definition == null || entry.state != QuestState.Active || entry.currentObjectiveIndex >= definition.Objectives.Count)
                continue;
            QuestObjectiveDefinition objective = definition.Objectives[entry.currentObjectiveIndex];
            if (objective.Type == type && objective.TargetId == targetId)
            {
                CompleteCurrentObjective(definition.QuestId, definition, entry);
                return true;
            }
        }
        return false;
    }

    // 兼容 EditMode 测试或禁用对象，确保定义缓存和进度先于 API 调用建立。
    private void EnsureInitialized()
    {
        if (progress == null)
            progress = Load();
        if (definitions.Count == 0)
        {
            foreach (QuestDefinition definition in Resources.LoadAll<QuestDefinition>(DefinitionsPath))
                if (definition != null && !string.IsNullOrWhiteSpace(definition.QuestId))
                    definitions[definition.QuestId] = definition;
        }
    }

    // 完成当前顺序目标，并在最后一个目标后切换为待交付。
    private void CompleteCurrentObjective(string questId, QuestDefinition definition, QuestProgressEntry entry)
    {
        entry.currentObjectiveIndex++;
        if (entry.currentObjectiveIndex >= definition.Objectives.Count)
            entry.state = QuestState.ReadyToTurnIn;
        SaveAndNotify(questId);
    }

    // 查找受配置保护的任务定义。
    private QuestDefinition GetDefinition(string questId)
    {
        return !string.IsNullOrWhiteSpace(questId) && definitions.TryGetValue(questId, out QuestDefinition definition)
            ? definition : null;
    }

    // 返回已有存档条目，或为合法任务初始化可接取状态。
    private QuestProgressEntry GetOrCreateEntry(string questId)
    {
        if (GetDefinition(questId) == null)
            return null;
        QuestProgressEntry entry = progress.entries.Find(value => value.questId == questId);
        if (entry != null)
            return entry;
        entry = new QuestProgressEntry { questId = questId, state = QuestState.Available };
        progress.entries.Add(entry);
        return entry;
    }

    // 返回配置中与稳定目标 ID 对应的索引。
    private static int FindObjectiveIndex(QuestDefinition definition, string objectiveId)
    {
        for (int index = 0; index < definition.Objectives.Count; index++)
            if (definition.Objectives[index].ObjectiveId == objectiveId)
                return index;
        return -1;
    }

    // 将进度写入本地并发布增量刷新通知。
    private void SaveAndNotify(string questId)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress));
        PlayerPrefs.Save();
        QuestChanged?.Invoke(questId);
    }

    // 从本地读取进度，并修复旧数据的空列表。
    private QuestProgressData Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        QuestProgressData result = string.IsNullOrWhiteSpace(json) ? new QuestProgressData() : JsonUtility.FromJson<QuestProgressData>(json);
        result ??= new QuestProgressData();
        result.entries ??= new List<QuestProgressEntry>();
        return result;
    }

    // 在清档后恢复空进度并通知所有 UI。
    private void ReloadProgress()
    {
        progress = Load();
        QuestChanged?.Invoke(string.Empty);
    }
}

/// <summary>保存全部任务运行时状态的轻量 DTO。</summary>
[Serializable]
public sealed class QuestProgressData
{
    public List<QuestProgressEntry> entries = new List<QuestProgressEntry>();
}

/// <summary>保存单条任务的状态与顺序目标索引。</summary>
[Serializable]
public sealed class QuestProgressEntry
{
    public string questId;
    public QuestState state;
    public int currentObjectiveIndex;
}
