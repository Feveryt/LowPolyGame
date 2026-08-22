/// <summary>
/// 对话节点进入时发出的类型化事件。
/// 业务系统可通过 QFramework RegisterEvent&lt;DialogueNodeEvent&gt; 监听 EventId 并处理任务、奖励或演出。
/// </summary>
public struct DialogueNodeEvent
{
    /// <summary>策划填写的业务事件标识。</summary>
    public string EventId;
    /// <summary>触发事件的对话资产标识。</summary>
    public string DialogueId;
    /// <summary>触发事件的台词节点标识。</summary>
    public int NodeId;

    /// <summary>使用来源信息构造对话事件。</summary>
    public DialogueNodeEvent(string eventId, string dialogueId, int nodeId)
    {
        EventId = eventId;
        DialogueId = dialogueId;
        NodeId = nodeId;
    }
}
