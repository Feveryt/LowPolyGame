using UnityEngine;

/// <summary>
/// 投射物/弹道
/// 职责：飞行、命中检测、伤害结算、命中特效
/// </summary>
public class Projectile : MonoBehaviour
{
    // 飞行速度 / 飞行方向
    // 最大飞行距离 / 飞行时间
    // 伤害信息（DamageInfo）
    // 是否穿透（命中后继续飞行）
    // 是否追踪（跟踪目标）
    // 初始化：Init(Vector3 direction, DamageInfo damage, Transform homingTarget = null)
    // 飞行更新（MoveTowards 或 Rigidbody.velocity）
    // 命中处理：OnHit(Collider target) -> 调用 DamageSystem + 播放命中特效 + 回收
    // 爆炸型弹道（命中后范围伤害）
}
