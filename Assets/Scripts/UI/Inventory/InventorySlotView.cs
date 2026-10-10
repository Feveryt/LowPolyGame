using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包单个槽位的预制体视图，负责显示物品并将点击、焦点、拖拽事件转发给背包面板。
/// 支持拖拽（拖影跟随指针）、拖到其他背包格交换、拖到装备格穿戴、双击直接装备。
/// </summary>
public sealed class InventorySlotView : MonoBehaviour, ISelectHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IDropHandler, IPointerClickHandler
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
    // 拖拽生命周期回调（由背包面板注入）。
    private Action<InventorySlotView> beginDragHandler;
    private Action<InventorySlotView, Vector2> dragHandler;
    private Action<InventorySlotView> endDragHandler;
    // 拖拽落到本格时的回调（来源为另一背包槽）。
    private Action<int, int> dropHandler;
    // 双击装备回调。
    private Action<int> doubleClickHandler;

    /// <summary>供背包面板设置显式 UGUI 导航的按钮。</summary>
    public Button Button => button;

    /// <summary>供背包面板计算滚动位置的槽位区域。</summary>
    public RectTransform RectTransform => transform as RectTransform;

    /// <summary>本视图对应的背包槽位索引（拖拽流程使用）。</summary>
    public int GetSlotIndexForDrag() => slotIndex;

    /// <summary>初始化槽位索引及其交互回调。</summary>
    public void Initialize(int index, Action<int> onClicked, Action<int> onSelected)
    {
        slotIndex = index;
        clickHandler = onClicked;
        selectHandler = onSelected;
        button = button != null ? button : GetComponent<Button>();

        // 选中底图带有不透明中心，必须绘制在物品图标下方以避免遮挡图标。
        if (selectionImage != null)
            selectionImage.transform.SetAsFirstSibling();

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    /// <summary>注入拖拽与双击回调（由背包面板在初始化时调用一次）。</summary>
    public void InitializeDrag(
        Action<InventorySlotView> onBeginDrag,
        Action<InventorySlotView, Vector2> onDrag,
        Action<InventorySlotView> onEndDrag,
        Action<int, int> onDrop,
        Action<int> onDoubleClick)
    {
        beginDragHandler = onBeginDrag;
        dragHandler = onDrag;
        endDragHandler = onEndDrag;
        dropHandler = onDrop;
        doubleClickHandler = onDoubleClick;
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

    // 拖拽开始：通知面板记录来源并创建拖影。
    public void OnBeginDrag(PointerEventData eventData)
    {
        beginDragHandler?.Invoke(this);
    }

    // 拖拽中：拖影跟随指针。
    public void OnDrag(PointerEventData eventData)
    {
        dragHandler?.Invoke(this, eventData.position);
    }

    // 拖拽结束：由面板判定落点结果并销毁拖影。
    public void OnEndDrag(PointerEventData eventData)
    {
        endDragHandler?.Invoke(this);
    }

    // 拖拽落到本格：通知面板做交换或合并。
    public void OnDrop(PointerEventData eventData)
    {
        dropHandler?.Invoke(slotIndex, DragPayload.SourceSlotIndex);
    }

    // 双击装备：EventSystem 的 clickCount 在连击时大于 1。
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
            doubleClickHandler?.Invoke(slotIndex);
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

/// <summary>拖拽过程中的静态载荷：记录来源槽位，供 Drop 目标读取。</summary>
public static class DragPayload
{
    /// <summary>拖拽来源的背包槽位索引；-1 表示来源不是背包格。</summary>
    public static int SourceSlotIndex { get; set; } = -1;
    /// <summary>本次拖拽是否处于进行中。</summary>
    public static bool IsDragging { get; set; }
}
