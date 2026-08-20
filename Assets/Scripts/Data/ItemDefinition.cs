using UnityEngine;

/// <summary>
/// 物品的静态分类。
/// </summary>
public enum ItemCategory
{
    /// <summary>可以直接使用并产生资源效果的物品。</summary>
    Consumable,
    /// <summary>用于合成或任务的材料。</summary>
    Material,
    /// <summary>当前版本仅展示信息的装备物品。</summary>
    Equipment,
}

/// <summary>
/// 消耗品使用时产生的效果类型。
/// </summary>
public enum ItemEffectType
{
    /// <summary>不产生运行时效果。</summary>
    None,
    /// <summary>恢复角色生命。</summary>
    RestoreHealth,
    /// <summary>恢复角色蓝量。</summary>
    RestoreMana,
}

/// <summary>
/// 物品品质，用于颜色和后续筛选扩展。
/// </summary>
public enum ItemQuality
{
    /// <summary>普通品质。</summary>
    Common,
    /// <summary>稀有品质。</summary>
    Rare,
    /// <summary>史诗品质。</summary>
    Epic,
    /// <summary>传说品质。</summary>
    Legendary,
}

/// <summary>
/// 物品的静态配置资产。
/// 运行时持有数量不放在这里，避免不同角色共享状态。
/// </summary>
[CreateAssetMenu(fileName = "ItemDefinition", menuName = "RPG/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    // 物品的稳定配置 ID，供存档和商店引用。
    [Header("基础信息")]
    [SerializeField] private int id;
    // 物品在背包和商店中的显示名称。
    [SerializeField] private string displayName;
    // 背包槽位显示的物品图标。
    [SerializeField] private Sprite icon;
    // 物品分类，决定可用行为和后续筛选。
    [SerializeField] private ItemCategory category;
    // 物品品质，供 UI 展示和后续扩展。
    [SerializeField] private ItemQuality quality;
    // 物品详情描述。
    [TextArea]
    [SerializeField] private string description;

    // 单个槽位允许堆叠的最大数量，装备通常为 1。
    [Header("堆叠与交易")]
    [Min(1)]
    [SerializeField] private int maxStack = 1;
    // 商店购买该物品时的默认价格。
    [Min(0)]
    [SerializeField] private int buyPrice;
    // 商店回收该物品时的默认价格。
    [Min(0)]
    [SerializeField] private int sellPrice;

    // 消耗品的效果配置，非消耗品保持 None。
    [Header("消耗品效果")]
    [SerializeField] private ItemEffectType effectType;
    // 消耗品每次使用的资源恢复量。
    [Min(0f)]
    [SerializeField] private float effectValue;

    /// <summary>物品稳定 ID。</summary>
    public int Id => id;
    /// <summary>物品显示名称。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    /// <summary>物品图标。</summary>
    public Sprite Icon => icon;
    /// <summary>物品分类。</summary>
    public ItemCategory Category => category;
    /// <summary>物品品质。</summary>
    public ItemQuality Quality => quality;
    /// <summary>物品描述。</summary>
    public string Description => description;
    /// <summary>单槽最大堆叠数量。</summary>
    public int MaxStack => Mathf.Max(1, maxStack);
    /// <summary>默认购买价格。</summary>
    public int BuyPrice => Mathf.Max(0, buyPrice);
    /// <summary>默认出售价格。</summary>
    public int SellPrice => Mathf.Max(0, sellPrice);
    /// <summary>使用效果类型。</summary>
    public ItemEffectType EffectType => effectType;
    /// <summary>单次使用的效果数值。</summary>
    public float EffectValue => Mathf.Max(0f, effectValue);

    /// <summary>判断物品是否可以堆叠。</summary>
    public bool IsStackable => MaxStack > 1;
}
