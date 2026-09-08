using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>任务详情中的顺序目标视图，区分已完成、当前与后续步骤。</summary>
[DisallowMultipleComponent]
public sealed class QuestObjectiveView : MonoBehaviour
{
    // 目标条目的石质背景，用于强调当前步骤。
    [SerializeField] private Image background;
    // 目标状态和描述文本。
    [SerializeField] private TMP_Text objectiveText;

    /// <summary>将只读目标快照映射为完成、当前或后续的视觉样式。</summary>
    public void Configure(QuestObjectiveSnapshot objective)
    {
        if (objective.IsCompleted)
        {
            objectiveText.text = "✓ " + objective.Text;
            objectiveText.color = new Color(0.55f, 0.62f, 0.57f, 1f);
            background.color = new Color(0.18f, 0.22f, 0.18f, 0.46f);
            return;
        }

        if (objective.IsCurrent)
        {
            objectiveText.text = "• " + objective.Text;
            objectiveText.color = new Color(1f, 0.86f, 0.47f, 1f);
            background.color = new Color(0.37f, 0.29f, 0.14f, 0.9f);
            return;
        }

        objectiveText.text = "• " + objective.Text;
        objectiveText.color = new Color(0.82f, 0.81f, 0.75f, 1f);
        background.color = new Color(0.12f, 0.14f, 0.15f, 0.52f);
    }
}
