using UnityEngine;

/// <summary>
/// 龙 BOSS 攻击的静态配置资产。
/// 撕咬、火球、地火 AoE 与红光龙息的全部数值，以及二阶段狂暴强化由此资产管理。
/// </summary>
[CreateAssetMenu(fileName = "DragonBossAttack_", menuName = "RPG/Enemy/Dragon Boss Attack Definition")]
public sealed class DragonBossAttackDefinition : ScriptableObject
{
    [Header("撕咬（近身）")]
    // 撕咬伤害倍率。
    [SerializeField, Min(0f)] private float biteDamageMultiplier = 1.3f;
    // 撕咬命中点相对攻击原点的本地偏移。
    [SerializeField] private Vector3 biteHitOffset = new Vector3(0f, 1.2f, 2.4f);
    // 撕咬命中半径，单位为米。
    [SerializeField, Min(0.1f)] private float biteRadius = 1.6f;
    // 撕咬伤害判定的时间窗起点（归一化）。
    [SerializeField, Range(0f, 1f)] private float biteHitWindowStart = 0.45f;
    // 撕咬伤害判定的时间窗终点（归一化）。
    [SerializeField, Range(0f, 1f)] private float biteHitWindowEnd = 0.65f;
    // 撕咬格挡需要支付的破防体力。
    [SerializeField, Min(0f)] private float biteGuardStamina = 15f;

    [Header("火球（远程弹道）")]
    // 火球伤害倍率。
    [SerializeField, Min(0f)] private float fireballDamageMultiplier = 1.2f;
    // 火球可选的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float fireballRange = 12f;
    // 火球飞行速度，单位为米每秒。
    [SerializeField, Min(0.1f)] private float fireballSpeed = 12f;
    // 火球最长存活时间，单位为秒。
    [SerializeField, Min(0.1f)] private float fireballLifetime = 2f;
    // 火球冷却时间，单位为秒。
    [SerializeField, Min(0f)] private float fireballCooldown = 4f;
    // 火球格挡需要支付的破防体力。
    [SerializeField, Min(0f)] private float fireballGuardStamina = 18f;
    // 火球弹道的视觉预制体。
    [SerializeField] private GameObject fireballVisualPrefab;
    // 火球发射点相对攻击原点的本地偏移。
    [SerializeField] private Vector3 fireballSpawnOffset = new Vector3(0f, 1.6f, 2f);
    // 火球命中/消失时的爆花粒子预制体。
    [SerializeField] private GameObject fireballImpactPrefab;
    // 龙息喷射的火焰粒子预制体（素材包 VFX-Fire Breath）。
    [SerializeField] private GameObject fireBreathVfxPrefab;

    [Header("地火 AoE（黄光范围技）")]
    // 地火伤害倍率。
    [SerializeField, Min(0f)] private float spellAoEDamageMultiplier = 1.6f;
    // 地火可选的水平距离范围，单位为米。
    [SerializeField] private Vector2 spellAoERange = new Vector2(3f, 9f);
    // 地火影响半径，单位为米。
    [SerializeField, Min(0.1f)] private float spellAoERadius = 2.6f;
    // 地火预警到落下的延迟，单位为秒（玩家可预判走位）。
    [SerializeField, Range(0.3f, 2f)] private float spellAoETelegraphDelay = 0.8f;
    // 地火冷却时间，单位为秒。
    [SerializeField, Min(0f)] private float spellAoECooldown = 7f;
    // 地火格挡需要支付的破防体力。
    [SerializeField, Min(0f)] private float spellAoEGuardStamina = 30f;
    // 地火落地时的地面冲击波预制体（一次性，按橙色染色）。
    [SerializeField] private GameObject spellAoEWavePrefab;
    // 地火落地时的烟尘预制体。
    [SerializeField] private GameObject spellAoEDustPrefab;

    [Header("龙息（红光不可格挡）")]
    // 龙息伤害倍率。
    [SerializeField, Min(0f)] private float fireBreathDamageMultiplier = 2.2f;
    // 龙息有效距离，单位为米。
    [SerializeField, Min(0f)] private float fireBreathRange = 8f;
    // 龙息命中扇形半角，单位为度。
    [SerializeField, Range(10f, 90f)] private float fireBreathHalfAngle = 35f;
    // 龙息冷却时间，单位为秒。
    [SerializeField, Min(0f)] private float fireBreathCooldown = 9f;

