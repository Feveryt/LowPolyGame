using UnityEngine;

/// <summary>
/// 石头人石肤格挡：举盾期间正面伤害被完全吸收，玩家重攻击可击碎石肤并造成硬直。
/// 作为一种"招式"由 StoneGolemAttack 选择触发；通过 IIncomingDamageHandler 接入伤害管线。
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneGolemDefend : MonoBehaviour, IIncomingDamageHandler
{
    // 石头人攻击配置资产（复用石肤参数）。
    [SerializeField] private StoneGolemAttackDefinition definition;
    // 石头人 Animator 适配组件。
    [SerializeField] private StoneGolemAnimation animationBehaviour;
    // 石头人数值组件，用于挂载拦截器。
    [SerializeField] private EnemyStats stats;
    // 同对象的通用 AI，用于破防后的硬直。
    [SerializeField] private EnemyAI enemyAi;
    // 可选的预警组件，吸收攻击时闪黄光。
    [SerializeField] private AttackTelegraph telegraph;

    // 石肤生效截止时间。
    private float defendEndsAt = float.NegativeInfinity;
    // 上次石肤结束时间，用于冷却。
    private float lastDefendTime = float.NegativeInfinity;
    // 石肤当前是否生效。
    private bool active;

    /// <summary>石肤当前是否生效。</summary>
    public bool IsActive => active && Time.time < defendEndsAt;

    // 缓存协作组件引用。
    private void Awake()
    {
        ResolveDefinition();
        animationBehaviour = animationBehaviour != null ? animationBehaviour : GetComponentInChildren<StoneGolemAnimation>();
        stats = stats != null ? stats : GetComponent<EnemyStats>();
        enemyAi = enemyAi != null ? enemyAi : GetComponent<EnemyAI>();
        telegraph = telegraph != null ? telegraph : GetComponent<AttackTelegraph>();
    }

    // 从本宿主的攻击组件解析配置资产（两者共用同一 StoneGolemAttackDefinition）。
    private void ResolveDefinition()
    {
        if (definition != null)
            return;

        StoneGolemAttack attack = GetComponent<StoneGolemAttack>();
        definition = attack != null ? attack.DefinitionAsset : null;
    }

    // 禁用时强制结束石肤并解除拦截器。
    private void OnDisable()
    {
        EndDefend();
    }

    /// <summary>石肤是否已准备好（冷却结束）。</summary>
    public bool IsReady => !IsActive && Time.time >= lastDefendTime + (definition != null ? definition.DefendCooldown : 0f);

    /// <summary>尝试进入石肤格挡；成功返回 true。</summary>
    public bool TryStartDefend()
    {
        if (definition == null)
            ResolveDefinition();

        if (definition == null || !IsReady || IsActive)
            return false;

        active = true;
        defendEndsAt = Time.time + definition.DefendDuration;

        if (stats != null)
            stats.IncomingDamageHandler = this;
        if (animationBehaviour != null)
            animationBehaviour.SetDefending(true);

        return true;
    }

    // 结束石肤。
    private void EndDefend()
    {
        if (!active)
            return;

        active = false;
        lastDefendTime = Time.time;
        defendEndsAt = float.NegativeInfinity;

        if (stats != null && ReferenceEquals(stats.IncomingDamageHandler, this))
            stats.IncomingDamageHandler = null;
        if (animationBehaviour != null)
            animationBehaviour.SetDefending(false);
    }

    // 处理石肤期间的入站伤害。
    public DamageResult? HandleDamage(DamageRequest request, CharacterStats target)
    {
        if (!IsActive)
            return null;

        // 来自身后的攻击绕过石肤。
        if (!IsFrontalAttack(request))
        {
            EndDefend();
            return null;
        }

        // 重攻击击碎石肤：解除石肤并放行完整伤害，同时进入硬直。
        if (request.AttackType == AttackType.HeavyAttack)
        {
            EndDefend();
            enemyAi?.Stagger(1.5f);
            return null;
        }

        // 轻攻击与技能被完全吸收。
        float rawDamage = Mathf.Max(0f, request.AttackPower) * Mathf.Max(0f, request.Multiplier);
        telegraph?.Flash(Color.yellow, 0.25f);
        return DamageResult.Blocked(rawDamage, 0);
    }

    // 判断攻击是否来自正面；未知来源按正面处理。
    private bool IsFrontalAttack(DamageRequest request)
    {
        Vector3 sourcePosition;
        switch (request.Source)
        {
            case Component component:
                sourcePosition = component.transform.position;
                break;
            case GameObject gameObject:
                sourcePosition = gameObject.transform.position;
                break;
            default:
                return true;
        }

        Vector3 toSource = sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(transform.forward, toSource) <= 90f;
    }
}
