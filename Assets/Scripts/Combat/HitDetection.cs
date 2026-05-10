using UnityEngine;

/// <summary>
/// 打击检测
/// 职责：攻击/技能碰撞体触发检测，收集命中目标，避免同一次攻击命中同一目标多次
/// </summary>
public class HitDetection : MonoBehaviour
{
    // 碰撞体触发器（通常挂在武器或技能特效上）
    // 已命中目标列表（防止穿透多个敌人时重复计算伤害）
    // 攻击帧区间（只在动画关键帧期间检测）
    // 启用检测：EnableDetection()
    // 禁用检测：DisableDetection()
    // 命中回调：OnTriggerEnter(Collider other) -> 过滤条件 -> 计算伤害 -> 应用伤害
    // 过滤条件：阵营（玩家/敌人）、是否存活、是否已有命中记录
    // 伤害结算：调用目标的 TakeDamage 接口
}
