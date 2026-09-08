using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>任务面板左侧列表的单条任务视图，负责显示状态并将点击或焦点选择回传给面板。</summary>
[DisallowMultipleComponent]
public sealed class QuestListItem : MonoBehaviour, ISelectHandler
{
    // 承接鼠标、键盘和手柄提交事件的任务按钮。
    [SerializeField] private Button button;
    // 当前选中任务使用的石质高亮边框。
    [SerializeField] private Image selectionFrame;
    // 任务标题文本。
    [SerializeField] private TMP_Text titleText;
    // 任务生命周期状态文本。
    [SerializeField] private TMP_Text statusText;

    // 此条目关联的稳定任务 ID。
    private string questId;
    // 选择本条目时由任务面板接收的回调。
    private Action<string> selected;

    /// <summary>条目的可导航按钮。</summary>
    public Button Button => button;
    /// <summary>任务快照对应的稳定标识。</summary>
    public string QuestId => questId;

    /// <summary>绑定任务数据、视觉选中态与选择回调。</summary>
    public void Configure(QuestSnapshot snapshot, bool isSelected, Action<string> onSelected)
    {
        questId = snapshot.QuestId;
        selected = onSelected;
        titleText.text = snapshot.Title;
        statusText.text = GetStatusText(snapshot.State);
        statusText.color = snapshot.State == QuestState.ReadyToTurnIn
            ? new Color(0.61f, 0.86f, 0.48f, 1f)
            : snapshot.State == QuestState.Completed
                ? new Color(0.61f, 0.66f, 0.62f, 1f)
                : new Color(0.91f, 0.8f, 0.51f, 1f);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Select);
        SetSelected(isSelected);
    }

    /// <summary>切换石质选中框的可见性。</summary>
    public void SetSelected(bool isSelected)
    {
        if (selectionFrame != null)
            selectionFrame.enabled = isSelected;
    }

    // 将鼠标或 UI 提交转换为任务 ID 回调。
    private void Select()
    {
        selected?.Invoke(questId);
    }

    /// <summary>响应键盘或手柄焦点进入，令右侧详情与焦点同步。</summary>
    public void OnSelect(BaseEventData eventData)
    {
        selected?.Invoke(questId);
    }

    // 将任务状态转换为面板使用的短标签。
    private static string GetStatusText(QuestState state)
    {
        return state == QuestState.ReadyToTurnIn ? "可交付"
            : state == QuestState.Completed ? "已完成"
            : "进行中";
    }
}
