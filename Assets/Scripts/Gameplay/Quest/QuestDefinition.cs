using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>定义可由任务服务加载的静态任务与目标资料，不保存运行时进度。</summary>
[CreateAssetMenu(menuName = "Low Poly Game/Quest/Quest Definition", fileName = "Quest_")]
public sealed class QuestDefinition : ScriptableObject
{
    [SerializeField] private string questId;
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();

    /// <summary>用于存档与对话动作引用的稳定任务标识。</summary>
    public string QuestId => questId;
    /// <summary>任务列表中显示的标题。</summary>
    public string Title => title;
    /// <summary>任务列表中显示的简短说明。</summary>
    public string Description => description;
    /// <summary>按顺序完成的任务目标。</summary>
    public IReadOnlyList<QuestObjectiveDefinition> Objectives => objectives;

    // 在编辑器中提示缺失的稳定标识。
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(questId))
            questId = name;
    }
}

/// <summary>任务运行时的可持久化状态。</summary>
public enum QuestState
{
    Available,
    Active,
    ReadyToTurnIn,
    Completed,
}

/// <summary>单个任务目标响应的世界事件类型。</summary>
public enum QuestObjectiveType
{
    Interaction,
    EnemyKilled,
}

/// <summary>任务定义中的单个顺序目标。</summary>
[Serializable]
public sealed class QuestObjectiveDefinition
{
    [SerializeField] private string objectiveId;
    [SerializeField] private QuestObjectiveType type;
    [SerializeField, TextArea(1, 3)] private string text;
    [SerializeField] private string targetId;
    [SerializeField, Min(1)] private int requiredCount = 1;

    /// <summary>供对话动作精确推进的稳定目标标识。</summary>
    public string ObjectiveId => objectiveId;
    /// <summary>目标监听的世界事件类型。</summary>
    public QuestObjectiveType Type => type;
    /// <summary>HUD 中显示的当前目标文本。</summary>
    public string Text => text;
    /// <summary>交互物或敌人上报的稳定目标标识。</summary>
    public string TargetId => targetId;
    /// <summary>目标完成所需的事件次数。</summary>
    public int RequiredCount => requiredCount;
}
