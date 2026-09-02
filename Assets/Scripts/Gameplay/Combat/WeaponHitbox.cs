using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体武器的触发器命中组件，仅在攻击有效帧开启并向 IDamageable 提交一次伤害。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class WeaponHitbox : MonoBehaviour
{
    // 武器上用作攻击判定的 Trigger Collider。
    [SerializeField] private Collider hitboxCollider;
    // 一次攻击窗口内已经受到伤害的目标，避免多碰撞体重复结算。
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    // 当前攻击的角色数值来源。
    private PlayerStats attacker;
    // 当前攻击使用的伤害与分类配置。
    private PlayerAttackDefinition currentAttack;

    /// <summary>当前武器命中窗口是否已经开启。</summary>
    public bool IsActive => hitboxCollider != null && hitboxCollider.enabled && currentAttack != null;

    // 缓存碰撞体并确保武器在未攻击时不会产生命中。
    private void Awake()
    {
        hitboxCollider = hitboxCollider != null ? hitboxCollider : GetComponent<Collider>();
        if (hitboxCollider != null)
            hitboxCollider.isTrigger = true;
        SetHitboxEnabled(false);
    }

    // 组件禁用时清理攻击上下文，避免复用对象保留旧攻击。
    private void OnDisable()
    {
        EndAttack();
    }

    /// <summary>开始一个新的武器攻击窗口并清空该段的命中记录。</summary>
    public void BeginAttack(PlayerStats owner, PlayerAttackDefinition attack)
    {
        attacker = owner;
        currentAttack = attack;
        hitTargets.Clear();
        SetHitboxEnabled(true);
    }

    /// <summary>关闭武器攻击窗口并清空本轮运行时引用。</summary>
    public void EndAttack()
    {
        SetHitboxEnabled(false);
        hitTargets.Clear();
        attacker = null;
        currentAttack = null;
    }

    // 仅在有效攻击窗口内处理进入武器触发器的目标。
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    // 处理攻击窗口开启时已经与武器重叠的目标。
    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    // 过滤无效目标、去重后通过统一伤害接口结算。
    private void TryApplyDamage(Collider other)
    {
        if (!IsActive || attacker == null || !attacker.IsAlive || other == null)
            return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || ReferenceEquals(target, attacker) || !target.IsAlive || !hitTargets.Add(target))
            return;

        target.TakeDamage(new DamageRequest(
            attacker.Attack,
            currentAttack.DamageMultiplier,
            currentAttack.AttackType,
            currentAttack.AttackId,
            attacker));
    }

    // 安全切换 Collider，兼容 Inspector 尚未绑定的预制体配置阶段。
    private void SetHitboxEnabled(bool enabled)
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = enabled;
    }
}
