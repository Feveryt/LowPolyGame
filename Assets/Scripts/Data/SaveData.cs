using System;
using UnityEngine;

/// <summary>
/// 单槽存档的可序列化运行时数据传输对象。
/// 静态配置仍由 ScriptableObject 提供，此对象只保存会在游玩过程中改变的状态。
/// </summary>
[Serializable]
public sealed class SaveData
{
    // 用于兼容后续数据结构升级的存档版本。
    public int schemaVersion;
    // 使用 UTC Tick 记录的最近保存时间。
    public long savedAtUtcTicks;
    // 保存时所在的 Unity 场景名称。
    public string sceneName;

    // 玩家世界位置的 X 坐标。
    public float playerPositionX;
    // 玩家世界位置的 Y 坐标。
    public float playerPositionY;
    // 玩家世界位置的 Z 坐标。
    public float playerPositionZ;
    // 玩家根节点的水平 Yaw 朝向。
    public float playerYaw;

    // 保存时的当前生命值。
    public float currentHealth;
    // 保存时的当前体力值。
    public float currentStamina;
    // 保存时的当前蓝量。
    public float currentMana;
    // 保存时持有的金币数量。
    public int gold;
    // 按固定背包槽位顺序保存的物品数据。
    public InventorySlotSaveData[] inventorySlots;

    /// <summary>将序列化的位置字段还原为 Unity 向量。</summary>
    public Vector3 PlayerPosition => new Vector3(playerPositionX, playerPositionY, playerPositionZ);
}

/// <summary>
/// 单个背包槽位的轻量存档数据。
/// 只保存物品稳定 ID 和数量，不直接序列化 ScriptableObject 引用。
/// </summary>
[Serializable]
public struct InventorySlotSaveData
{
    // 对应 ItemDefinition 的稳定配置 ID。
    public int itemId;
    // 槽位内的物品数量。
    public int quantity;

    /// <summary>创建一条物品槽位存档记录。</summary>
    public InventorySlotSaveData(int itemId, int quantity)
    {
        this.itemId = itemId;
        this.quantity = quantity;
    }
}
