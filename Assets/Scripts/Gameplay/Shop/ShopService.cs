using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>商店交易服务，隔离商店规则、玩家背包和商店界面。</summary>
[DisallowMultipleComponent]
public sealed class ShopService : MonoBehaviour
{
    private static ShopService instance;
    private PlayerInventory playerInventory;
    private ShopDefinition activeShop;

    /// <summary>当前场景中的商店服务实例。</summary>
    public static ShopService Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<ShopService>();
            if (instance == null)
            {
                GameObject serviceObject = new GameObject(nameof(ShopService));
                instance = serviceObject.AddComponent<ShopService>();
            }
            return instance;
        }
    }

    /// <summary>当前打开的商店资料。</summary>
    public ShopDefinition ActiveShop => activeShop;
    /// <summary>交易完成或背包变化时通知界面刷新。</summary>
    public event Action Changed;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        playerInventory = FindFirstObjectByType<PlayerInventory>();
    }

    /// <summary>打开指定商店界面。</summary>
    public void OpenShop(ShopDefinition shop)
    {
        if (shop == null)
            return;

        activeShop = shop;
        EnsurePlayerInventory();
        ShopPanel panel = ShopPanel.Instance;
        if (panel == null && UIManager.Instance != null)
        {
            GameObject panelObject = UIManager.Instance.EnsurePanel("ShopPanel");
            panel = panelObject != null ? panelObject.GetComponent<ShopPanel>() : null;
        }
        if (panel == null)
            panel = new GameObject(nameof(ShopPanel), typeof(RectTransform)).AddComponent<ShopPanel>();
        panel.OpenShop(shop, this);
    }

    /// <summary>关闭当前商店并清理活动资料。</summary>
    public void CloseShop()
    {
        activeShop = null;
        ShopPanel.Instance?.CloseShop();
    }

    /// <summary>尝试购买一件商品，并在成功时扣除金币和加入背包。</summary>
    public ShopTransactionResult TryBuy(ShopItemEntry entry)
    {
        return TryBuy(entry, 1);
    }

    /// <summary>尝试批量购买指定数量的商品。</summary>
    public ShopTransactionResult TryBuy(ShopItemEntry entry, int amount)
    {
        EnsurePlayerInventory();
        if (entry == null || entry.Item == null || !entry.CanBuy || amount <= 0)
            return ShopTransactionResult.Failed("该商品不可购买");
        if (playerInventory == null)
            return ShopTransactionResult.Failed("找不到玩家背包");
        if (!playerInventory.CanAddItem(entry.Item, amount))
            return ShopTransactionResult.Failed("背包已满");
        long totalPrice = (long)entry.BuyPrice * amount;
        if (totalPrice > int.MaxValue || !playerInventory.TrySpendGold((int)totalPrice))
            return ShopTransactionResult.Failed("金币不足");
        if (!playerInventory.TryAddItem(entry.Item, amount))
        {
            playerInventory.AddGold((int)totalPrice);
            return ShopTransactionResult.Failed("背包已满");
        }

        Changed?.Invoke();
        return ShopTransactionResult.Success($"购买了 {entry.Item.DisplayName} x{amount}");
    }

    /// <summary>尝试出售一件指定商品，并在成功时从背包移除后增加金币。</summary>
    public ShopTransactionResult TrySell(ShopItemEntry entry)
    {
        return TrySell(entry, 1);
    }

    /// <summary>尝试批量出售指定数量的商品。</summary>
    public ShopTransactionResult TrySell(ShopItemEntry entry, int amount)
    {
        EnsurePlayerInventory();
        if (entry == null || entry.Item == null || !entry.CanSell || amount <= 0)
            return ShopTransactionResult.Failed("该商品不可出售");
        if (playerInventory == null)
            return ShopTransactionResult.Failed("找不到玩家背包");

        IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
        int remaining = amount;
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index].IsEmpty || slots[index].Item != entry.Item)
                continue;
            remaining -= slots[index].Quantity;
            if (remaining <= 0)
                break;
        }

        if (remaining > 0)
            return ShopTransactionResult.Failed("背包中没有足够的该物品");

        remaining = amount;
        for (int index = 0; index < slots.Count && remaining > 0; index++)
        {
            if (slots[index].IsEmpty || slots[index].Item != entry.Item)
                continue;
            int removeAmount = Mathf.Min(remaining, slots[index].Quantity);
            if (!playerInventory.TryRemoveItem(index, removeAmount))
                return ShopTransactionResult.Failed("出售失败");
            remaining -= removeAmount;
        }

        long totalPrice = (long)entry.SellPrice * amount;
        if (totalPrice > 0 && totalPrice <= int.MaxValue)
            playerInventory.AddGold((int)totalPrice);
        Changed?.Invoke();
        return ShopTransactionResult.Success($"出售了 {entry.Item.DisplayName} x{amount}");
    }

    private void EnsurePlayerInventory()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();
    }
}

/// <summary>交易 API 的成功或失败结果。</summary>
public readonly struct ShopTransactionResult
{
    public bool Succeeded { get; }
    public string Message { get; }

    private ShopTransactionResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>创建成功结果。</summary>
    public static ShopTransactionResult Success(string message) => new ShopTransactionResult(true, message);
    /// <summary>创建失败结果。</summary>
    public static ShopTransactionResult Failed(string message) => new ShopTransactionResult(false, message);
}
