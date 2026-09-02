using UnityEngine;

/// <summary>区分普通攻击、重攻击和技能的伤害来源类型。</summary>
public enum AttackType
{
    /// <summary>快速普通攻击。</summary>
    LightAttack,
    /// <summary>玩家重攻击。</summary>
    HeavyAttack,
    /// <summary>技能造成的伤害。</summary>
    Skill,
}

/// <summary>玩家攻击的静态命中与伤害配置。</summary>
[CreateAssetMenu(fileName = "PlayerAttack_", menuName = "RPG/Player/Attack Definition")]
public sealed class PlayerAttackDefinition : ScriptableObject
{
    // 用于存档、日志和技能系统识别的稳定攻击 ID。
    [SerializeField, Min(1)] private int attackId = 1;
    // 当前配置对应的攻击类别。
    [SerializeField] private AttackType attackType = AttackType.HeavyAttack;
    // 传给 DamageSystem 的伤害倍率。
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    // 沿攻击原点前方额外偏移的距离，单位为米。
    [SerializeField, Min(0f)] private float hitForwardOffset = 0.8f;
    // OverlapSphere 的检测半径，单位为米。
    [SerializeField, Min(0.01f)] private float hitRadius = 0.8f;
    // 允许本次攻击命中的目标层。
    [SerializeField] private LayerMask targetMask;

    /// <summary>攻击配置的稳定 ID。</summary>
    public int AttackId => attackId;
    /// <summary>攻击类别。</summary>
    public AttackType AttackType => attackType;
    /// <summary>伤害倍率。</summary>
    public float DamageMultiplier => damageMultiplier;
    /// <summary>攻击球沿原点前方的偏移距离。</summary>
    public float HitForwardOffset => hitForwardOffset;
    /// <summary>攻击球半径。</summary>
    public float HitRadius => hitRadius;
    /// <summary>可被命中的目标层。</summary>
    public LayerMask TargetMask => targetMask;

    // 保证攻击配置在 Inspector 中始终保持有效范围。
    private void OnValidate()
    {
        attackId = Mathf.Max(1, attackId);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        hitForwardOffset = Mathf.Max(0f, hitForwardOffset);
        hitRadius = Mathf.Max(0.01f, hitRadius);
    }
}
