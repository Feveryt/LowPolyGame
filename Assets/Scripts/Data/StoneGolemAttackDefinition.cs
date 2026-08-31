using UnityEngine;

/// <summary>
/// 石头人近战攻击的静态配置资产。
/// 单拳、双拳的权重、伤害倍率、命中范围和动画保底时间均由此资产管理。
/// </summary>
[CreateAssetMenu(fileName = "StoneGolemAttack_", menuName = "RPG/Enemy/Stone Golem Attack Definition")]
public sealed class StoneGolemAttackDefinition : ScriptableObject
{
    [Header("动作权重")]
    // 随机选择单拳动作时使用的相对权重。
    [SerializeField, Min(0f)] private float punchWeight = 1f;
    // 随机选择双拳动作时使用的相对权重。
    [SerializeField, Min(0f)] private float doublePunchWeight = 1f;

    [Header("攻击节奏")]
    // 可以开始攻击的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float attackRange = 2.2f;
    // 攻击动作结束后到下一次攻击前的最短等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMin = 1f;
    // 攻击动作结束后到下一次攻击前的最长等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMax = 2f;
    // Animator 未进入攻击状态时的保底等待时间，单位为秒。
    [SerializeField, Min(0.05f)] private float actionStartTimeout = 0.5f;

    [Header("伤害")]
    // 单拳攻击传入 DamageSystem 的伤害倍率。
    [SerializeField, Min(0f)] private float punchDamageMultiplier = 1f;
    // 双拳攻击传入 DamageSystem 的伤害倍率。
    [SerializeField, Min(0f)] private float doublePunchDamageMultiplier = 1.5f;

    [Header("命中检测")]
    // 命中球体相对攻击原点的本地偏移，单位为米。
    [SerializeField] private Vector3 meleeHitOffset = new Vector3(0f, 1f, 1.2f);
    // 动画关键帧命中球体的半径，单位为米。
    [SerializeField, Min(0.01f)] private float meleeHitRadius = 0.8f;
    // 可被石头人近战命中的目标层，通常设置为 Player。
    [SerializeField] private LayerMask damageableMask;

    /// <summary>单拳攻击的随机权重。</summary>
    public float PunchWeight => punchWeight;
    /// <summary>双拳攻击的随机权重。</summary>
    public float DoublePunchWeight => doublePunchWeight;
    /// <summary>近战攻击距离。</summary>
    public float AttackRange => attackRange;
    /// <summary>攻击结束后的最短连击等待时间。</summary>
    public float AttackIntervalMin => attackIntervalMin;
    /// <summary>攻击结束后的最长连击等待时间。</summary>
    public float AttackIntervalMax => attackIntervalMax;
    /// <summary>攻击动作未启动时的保底超时时间。</summary>
    public float ActionStartTimeout => actionStartTimeout;
    /// <summary>单拳伤害倍率。</summary>
    public float PunchDamageMultiplier => punchDamageMultiplier;
    /// <summary>双拳伤害倍率。</summary>
    public float DoublePunchDamageMultiplier => doublePunchDamageMultiplier;
    /// <summary>命中球体相对攻击原点的偏移。</summary>
    public Vector3 MeleeHitOffset => meleeHitOffset;
    /// <summary>命中球体半径。</summary>
    public float MeleeHitRadius => meleeHitRadius;
    /// <summary>可受本次攻击伤害的物理层。</summary>
    public LayerMask DamageableMask => damageableMask;

    // 在 Inspector 中将权重、范围、攻击间隔与超时限制为合法值。
    private void OnValidate()
    {
        punchWeight = Mathf.Max(0f, punchWeight);
        doublePunchWeight = Mathf.Max(0f, doublePunchWeight);
        attackRange = Mathf.Max(0f, attackRange);
        attackIntervalMin = Mathf.Max(0f, attackIntervalMin);
        attackIntervalMax = Mathf.Max(attackIntervalMin, attackIntervalMax);
        actionStartTimeout = Mathf.Max(0.05f, actionStartTimeout);
        punchDamageMultiplier = Mathf.Max(0f, punchDamageMultiplier);
        doublePunchDamageMultiplier = Mathf.Max(0f, doublePunchDamageMultiplier);
        meleeHitRadius = Mathf.Max(0.01f, meleeHitRadius);
    }
}