    [Header("节奏与阶段")]
    // 撕咬攻击范围，单位为米。
    [SerializeField, Min(0f)] private float attackRange = 4.5f;
    // 攻击后的最短等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMin = 0.8f;
    // 攻击后的最长等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMax = 1.6f;
    // Animator 未进入攻击状态时的保底等待时间，单位为秒。
    [SerializeField, Min(0.05f)] private float actionStartTimeout = 0.5f;
    // 二阶段生命值比例阈值。
    [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthThreshold = 0.6f;
    // 二阶段攻击间隔缩放。
    [SerializeField, Min(0.1f)] private float phaseTwoIntervalScale = 0.75f;
    // 二阶段追击速度缩放。
    [SerializeField, Min(1f)] private float phaseTwoSpeedScale = 1.2f;
    // 二阶段是否解锁龙息与双咬连段。
    [SerializeField] private bool phaseTwoUnlocksBreath = true;
    // 可被伤害的目标层。
    [SerializeField] private LayerMask damageableMask;

    // 撕咬倍率。
    public float BiteDamageMultiplier => biteDamageMultiplier;
    // 撕咬偏移。
    public Vector3 BiteHitOffset => biteHitOffset;
    // 撕咬半径。
    public float BiteRadius => biteRadius;
    // 撕咬时窗起点。
    public float BiteHitWindowStart => biteHitWindowStart;
    // 撕咬时窗终点。
    public float BiteHitWindowEnd => biteHitWindowEnd;
    // 撕咬破防体力。
    public float BiteGuardStamina => biteGuardStamina;
    // 火球倍率。
    public float FireballDamageMultiplier => fireballDamageMultiplier;
    // 火球距离。
    public float FireballRange => fireballRange;
    // 火球速度。
    public float FireballSpeed => fireballSpeed;
    // 火球存活时间。
    public float FireballLifetime => fireballLifetime;
    // 火球冷却。
    public float FireballCooldown => fireballCooldown;
    // 火球破防体力。
    public float FireballGuardStamina => fireballGuardStamina;
    // 火球视觉。
    public GameObject FireballVisualPrefab => fireballVisualPrefab;
    // 火球命中爆花。
    public GameObject FireballImpactPrefab => fireballImpactPrefab;
    // 龙息火焰粒子。
    public GameObject FireBreathVfxPrefab => fireBreathVfxPrefab;
    // 地火落地冲击波。
    public GameObject SpellAoEWavePrefab => spellAoEWavePrefab;
    // 地火落地烟尘。
    public GameObject SpellAoEDustPrefab => spellAoEDustPrefab;
    // 火球发射偏移。
    public Vector3 FireballSpawnOffset => fireballSpawnOffset;
    // 地火倍率。
    public float SpellAoEDamageMultiplier => spellAoEDamageMultiplier;
    // 地火距离范围。
    public Vector2 SpellAoERange => spellAoERange;
    // 地火半径。
    public float SpellAoERadius => spellAoERadius;
    // 地火预警延迟。
    public float SpellAoETelegraphDelay => spellAoETelegraphDelay;
    // 地火冷却。
    public float SpellAoECooldown => spellAoECooldown;
    // 地火破防体力。
    public float SpellAoEGuardStamina => spellAoEGuardStamina;
    // 龙息倍率。
    public float FireBreathDamageMultiplier => fireBreathDamageMultiplier;
    // 龙息距离。
    public float FireBreathRange => fireBreathRange;
    // 龙息半角。
    public float FireBreathHalfAngle => fireBreathHalfAngle;
    // 龙息冷却。
    public float FireBreathCooldown => fireBreathCooldown;
    // 撕咬攻击范围。
    public float AttackRange => attackRange;
    // 攻击后最短等待。
    public float AttackIntervalMin => attackIntervalMin;
    // 攻击后最长等待。
    public float AttackIntervalMax => attackIntervalMax;
    // 动作启动保底。
    public float ActionStartTimeout => actionStartTimeout;
    // 二阶段阈值。
    public float PhaseTwoHealthThreshold => phaseTwoHealthThreshold;
    // 二阶段间隔缩放。
    public float PhaseTwoIntervalScale => phaseTwoIntervalScale;
    // 二阶段速度缩放。
    public float PhaseTwoSpeedScale => phaseTwoSpeedScale;
    // 二阶段是否解锁龙息。
    public bool PhaseTwoUnlocksBreath => phaseTwoUnlocksBreath;
    // 可受击目标层。
    public LayerMask DamageableMask => damageableMask;

    // 在 Inspector 中限制数值为合法值。
    private void OnValidate()
    {
        spellAoERange.x = Mathf.Max(0f, spellAoERange.x);
        spellAoERange.y = Mathf.Max(spellAoERange.x, spellAoERange.y);
        attackIntervalMax = Mathf.Max(attackIntervalMin, attackIntervalMax);
    }
}
