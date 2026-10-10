using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包面板的装备格视图：按槽位类型接收拖拽穿戴、点击卸下（放回背包）。
/// 图标显示当前已装备的物品；空置时显示槽位语义图标（素材库图标）或留空。
/// </summary>
public sealed class EquipmentSlotView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    // 槽位类型（武器/盾牌/头盔/护甲/配饰）。
    [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.None;
    // 物品图标显示控件。
    [SerializeField] private Image icon;
    // 槽位名称文本（"武器"/"盾牌"...）。
    [SerializeField] private TMP_Text label;
    // 槽位名称文本（"武器""盾牌""头盔"...）。
    [SerializeField] private TMP_Text labelText;
    // 空置提示文本（"未穿戴"/"未开放"）。
    [SerializeField] private TMP_Text emptyHint;

    // 当前已装备的物品定义。
    private ItemDefinition equippedItem;
    // 交互回调（由背包面板注入）。
    private System.Action<EquipmentSlotView> dropHandler;
    private System.Action<EquipmentSlotView> clickHandler;

    /// <summary>槽位类型。</summary>
    public EquipmentSlotType SlotType => slotType;
    /// <summary>当前已装备的物品。</summary>
    public ItemDefinition EquippedItem => equippedItem;

    /// <summary>注入交互回调。</summary>
    public void Initialize(System.Action<EquipmentSlotView> onDrop, System.Action<EquipmentSlotView> onClicked)
    {
        dropHandler = onDrop;
        clickHandler = onClicked;
    }

    /// <summary>绑定图标显示组件（运行时动态创建槽位时调用）。</summary>
    public void BindIcon(Image iconImage)
    {
        icon = iconImage;
    }

    /// <summary>
    /// 绑定槽位名称文本（运行时动态创建槽位时调用）。
    /// 运行时 AddComponent 的序列化字段为空，必须显式绑定，否则 Configure 设置的名称为空。
    /// </summary>
    public void BindLabel(TMP_Text slotLabel)
    {
        labelText = slotLabel;
    }

    /// <summary>设置槽位类型与标签文案（文案由调用方给出，例如"武器 · 右手"）。</summary>
    public void Configure(EquipmentSlotType type, string displayLabel)
    {
        slotType = type;
        if (labelText != null)
            labelText.text = displayLabel;
    }

    /// <summary>刷新已装备物品的图标显示。</summary>
    public void SetEquipped(ItemDefinition item)
    {
        equippedItem = item;
        if (icon != null)
        {
            icon.enabled = item != null && item.Icon != null;
            icon.sprite = item != null ? item.Icon : null;
        }

        if (emptyHint != null)
            emptyHint.enabled = item == null;
    }

    // 拖拽落点：只有物品槽位类型匹配且可装备时才穿戴。
    public void OnDrop(PointerEventData eventData)
    {
        dropHandler?.Invoke(this);
    }

    // 点击已装备的槽位：通知面板执行卸下。
    public void OnPointerClick(PointerEventData eventData)
    {
        clickHandler?.Invoke(this);
    }

    // 同时兼容旧 UGUI Text 与石质主题使用的 TMP 文本。
    private static void SetText(TMP_Text text, string content)
    {
        if (text != null)
            text.text = content;
    }
}
