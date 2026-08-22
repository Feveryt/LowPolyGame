using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包数据变化的原因。
/// </summary>
public enum InventoryChangeType
{
    /// <summary>完成初始数据创建。</summary>
    Initialized,
    /// <summary>添加物品。</summary>
    Added,
    /// <summary>移除物品。</summary>
    Removed,
    /// <summary>交换或合并槽位。</summary>
    Moved,
    /// <summary>使用物品。</summary>
    Used,
    /// <summary>丢弃物品。</summary>
    Dropped,
    /// <summary>金币发生变化。</summary>
    GoldChanged,
}

/// <summary>
/// 单个背包槽位的运行时数据。
/// </summary>
public readonly struct InventorySlot
{
    /// <summary>槽位中的物品配置。</summary>
    public ItemDefinition Item { get; }
    /// <summary>槽位中的运行时数量。</summary>
    public int Quantity { get; }
    /// <summary>槽位是否为空。</summary>
    public bool IsEmpty => Item == null || Quantity <= 0;

    /// <summary>创建一个物品槽位。</summary>
    public InventorySlot(ItemDefinition item, int quantity)
    {
        Item = item;
        Quantity = Mathf.Max(0, quantity);
    }
}

/// <summary>
/// 背包变化通知，供 UI 和后续商店系统订阅。
/// </summary>
public readonly struct InventoryChangedEvent
{
    /// <summary>变化类型。</summary>
    public InventoryChangeType ChangeType { get; }
    /// <summary>主要受影响的槽位索引。</summary>
    public int SlotIndex { get; }
    /// <summary>次要受影响的槽位索引。</summary>
    public int SecondarySlotIndex { get; }

    /// <summary>创建背包变化通知。</summary>
    public InventoryChangedEvent(InventoryChangeType changeType, int slotIndex = -1, int secondarySlotIndex = -1)
    {
        ChangeType = changeType;
        SlotIndex = slotIndex;
        SecondarySlotIndex = secondarySlotIndex;
    }
}

/// <summary>
/// 与 Unity 场景解耦的背包运行时模型。
/// 负责容量、堆叠、交换、移除和使用前的槽位数据操作。
/// </summary>
public sealed class InventoryModel
{
    // 背包槽位列表，索引稳定以支持手柄选择和未来存档。
    private readonly List<InventorySlot> slots;
    // 背包容量上限。
    public int Capacity { get; }

    /// <summary>背包数据变化事件。</summary>
    public event Action<InventoryChangedEvent> Changed;

    /// <summary>只读访问所有背包槽位。</summary>
    public IReadOnlyList<InventorySlot> Slots => slots;

    /// <summary>创建指定容量的空背包。</summary>
    public InventoryModel(int capacity)
    {
        Capacity = Mathf.Max(1, capacity);
        slots = new List<InventorySlot>(Capacity);
        for (int i = 0; i < Capacity; i++)
            slots.Add(default);
    }

