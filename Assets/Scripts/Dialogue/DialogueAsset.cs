using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>对话中台词所属的说话角色。</summary>
public enum DialogueSpeaker
{
    Player,
    Npc,
}

/// <summary>头像在对话框中的固定显示侧。</summary>
public enum DialoguePortraitSide
{
    Left,
    Right,
}

/// <summary>
/// 单个 NPC 的可编辑对话资产。
/// 场景对象只引用本资产，台词、分支和展示资料均独立于场景保存。
/// </summary>
[CreateAssetMenu(menuName = "Low Poly Game/Dialogue/Dialogue Asset", fileName = "Dialogue_")]
public sealed class DialogueAsset : ScriptableObject
{
    // 用于存档和事件追踪的稳定资产标识。
    [SerializeField, HideInInspector] private string dialogueId;
    // NPC 在对话框中显示的名称。
    [SerializeField] private string npcName = "NPC";
    // NPC 发言时使用的头像图片。
    [SerializeField] private Sprite npcPortrait;
    // NPC 头像的固定显示位置。
    [SerializeField] private DialoguePortraitSide npcPortraitSide = DialoguePortraitSide.Left;
    // 首次交互时进入的台词节点 ID。
    [SerializeField] private int entryNodeId = -1;
    // 按编辑器显示顺序保存的全部台词节点。
    [SerializeField] private List<DialogueNode> nodes = new List<DialogueNode>();
    // 首次对话完成后的重复文本。
    [SerializeField, TextArea(2, 5)] private string completionText = "我已经没有更多要说的了。";
    // 首次完成时发送一次的可选事件 ID。
    [SerializeField] private string completionEventId;

    /// <summary>用于存档的稳定对话标识。</summary>
    public string DialogueId => dialogueId;
    /// <summary>NPC 的显示名称。</summary>
    public string NpcName => npcName;
    /// <summary>NPC 的头像。</summary>
    public Sprite NpcPortrait => npcPortrait;
    /// <summary>NPC 头像的显示侧。</summary>
    public DialoguePortraitSide NpcPortraitSide => npcPortraitSide;
    /// <summary>首次对话的入口节点 ID。</summary>
    public int EntryNodeId => entryNodeId;
    /// <summary>按编辑顺序保存的台词节点。</summary>
    public IReadOnlyList<DialogueNode> Nodes => nodes;
    /// <summary>完成后重复显示的 NPC 文本。</summary>
    public string CompletionText => completionText;
    /// <summary>首次完成时可发送的事件 ID。</summary>
    public string CompletionEventId => completionEventId;

    /// <summary>按节点 ID 查找运行时应显示的台词。</summary>
    public DialogueNode GetNode(int nodeId)
    {
        return nodes.Find(node => node.NodeId == nodeId);
    }

    /// <summary>保证新建或复制的资产拥有存档所需的唯一标识。</summary>
    public void EnsureDialogueId()
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
            dialogueId = Guid.NewGuid().ToString("N");
    }

    // 在编辑器保存资产前补齐稳定 ID。
    private void OnValidate()
    {
        EnsureDialogueId();
    }
}

/// <summary>
/// 可在编辑器中排序和跳转的单条台词数据。
/// 无选项节点通过 NextNodeId 前进；有选项的 NPC 节点由选项决定分支。
/// </summary>
[Serializable]
public sealed class DialogueNode
{
    // 同一对话资产内唯一的节点标识。
    [SerializeField] private int nodeId;
    // 当前文本的发言角色。
    [SerializeField] private DialogueSpeaker speaker;
    // 对话框中显示的内容。
    [SerializeField, TextArea(2, 6)] private string text;
    // 节点进入时广播的可选业务事件标识。
    [SerializeField] private string eventId;
    // 无选项节点继续时进入的下一节点，负数表示结束。
    [SerializeField] private int nextNodeId = -1;
    // 仅 NPC 节点使用的玩家回答分支。
    [SerializeField] private List<DialogueChoice> choices = new List<DialogueChoice>();

    /// <summary>节点的唯一标识。</summary>
    public int NodeId => nodeId;
    /// <summary>当前台词的发言角色。</summary>
    public DialogueSpeaker Speaker => speaker;
    /// <summary>当前台词文本。</summary>
    public string Text => text;
    /// <summary>节点进入时发送的事件 ID。</summary>
    public string EventId => eventId;
    /// <summary>无选项节点的默认后继节点。</summary>
    public int NextNodeId => nextNodeId;
    /// <summary>NPC 节点可提供的玩家选项。</summary>
    public IReadOnlyList<DialogueChoice> Choices => choices;
}

/// <summary>NPC 台词后可供玩家选择的一条回答及其跳转目标。</summary>
[Serializable]
public sealed class DialogueChoice
{
    // 玩家在选项列表中看到并说出的回答文本。
    [SerializeField, TextArea(1, 3)] private string text;
    // 选择回答后进入的后续节点，负数表示对话结束。
    [SerializeField] private int targetNodeId = -1;

    /// <summary>玩家回答文本。</summary>
    public string Text => text;
    /// <summary>回答后进入的节点 ID。</summary>
    public int TargetNodeId => targetNodeId;
}
