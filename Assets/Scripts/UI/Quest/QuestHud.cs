using UnityEngine;
using UnityEngine.UI;

/// <summary>以最小 HUD 文本显示当前活跃任务及其顺序目标。</summary>
public sealed class QuestHud : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text objectiveText;

    /// <summary>为运行时创建的 HUD 指定两段显示文本。</summary>
    public void Configure(Text title, Text objective)
    {
        titleText = title;
        objectiveText = objective;
    }

    // 订阅任务状态变化并立即刷新首次显示。
    private void OnEnable()
    {
        QuestService.Instance.QuestChanged += Refresh;
        Refresh(string.Empty);
    }

    // 解除全局服务事件订阅。
    private void OnDisable()
    {
        QuestService.Instance.QuestChanged -= Refresh;
    }

    // 从任务服务读取单条当前任务。
    private void Refresh(string _)
    {
        bool hasObjective = QuestService.Instance.TryGetActiveObjective(out string title, out string objective);
        if (titleText != null)
            titleText.text = hasObjective ? title : string.Empty;
        if (objectiveText != null)
            objectiveText.text = hasObjective ? objective : string.Empty;
    }
}
