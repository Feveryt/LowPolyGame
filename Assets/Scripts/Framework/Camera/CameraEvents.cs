using UnityEngine;

/// <summary>
/// 装备状态变更事件
/// 由 PlayerController 在 R 键切换武器时广播，驱动相机模式切换。
/// </summary>
public struct EquipmentChangedEvent
{
    /// <summary>是否处于持武器状态</summary>
    public bool Equipped;

    public EquipmentChangedEvent(bool equipped)
    {
        Equipped = equipped;
    }
}

/// <summary>
/// 锁定目标变更事件
/// 由 LockOnController 广播。Target 为 null 表示解锁。
/// 监听方：CameraModeController（战斗相机环绕）、PlayerController（朝向目标）。
/// </summary>
public struct LockOnTargetChangedEvent
{
    public Transform Target;

    public LockOnTargetChangedEvent(Transform target)
    {
        Target = target;
    }
}
