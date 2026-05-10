using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家背包/道具系统
/// 职责：道具获取/使用/丢弃，装备穿戴/卸下，道具分类管理
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    // 背包物品列表（最大格数）
    // 装备槽：武器、头盔、胸甲、护手、鞋子、饰品1、饰品2
    // 快捷栏物品（药水等）
    // 金币数量

    // 添加物品：AddItem(ItemData item, int count)
    // 移除物品：RemoveItem(int itemId, int count)
    // 使用物品：UseItem(int slotIndex)
    // 装备物品：EquipItem(int slotIndex)
    // 卸下装备：UnequipItem(EquipSlot slot)
    // 检查是否拥有某物品：HasItem(int itemId, int count)
    // 背包排序
}
