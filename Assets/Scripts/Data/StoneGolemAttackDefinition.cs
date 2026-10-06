using UnityEngine;

/// <summary>
/// 石头人攻击的静态配置资产。
/// 涵盖拳法连段、跺地 AoE、震波弹道、跳跃砸地、石肤格挡与二阶段强化的全部数值。
/// </summary>
[CreateAssetMenu(fileName = "StoneGolemAttack_", menuName = "RPG/Enemy/Stone Golem Attack Definition")]
public sealed class StoneGolemAttackDefinition : ScriptableObject
{
    [Header("拳法连段")]
    // 随机选择单拳动作时使用的相对权重。
    [SerializeField, Min(0f)] private float punchWeight = 1f;
    // 随机选择双拳动作时使用的相对权重。
    [SerializeField, Min(0f)] private float doublePunchWeight = 1f;
    // 单拳攻击传入 DamageSystem 的伤害倍率。
    [SerializeField, Min(0f)] private float punchDamageMultiplier = 1f;
    // 双拳攻击传入 DamageSystem 的伤害倍率。
    [SerializeField, Min(0f)] private float doublePunchDamageMultiplier = 1.5f;
    // 是否启用延迟连段（拳法后追加第二段）。
    [SerializeField] private bool enableComboChains = true;
    // 触发延迟连段的概率。
    [SerializeField, Range(0f, 1f)] private float comboChainChance = 0.45f;
    // 连段中相邻两招之间的停顿时间，单位为秒（钓翻滚的延迟连段）。
    [SerializeField, Range(0f, 1.5f)] private float comboLinkDelay = 0.35f;

    [Header("跺地 AoE（黄光）")]
    // 跺地动作作为起手或连段插入的权重。
    [SerializeField, Min(0f)] private float slamWeight = 0.6f;
    // 跺地伤害倍率。
    [SerializeField, Min(0f)] private float slamDamageMultiplier = 1.6f;
    // 跺地 AoE 半径，单位为米。
    [SerializeField, Min(0.1f)] private float slamRadius = 2.8f;
    // 跺地命中点相对攻击原点的本地偏移。
    [SerializeField] private Vector3 slamHitOffset = new Vector3(0f, 0.2f, 1.6f);
    // 跺地格挡需要支付的破防体力。
    [SerializeField, Min(0f)] private float slamGuardStamina = 35f;

    [Header("震波弹道（黄光）")]
    // 震波作为远程选择的权重。
    [SerializeField, Min(0f)] private float shockwaveWeight = 1f;
    // 震波伤害倍率。
    [SerializeField, Min(0f)] private float shockwaveDamageMultiplier = 1.2f;
    // 可以选择震波的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float shockwaveRange = 9f;
    // 震波飞行速度，单位为米每秒。
    [SerializeField, Min(0.1f)] private float shockwaveSpeed = 10f;
    // 震波最长存活时间，单位为秒。
    [SerializeField, Min(0.1f)] private float shockwaveLifetime = 1.2f;
    // 震波格挡需要支付的破防体力。
    [SerializeField, Min(0f)] private float shockwaveGuardStamina = 22f;
    // 震波弹道的视觉预制体（应为石头类模型）；为空时只生成不可见的判定体。
    [SerializeField] private GameObject shockwaveVisualPrefab;
    // 震波发射点相对攻击原点的本地偏移。
    [SerializeField] private Vector3 shockwaveSpawnOffset = new Vector3(0f, 1.2f, 1f);
    // 震波石头的模型缩放。
    [SerializeField, Min(0.01f)] private float shockwaveVisualScale = 0.35f;
    // 震波石头的自转速度，单位为度每秒。
    [SerializeField, Min(0f)] private float shockwaveSpinSpeed = 720f;

    [Header("命中表现（碎石/烟尘/地面波）")]
    // 碎石飞溅使用的模型列表（石头/鹅卵石/砖块等）。
    [SerializeField] private GameObject[] debrisPrefabs;
    // 每次命中爆出的碎石基准数量（跳砸为 1.5 倍）。
    [SerializeField, Min(0f)] private float debrisCount = 10f;
    // 砸地烟尘粒子预制体。
    [SerializeField] private GameObject impactDustPrefab;
    // 地面冲击波预制体（一次性贴地粒子，运行时按招式颜色染色）。
    [SerializeField] private GameObject impactWavePrefab;

