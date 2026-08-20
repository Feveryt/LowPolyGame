using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家运行时背包组件。
/// 负责初始化测试物品、转发背包操作、管理金币，并为 UI 提供事件入口。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    // 第一版固定背包容量，后续可以由角色或背包升级配置覆盖。
    private const int DefaultCapacity = 20;

    // 场景启动时自动加入的测试物品。
    [Header("背包设置")]
    [SerializeField] private InitialItemEntry[] initialItems;
    // 场景启动时的演示金币数量。
    [Min(0)]
    [SerializeField] private int initialGold = 250;

    // 背包运行时模型，不直接序列化到场景。
    private InventoryModel model;
    // 玩家资源组件，用于消耗品效果。
    private PlayerStats playerStats;
    // 当前金币数量。
    private int gold;
    // 防止重新启用组件时重复添加初始物品。
    private bool initialized;

    /// <summary>背包固定容量。</summary>
    public int Capacity => model?.Capacity ?? DefaultCapacity;
    /// <summary>当前金币数量。</summary>
    public int Gold => gold;
    /// <summary>只读访问当前所有槽位。</summary>
    public IReadOnlyList<InventorySlot> Slots => model?.Slots;
    /// <summary>背包槽位或物品状态发生变化时触发。</summary>
    public event Action<InventoryChangedEvent> InventoryChanged;
    /// <summary>金币发生变化时触发。</summary>
    public event Action<int> GoldChanged;

    // 初始化运行时模型和玩家数值引用。
    private void Awake()
    {
        Initialize();
    }

    /// <summary>初始化空背包、测试物品和初始金币。</summary>
    public void Initialize()
    {
        if (initialized)
            return;

        model = new InventoryModel(DefaultCapacity);
        model.Changed += OnModelChanged;
        playerStats = GetComponent<PlayerStats>();
        gold = Mathf.Max(0, initialGold);

        if (initialItems != null)
        {
            for (int i = 0; i < initialItems.Length; i++)
            {
                InitialItemEntry entry = initialItems[i];
                if (entry.Item != null && entry.Amount > 0)
                    model.TryAddItem(entry.Item, entry.Amount);
            }
        }

        initialized = true;
        model.RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Initialized));
        GoldChanged?.Invoke(gold);
    }

    /// <summary>尝试添加指定数量的物品。</summary>
    public bool TryAddItem(ItemDefinition item, int amount)
    {
        EnsureInitialized();
        return model.TryAddItem(item, amount);
    }

    /// <summary>尝试从槽位移除指定数量的物品。</summary>
    public bool TryRemoveItem(int slotIndex, int amount)
    {
        EnsureInitialized();
        return model.TryRemoveItem(slotIndex, amount);
    }

    /// <summary>尝试合并或交换两个背包槽位。</summary>
    public bool TryMoveItem(int fromIndex, int toIndex)
    {
        EnsureInitialized();
        return model.TryMoveItem(fromIndex, toIndex);
    }

    /// <summary>尝试使用选中槽位中的消耗品。</summary>
    public bool TryUseItem(int slotIndex)
    {
        EnsureInitialized();
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        return model.TryUseItem(slotIndex, playerStats);
    }

    /// <summary>尝试丢弃选中槽位中的全部物品。</summary>
    public bool TryDropItem(int slotIndex)
    {
        EnsureInitialized();
        return model.TryDropItem(slotIndex);
    }

    /// <summary>判断背包中是否拥有足够数量的物品。</summary>
    public bool HasItem(ItemDefinition item, int amount)
    {
        EnsureInitialized();
        return model.HasItem(item, amount);
    }

    /// <summary>尝试扣除金币，余额不足时保持不变。</summary>
    public bool TrySpendGold(int amount)
    {
        EnsureInitialized();
        if (amount < 0 || gold < amount)
            return false;

        if (amount == 0)
            return true;

        gold -= amount;
        GoldChanged?.Invoke(gold);
        model.RaiseChanged(new InventoryChangedEvent(InventoryChangeType.GoldChanged));
        return true;
    }

    /// <summary>增加金币并通知 UI。</summary>
    public void AddGold(int amount)
    {
        EnsureInitialized();
        if (amount <= 0)
            return;

        gold += amount;
        GoldChanged?.Invoke(gold);
        model.RaiseChanged(new InventoryChangedEvent(InventoryChangeType.GoldChanged));
    }

    // 转发运行时模型事件，保持 UI 不依赖纯数据模型的生命周期。
    private void OnModelChanged(InventoryChangedEvent changeEvent)
    {
        InventoryChanged?.Invoke(changeEvent);
    }

    // 确保外部接口在异常的生命周期顺序下仍可用。
    private void EnsureInitialized()
    {
        if (!initialized)
            Initialize();
    }

    /// <summary>场景启动时注入的一组物品和数量。</summary>
    [Serializable]
    public struct InitialItemEntry
    {
        // 要加入背包的物品配置。
        public ItemDefinition Item;
        // 要加入的物品数量。
        [Min(1)] public int Amount;
    }
}
