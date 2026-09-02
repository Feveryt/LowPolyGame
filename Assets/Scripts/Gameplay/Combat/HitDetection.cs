using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用近战命中检测组件，在动画命中帧查找目标并提交伤害请求。
/// </summary>
[DisallowMultipleComponent]
public sealed class HitDetection : MonoBehaviour
{
    // 每轮攻击已经结算过伤害的目标集合。
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    // NonAlloc 检测缓存，避免命中帧产生临时数组。
    private readonly Collider[] overlapResults = new Collider[32];

    /// <summary>开始新的攻击段并清空上一段的命中记录。</summary>
    public void BeginAttack() => hitTargets.Clear();

    /// <summary>在攻击原点前方检测目标并应用本段攻击伤害。</summary>
    public int DetectAndApply(Transform origin, PlayerAttackDefinition attack, PlayerStats attacker)
    {
        if (origin == null || attack == null || attacker == null || !attacker.IsAlive)
            return 0;

        Vector3 center = origin.position + origin.forward * attack.HitForwardOffset;
        int count = Physics.OverlapSphereNonAlloc(center, attack.HitRadius, overlapResults, attack.TargetMask, QueryTriggerInteraction.Ignore);
        int appliedCount = 0;
        for (int index = 0; index < count; index++)
        {
            IDamageable target = overlapResults[index].GetComponentInParent<IDamageable>();
            if (target == null || ReferenceEquals(target, attacker) || !target.IsAlive || !hitTargets.Add(target))
                continue;

            DamageResult result = target.TakeDamage(new DamageRequest(attacker.Attack, attack.DamageMultiplier, attack.AttackType, attack.AttackId, attacker));
            if (result.WasApplied)
                appliedCount++;
        }

        return appliedCount;
    }

    /// <summary>在 Scene 视图绘制默认命中范围，便于调试组件位置。</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 0.8f, 0.8f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.6f);
    }
}