    [Header("跳跃砸地（红光不可格挡）")]
    // 近身时选择跳跃砸地的概率。
    [SerializeField, Range(0f, 1f)] private float jumpSlamChance = 0.25f;
    // 可以选择跳跃砸地的最小水平距离，单位为米。
    [SerializeField, Min(0f)] private float jumpSlamMinRange = 2.2f;
    // 可以选择跳跃砸地的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float jumpSlamMaxRange = 4.5f;
    // 跳跃砸地伤害倍率。
    [SerializeField, Min(0f)] private float jumpSlamDamageMultiplier = 2f;
    // 跳跃砸地落地 AoE 半径，单位为米。
    [SerializeField, Min(0.1f)] private float jumpSlamRadius = 3.2f;

    [Header("石肤格挡")]
    // 连续被玩家命中达到该次数后倾向于举盾。
    [SerializeField, Min(1)] private int defendTriggerHits = 2;
    // 举盾持续时间，单位为秒。
    [SerializeField, Range(0.5f, 4f)] private float defendDuration = 1.4f;
    // 石肤正面减伤比例。
    [SerializeField, Range(0f, 1f)] private float defendDamageReduction = 1f;
    // 两次举盾之间的最小间隔，单位为秒。
    [SerializeField, Min(0f)] private float defendCooldown = 7f;
    // 举盾生效的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float defendMaxRange = 4.5f;

