using UnityEngine;

/// <summary>
/// 玩家任务/追踪系统
/// 职责：接受任务、任务进度追踪、任务完成判定、任务奖励发放
/// </summary>
public class PlayerQuest : MonoBehaviour
{
    // 当前进行中的任务列表
    // 已完成任务列表
    // 接受任务：AcceptQuest(int questId)
    // 更新任务进度：UpdateProgress(int questId, int stepIndex, int amount)
    // 完成任务：CompleteQuest(int questId)
    // 检查触发条件（等级/前置任务等）
    // 任务状态变化事件通知 UI
}
