using System;
using System.Collections.Generic;

/// <summary>对话系统独立保存的轻量进度数据。</summary>
[Serializable]
public sealed class DialogueProgressData
{
    /// <summary>已完整播放过一次的对话资产 ID 列表。</summary>
    public List<string> completedDialogueIds = new List<string>();
}
