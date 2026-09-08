using System;
using UnityEngine;

/// <summary>可由设计师配置的商店静态资料，不保存玩家运行时状态。</summary>
[CreateAssetMenu(menuName = "RPG/Shop/Shop Definition", fileName = "Shop_")]
public sealed class ShopDefinition : ScriptableObject
{
    [SerializeField] private string shopId;
    [SerializeField] private string displayName = "商店";
    [SerializeField] private ShopItemEntry[] items;

    /// <summary>商店存档和事件使用的稳定标识。</summary>
    public string ShopId => string.IsNullOrWhiteSpace(shopId) ? name : shopId;
    /// <summary>商店界面显示名称。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    /// <summary>当前商店的商品配置。</summary>
    public ShopItemEntry[] Items => items ?? Array.Empty<ShopItemEntry>();

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(shopId))
            shopId = name;
    }
}

/// <summary>单个商店商品及其交易规则。</summary>
[Serializable]
public sealed class ShopItemEntry
{
    [SerializeField] private ItemDefinition item;
    [SerializeField] private bool canBuy = true;
    [SerializeField] private bool canSell = true;
    [SerializeField, Min(-1)] private int buyPriceOverride = -1;
    [SerializeField, Min(-1)] private int sellPriceOverride = -1;

    /// <summary>商品对应的物品定义。</summary>
    public ItemDefinition Item => item;
    /// <summary>是否允许玩家购买。</summary>
    public bool CanBuy => canBuy;
    /// <summary>是否允许玩家出售。</summary>
    public bool CanSell => canSell;
    /// <summary>购买价，负数表示使用物品默认值。</summary>
    public int BuyPrice => buyPriceOverride >= 0 ? buyPriceOverride : Item != null ? Item.BuyPrice : 0;
    /// <summary>出售价，负数表示使用物品默认值。</summary>
    public int SellPrice => sellPriceOverride >= 0 ? sellPriceOverride : Item != null ? Item.SellPrice : 0;
}
