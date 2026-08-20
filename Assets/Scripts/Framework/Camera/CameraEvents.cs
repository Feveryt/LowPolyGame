using UnityEngine;

/// <summary>
/// 装备状态变更事件
/// 由 PlayerController 在 R 键切换武器时广播，通知锁定等玩法模块更新可用性。
/// </summary>
public struct EquipmentChangedEvent
{
    /// <summary>是否处于持武器状态</summary>
    public bool Equipped;

    // 使用新的装备状态创建事件数据。
    public EquipmentChangedEvent(bool equipped)
    {
        Equipped = equipped;
    }
}

/// <summary>
/// 锁定目标变更事件
/// 由 LockOnController 广播。Target 为 null 表示解锁。
/// Listeners: CameraModeController (single-camera lock composition) and PlayerController (target-facing).
/// </summary>
public struct LockOnTargetChangedEvent
{
    /// <summary>新的锁定目标，空值表示解除锁定。</summary>
    public Transform Target;

    // 使用新的锁定目标创建事件数据。
    public LockOnTargetChangedEvent(Transform target)
    {
        Target = target;
    }
}