    [Header("二阶段（狂暴）")]
    // 生命值比例低于该值进入二阶段。
    [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthThreshold = 0.5f;
    // 二阶段攻击间隔缩放。
    [SerializeField, Min(0.1f)] private float phaseTwoIntervalScale = 0.8f;
    // 二阶段追击速度缩放。
    [SerializeField, Min(1f)] private float phaseTwoSpeedScale = 1.15f;
    // 二阶段是否解锁更长的四段连击。
    [SerializeField] private bool phaseTwoExtendedCombo = true;

    [Header("攻击节奏")]
    // 可以开始攻击的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float attackRange = 2.2f;
    // 攻击动作结束后到下一次攻击前的最短等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMin = 1f;
    // 攻击动作结束后到下一次攻击前的最长等待时间，单位为秒。
    [SerializeField, Min(0f)] private float attackIntervalMax = 2f;
    // Animator 未进入攻击状态时的保底等待时间，单位为秒。
    [SerializeField, Min(0.05f)] private float actionStartTimeout = 0.5f;

    [Header("命中检测")]
    // 命中球体相对攻击原点的本地偏移，单位为米。
    [SerializeField] private Vector3 meleeHitOffset = new Vector3(0f, 1f, 1.2f);
    // 动画关键帧命中球体的半径，单位为米。
    [SerializeField, Min(0.01f)] private float meleeHitRadius = 0.8f;
    // 可被石头人近战命中的目标层，通常设置为 Player。
    [SerializeField] private LayerMask damageableMask;

    // 拳法权重。
    public float PunchWeight => punchWeight;
    // 双拳权重。
    public float DoublePunchWeight => doublePunchWeight;
    // 单拳倍率。
    public float PunchDamageMultiplier => punchDamageMultiplier;
    // 双拳倍率。
    public float DoublePunchDamageMultiplier => doublePunchDamageMultiplier;
    // 是否启用连段。
    public bool EnableComboChains => enableComboChains;
    // 连段触发概率。
    public float ComboChainChance => comboChainChance;
    // 连段停顿时长。
    public float ComboLinkDelay => comboLinkDelay;
    // 跺地权重。
    public float SlamWeight => slamWeight;
    // 跺地倍率。
    public float SlamDamageMultiplier => slamDamageMultiplier;
    // 跺地半径。
    public float SlamRadius => slamRadius;
    // 跺地命中偏移。
    public Vector3 SlamHitOffset => slamHitOffset;
    // 跺地破防体力。
    public float SlamGuardStamina => slamGuardStamina;
    // 震波权重。
    public float ShockwaveWeight => shockwaveWeight;
    // 震波倍率。
    public float ShockwaveDamageMultiplier => shockwaveDamageMultiplier;
    // 震波可选距离。
    public float ShockwaveRange => shockwaveRange;
    // 震波速度。
    public float ShockwaveSpeed => shockwaveSpeed;
    // 震波存活时间。
    public float ShockwaveLifetime => shockwaveLifetime;
    // 震波破防体力。
    public float ShockwaveGuardStamina => shockwaveGuardStamina;
    // 震波视觉预制体。
    public GameObject ShockwaveVisualPrefab => shockwaveVisualPrefab;
    // 震波发射偏移。
    public Vector3 ShockwaveSpawnOffset => shockwaveSpawnOffset;
    // 震波石头缩放。
    public float ShockwaveVisualScale => shockwaveVisualScale;
    // 震波石头自转速度。
    public float ShockwaveSpinSpeed => shockwaveSpinSpeed;
    // 碎石预制体列表。
    public GameObject[] DebrisPrefabs => debrisPrefabs;
    // 碎石基准数量。
    public int DebrisCount => Mathf.Max(1, Mathf.RoundToInt(debrisCount));
    // 砸地烟尘预制体。
    public GameObject ImpactDustPrefab => impactDustPrefab;
    // 地面冲击波预制体。
    public GameObject ImpactWavePrefab => impactWavePrefab;
    // 跳跃砸地概率。
    public float JumpSlamChance => jumpSlamChance;
    // 跳跃砸地最近距离。
    public float JumpSlamMinRange => jumpSlamMinRange;
    // 跳跃砸地最远距离。
    public float JumpSlamMaxRange => jumpSlamMaxRange;
    // 跳跃砸地倍率。
    public float JumpSlamDamageMultiplier => jumpSlamDamageMultiplier;
    // 跳跃砸地半径。
    public float JumpSlamRadius => jumpSlamRadius;
    // 举盾触发命中数。
    public int DefendTriggerHits => defendTriggerHits;
    // 举盾时长。
    public float DefendDuration => defendDuration;
    // 石肤减伤。
    public float DefendDamageReduction => defendDamageReduction;
    // 举盾冷却。
    public float DefendCooldown => defendCooldown;
    // 举盾最大距离。
    public float DefendMaxRange => defendMaxRange;
    // 二阶段生命阈值。
    public float PhaseTwoHealthThreshold => phaseTwoHealthThreshold;
    // 二阶段间隔缩放。
    public float PhaseTwoIntervalScale => phaseTwoIntervalScale;
    // 二阶段速度缩放。
    public float PhaseTwoSpeedScale => phaseTwoSpeedScale;
    // 二阶段是否解锁加长连击。
    public bool PhaseTwoExtendedCombo => phaseTwoExtendedCombo;
    // 近战攻击距离。
    public float AttackRange => attackRange;
    // 攻击后最短等待。
    public float AttackIntervalMin => attackIntervalMin;
    // 攻击后最长等待。
    public float AttackIntervalMax => attackIntervalMax;
    // 动作启动保底时长。
    public float ActionStartTimeout => actionStartTimeout;
    // 近战命中偏移。
    public Vector3 MeleeHitOffset => meleeHitOffset;
    // 近战命中半径。
    public float MeleeHitRadius => meleeHitRadius;
    // 可受击目标层。
    public LayerMask DamageableMask => damageableMask;

    // 在 Inspector 中限制所有数值为合法值。
    private void OnValidate()
    {
        punchWeight = Mathf.Max(0f, punchWeight);
        doublePunchWeight = Mathf.Max(0f, doublePunchWeight);
        punchDamageMultiplier = Mathf.Max(0f, punchDamageMultiplier);
        doublePunchDamageMultiplier = Mathf.Max(0f, doublePunchDamageMultiplier);
        slamWeight = Mathf.Max(0f, slamWeight);
        slamDamageMultiplier = Mathf.Max(0f, slamDamageMultiplier);
        slamRadius = Mathf.Max(0.1f, slamRadius);
        slamGuardStamina = Mathf.Max(0f, slamGuardStamina);
        shockwaveWeight = Mathf.Max(0f, shockwaveWeight);
        shockwaveDamageMultiplier = Mathf.Max(0f, shockwaveDamageMultiplier);
        shockwaveRange = Mathf.Max(0f, shockwaveRange);
        shockwaveSpeed = Mathf.Max(0.1f, shockwaveSpeed);
        shockwaveLifetime = Mathf.Max(0.1f, shockwaveLifetime);
        shockwaveGuardStamina = Mathf.Max(0f, shockwaveGuardStamina);
        jumpSlamMinRange = Mathf.Max(0f, jumpSlamMinRange);
        jumpSlamMaxRange = Mathf.Max(jumpSlamMinRange, jumpSlamMaxRange);
        jumpSlamDamageMultiplier = Mathf.Max(0f, jumpSlamDamageMultiplier);
        jumpSlamRadius = Mathf.Max(0.1f, jumpSlamRadius);
        defendTriggerHits = Mathf.Max(1, defendTriggerHits);
        defendMaxRange = Mathf.Max(0f, defendMaxRange);
        attackRange = Mathf.Max(0f, attackRange);
        attackIntervalMin = Mathf.Max(0f, attackIntervalMin);
        attackIntervalMax = Mathf.Max(attackIntervalMin, attackIntervalMax);
        actionStartTimeout = Mathf.Max(0.05f, actionStartTimeout);
        meleeHitRadius = Mathf.Max(0.01f, meleeHitRadius);
    }
}
