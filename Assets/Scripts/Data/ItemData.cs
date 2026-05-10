using System;
using UnityEngine;

/// <summary>
/// 物品数据定义（ScriptableObject 或从配置表加载）
/// </summary>
[Serializable]
public class ItemData
{
    // 物品唯一 ID
    // 物品名称
    // 物品图标
    // 物品类型枚举：Weapon, Armor, Consumable, Material, QuestItem
    // 品质枚举：Common, Rare, Epic, Legendary
    // 描述文本
    // 堆叠上限
    // 出售价格 / 购买价格

    // ---- 装备属性（武器/防具时有效）----
    // 攻击力加成 / 防御力加成 / 血量加成 / 特殊效果
    // 装备槽位：Weapon, Helmet, Chest, Gloves, Boots, Accessory

    // ---- 消耗品属性 ----
    // 使用效果：回血值、回蓝值、Buff ID、Buff 持续时间
    // 使用冷却
}
