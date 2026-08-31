using UnityEngine;

/// <summary>
/// 石头人的专属近战攻击组件。
/// 负责随机拳法、Animator 触发、关键帧命中判定和攻击结束检测。
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneGolemAttack : EnemyAttackBehaviour
{
    // 石头人专属攻击数值与判定配置资产。
    [SerializeField] private StoneGolemAttackDefinition definition;
    // 广播攻击命中关键帧的通用动画适配组件。
    [SerializeField] private StoneGolemAnimation enemyAnimation;
    // 石头人 Animator，用于触发拳法并检查专属攻击状态。
    [SerializeField] private Animator animator;

    // 石头人单拳和双拳 Trigger 参数哈希值。
    private static readonly int PunchTriggerHash = Animator.StringToHash("Punch");
    private static readonly int DoublePunchTriggerHash = Animator.StringToHash("Double Punch");
    // 石头人单拳和双拳状态短名称哈希值。
    private static readonly int PunchStateHash = Animator.StringToHash("Punch");
    private static readonly int DoublePunchStateHash = Animator.StringToHash("DoublePunch");

    // 复用命中检测缓冲区，避免攻击关键帧产生 GC 分配。
    private readonly Collider[] hitBuffer = new Collider[16];
    // 当前攻击所属的石头人宿主。
    private StoneGolem enemy;
    // 本轮攻击是否选择双拳动作。
    private bool isDoublePunch;
    // 本轮攻击是否已经完成过命中结算。
    private bool hitConsumed;
    // Animator 尚未进入攻击状态时的保底截止时间。
    private float actionStartDeadline;
    // 当前攻击是否已经观察到 Animator 进入攻击状态。
    private bool attackStateObserved;
    // 当前是否有一轮已开始但尚未结束的攻击。
    private bool attackActive;

    /// <summary>石头人允许发起近战的最大水平距离。</summary>
    public override float AttackRange => definition != null ? definition.AttackRange : 0f;

    /// <summary>返回石头人本轮攻击结束后的随机连击等待时间。</summary>
    public override float GetNextAttackDelay()
    {
        return definition != null
            ? Random.Range(definition.AttackIntervalMin, definition.AttackIntervalMax)
            : 0f;
    }

    /// <summary>当前石头人攻击动作是否结束或配置缺失。</summary>
    public override bool IsAttackFinished
    {
        get
        {
            if (!attackActive || animator == null)
                return true;

            bool isPlayingAttack = IsPlayingAttackState();
            if (isPlayingAttack)
            {
                attackStateObserved = true;
                return false;
            }

            if (!attackStateObserved && Time.time < actionStartDeadline)
                return false;

            attackActive = false;
            return true;
        }
    }

    // 缓存石头人、动画组件和 Animator 引用。
    private void Awake()
    {
        enemy = GetComponent<StoneGolem>();
        enemyAnimation = enemyAnimation != null ? enemyAnimation : GetComponentInChildren<StoneGolemAnimation>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
    }

    // 订阅通用动画组件广播的攻击关键帧。
    private void OnEnable()
    {
        if (enemyAnimation != null)
            enemyAnimation.AttackHitFrame += OnAttackHitFrame;
    }

    // 取消关键帧订阅，避免禁用或销毁后仍响应动画事件。
    private void OnDisable()
    {
        if (enemyAnimation != null)
            enemyAnimation.AttackHitFrame -= OnAttackHitFrame;
    }

    /// <summary>随机选择拳法并触发本轮石头人攻击。</summary>
    public override void BeginAttack()
    {
        if (definition == null || animator == null)
        {
            attackActive = false;
            return;
        }

        enemyAnimation?.StopMovement();
        isDoublePunch = SelectDoublePunch();
        hitConsumed = false;
        attackStateObserved = false;
        attackActive = true;
        actionStartDeadline = Time.time + definition.ActionStartTimeout;
        animator.SetTrigger(isDoublePunch ? DoublePunchTriggerHash : PunchTriggerHash);
    }

    // 按配置权重随机决定单拳或双拳；两个权重都为零时退回单拳。
    private bool SelectDoublePunch()
    {
        if (definition == null)
            return false;

        float totalWeight = definition.PunchWeight + definition.DoublePunchWeight;
        return totalWeight > Mathf.Epsilon && Random.value * totalWeight >= definition.PunchWeight;
    }

    // 响应攻击动画事件，并保证每轮攻击只对第一个有效目标结算一次伤害。
    private void OnAttackHitFrame()
    {
        if (!attackActive || hitConsumed || definition == null || enemy == null || enemy.Stats == null)
            return;

        hitConsumed = true;
        Transform origin = enemy.AttackOrigin;
        Vector3 center = origin.TransformPoint(definition.MeleeHitOffset);
        int count = Physics.OverlapSphereNonAlloc(
            center,
            definition.MeleeHitRadius,
            hitBuffer,
            definition.DamageableMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = hitBuffer[index];
            hitBuffer[index] = null;
            if (hit == null || hit.transform.root == enemy.transform.root)
                continue;

            CharacterStats damageable = hit.GetComponentInParent<CharacterStats>();
            if (damageable == null || !damageable.IsAlive)
                continue;

            float multiplier = isDoublePunch
                ? definition.DoublePunchDamageMultiplier
                : definition.PunchDamageMultiplier;
            damageable.TakeDamage(new DamageRequest(enemy.Stats.Attack, multiplier));
            return;
        }
    }

    // 判断当前 Animator 或正在切入的状态是否为石头人专属拳法。
    private bool IsPlayingAttackState()
    {
        return IsPlayingState(PunchStateHash) || IsPlayingState(DoublePunchStateHash);
    }

    // 判断当前 Animator 状态或过渡目标是否为指定短名称。
    private bool IsPlayingState(int stateHash)
    {
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash)
            return true;

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
    }

    // 在 Scene 视图中预览石头人攻击关键帧的命中球体。
    private void OnDrawGizmosSelected()
    {
        if (definition == null)
            return;

        Transform origin = enemy != null ? enemy.AttackOrigin : transform;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin.TransformPoint(definition.MeleeHitOffset), definition.MeleeHitRadius);
    }
}
