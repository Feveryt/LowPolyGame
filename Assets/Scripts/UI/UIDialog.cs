using UnityEngine;

/// <summary>
/// 对话界面
/// 职责：显示NPC对话文本、选项分支、自动逐字打印效果
/// </summary>
public class UIDialog : UIPanel
{
    // 对话文本区域（打字机效果）
    // NPC 名字 / 头像
    // 选项按钮列表（当对话有分支时显示）
    // 跳过对话按钮
    // 显示对话：ShowDialog(int dialogId, int startNodeId)
    // 选择选项：SelectOption(int optionIndex)
    // 对话结束事件（触发任务/战斗等）
}
