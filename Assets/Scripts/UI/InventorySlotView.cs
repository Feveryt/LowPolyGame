using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包单个槽位的预制体视图，负责显示物品并将点击、焦点事件转发给背包面板。
/// </summary>
public sealed class InventorySlotView : MonoBehaviour, ISelectHandler
{
    // 槽位的可导航点击控件。
    [SerializeField] private Button button;
    // 物品图标显示控件。
    [SerializeField] private Image icon;
    // 物品堆叠数量显示文本。
    [SerializeField] private Graphic quantityText;
    // 当前槽位获得焦点时显示的边框。
    [SerializeField] private Image selectionImage;

    // 此视图对应的背包槽位索引。
    private int slotIndex;
    // 鼠标或提交操作的选择回调。
    private Action<int> clickHandler;
    // EventSystem 焦点切换后的回调。
    private Action<int> selectHandler;

    /// <summary>供背包面板设置显式 UGUI 导航的按钮。</summary>
    public Button Button => button;

    /// <summary>供背包面板计算滚动位置的槽位区域。</summary>
    public RectTransform RectTransform => transform as RectTransform;

    /// <summary>初始化槽位索引及其交互回调。</summary>
    public void Initialize(int index, Action<int> onClicked, Action<int> onSelected)
    {
        slotIndex = index;
        clickHandler = onClicked;
        selectHandler = onSelected;
        button = button != null ? button : GetComponent<Button>();

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    /// <summary>刷新物品图标、数量和空槽显示。</summary>
    public void SetSlot(InventorySlot slot)
    {
        bool hasItem = !slot.IsEmpty;
        if (icon != null)
        {
            icon.enabled = hasItem && slot.Item.Icon != null;
            icon.sprite = hasItem ? slot.Item.Icon : null;
        }

        if (quantityText != null)
        {
            quantityText.enabled = hasItem && slot.Quantity > 1;
            SetText(quantityText, hasItem ? slot.Quantity.ToString() : string.Empty);
        }

        if (button != null)
            button.interactable = true;
    }

    /// <summary>刷新选中边框的可见状态。</summary>
    public void SetSelected(bool selected)
    {
        if (selectionImage != null)
            selectionImage.enabled = selected;
    }

    /// <summary>响应 EventSystem 焦点变化并通知背包面板。</summary>
    public void OnSelect(BaseEventData eventData)
    {
        selectHandler?.Invoke(slotIndex);
    }

    // 将按钮点击转发为槽位选择。
    private void HandleClick()
    {
        clickHandler?.Invoke(slotIndex);
    }

    // 同时兼容旧 UGUI Text 与石质主题使用的 TMP 文本。
    private static void SetText(Graphic textGraphic, string content)
    {
        if (textGraphic is TMP_Text tmpText)
            tmpText.text = content;
        else if (textGraphic is Text legacyText)
            legacyText.text = content;
    }
}