    /// <summary>尝试将指定数量的物品添加到背包。</summary>
    public bool TryAddItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0 || !CanFit(item, amount))
            return false;

        int remaining = amount;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty || slot.Item != item)
                continue;

            int add = Mathf.Min(remaining, item.MaxStack - slot.Quantity);
            if (add <= 0)
                continue;

            slots[i] = new InventorySlot(item, slot.Quantity + add);
            remaining -= add;
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            int add = Mathf.Min(remaining, item.MaxStack);
            slots[i] = new InventorySlot(item, add);
            remaining -= add;
        }

        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Added));
        return remaining == 0;
    }

    /// <summary>尝试从指定槽位移除精确数量的物品。</summary>
    public bool TryRemoveItem(int slotIndex, int amount)
    {
        if (!IsValidIndex(slotIndex) || amount <= 0)
            return false;

        InventorySlot slot = slots[slotIndex];
        if (slot.IsEmpty || amount > slot.Quantity)
            return false;

        int remaining = slot.Quantity - amount;
        slots[slotIndex] = remaining > 0 ? new InventorySlot(slot.Item, remaining) : default;
        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Removed, slotIndex));
        return true;
    }

    /// <summary>尝试合并相同物品或交换两个槽位。</summary>
    public bool TryMoveItem(int fromIndex, int toIndex)
    {
        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex) || fromIndex == toIndex)
            return false;

        InventorySlot from = slots[fromIndex];
        InventorySlot to = slots[toIndex];
        if (from.IsEmpty)
            return false;

        if (!to.IsEmpty && from.Item == to.Item && from.Item.IsStackable)
        {
            int transferable = Mathf.Min(from.Quantity, from.Item.MaxStack - to.Quantity);
            if (transferable > 0)
            {
                slots[toIndex] = new InventorySlot(to.Item, to.Quantity + transferable);
                int remaining = from.Quantity - transferable;
                slots[fromIndex] = remaining > 0 ? new InventorySlot(from.Item, remaining) : default;
                RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Moved, fromIndex, toIndex));
                return true;
            }
        }

        slots[fromIndex] = to;
        slots[toIndex] = from;
        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Moved, fromIndex, toIndex));
        return true;
    }

    /// <summary>获取指定槽位的当前数据。</summary>
    public bool TryGetItem(int slotIndex, out InventorySlot slot)
    {
        if (!IsValidIndex(slotIndex))
        {
            slot = default;
            return false;
        }

        slot = slots[slotIndex];
        return !slot.IsEmpty;
    }

    /// <summary>判断背包中是否拥有足够数量的指定物品。</summary>
    public bool HasItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].Item == item)
                total += slots[i].Quantity;
        }

        return total >= amount;
    }

    /// <summary>尝试使用槽位中的消耗品并更新玩家资源。</summary>
    public bool TryUseItem(int slotIndex, PlayerStats playerStats)
    {
        if (playerStats == null || !IsValidIndex(slotIndex))
            return false;

        InventorySlot slot = slots[slotIndex];
        ItemDefinition item = slot.Item;
        if (slot.IsEmpty || item.Category != ItemCategory.Consumable || item.EffectValue <= 0f)
            return false;

        bool canApply = item.EffectType switch
        {
            ItemEffectType.RestoreHealth => playerStats.CurrentHealth < playerStats.MaxHealth,
            ItemEffectType.RestoreMana => playerStats.CurrentMana < playerStats.MaxMana,
            _ => false,
        };

        if (!canApply)
            return false;

        switch (item.EffectType)
        {
            case ItemEffectType.RestoreHealth:
                playerStats.RestoreHealth(item.EffectValue);
                break;
            case ItemEffectType.RestoreMana:
                playerStats.RestoreMana(item.EffectValue);
                break;
            default:
                return false;
        }

        slots[slotIndex] = slot.Quantity > 1
            ? new InventorySlot(item, slot.Quantity - 1)
            : default;
        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Used, slotIndex));
        return true;
    }

    /// <summary>移除指定槽位中的全部物品，用于丢弃操作。</summary>
    public bool TryDropItem(int slotIndex)
    {
        if (!IsValidIndex(slotIndex) || slots[slotIndex].IsEmpty)
            return false;

        slots[slotIndex] = default;
        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Dropped, slotIndex));
        return true;
    }

    /// <summary>通知背包数据已发生变化。</summary>
    public void RaiseChanged(InventoryChangedEvent changeEvent)
    {
        Changed?.Invoke(changeEvent);
    }

    /// <summary>按固定槽位索引替换背包内容，并在恢复完成后只通知一次。</summary>
    public void RestoreSlots(IReadOnlyList<InventorySlot> restoredSlots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot restoredSlot = restoredSlots != null && i < restoredSlots.Count
                ? restoredSlots[i]
                : default;
            slots[i] = restoredSlot.IsEmpty
                ? default
                : new InventorySlot(restoredSlot.Item, Mathf.Min(restoredSlot.Quantity, restoredSlot.Item.MaxStack));
        }

        RaiseChanged(new InventoryChangedEvent(InventoryChangeType.Initialized));
    }

    // 检查物品数量是否可以完整放入现有堆叠和空槽位。
    private bool CanFit(ItemDefinition item, int amount)
    {
        int remaining = amount;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty && slot.Item == item)
                remaining -= Mathf.Max(0, item.MaxStack - slot.Quantity);
        }

        int emptySlots = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
                emptySlots++;
        }

        return remaining <= emptySlots * item.MaxStack;
    }

    // 检查槽位索引是否处于有效范围。
    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }
}
