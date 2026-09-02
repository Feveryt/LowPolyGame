using UnityEngine;

/// <summary>
/// Handles the player's light and heavy three-hit weapon attack combos.
/// Animator transitions decide the exact cancel window; this component buffers
/// one input until that transition becomes eligible.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerCombat : MonoBehaviour
{
    // 第一段重攻击状态的 Animator 哈希值。
    private static readonly int Attack01Hash = Animator.StringToHash("Attack_01");
    // 第二段重攻击状态的 Animator 哈希值。
    private static readonly int Attack16Hash = Animator.StringToHash("Attack_16");
    // 第三段重攻击状态的 Animator 哈希值。
    private static readonly int Attack14Hash = Animator.StringToHash("Attack_14");
    // 第一段轻攻击状态的 Animator 哈希值。
    private static readonly int Attack02Hash = Animator.StringToHash("Attack_02");
    // 第二段轻攻击状态的 Animator 哈希值。
    private static readonly int Attack03Hash = Animator.StringToHash("Attack_03");
    // 第三段轻攻击状态的 Animator 哈希值。
    private static readonly int Attack06Hash = Animator.StringToHash("Attack_06");

    // 接收重攻击按键事件的输入组件。
    [SerializeField] private InputManager input;
    // 负责触发攻击动画的组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 用于查询当前攻击状态与切换进度的 Animator。
    [SerializeField] private Animator animator;
    // 提供玩家存活状态，防止死亡后继续发起或缓存攻击。
    [SerializeField] private PlayerStats playerStats;
    // 负责范围型攻击判定的通用检测组件，保留给拳头和范围技能。
    [SerializeField] private HitDetection hitDetection;
    // 负责当前实体武器 Trigger 命中窗口的组件。
    [SerializeField] private WeaponHitbox weaponHitbox;
    // 当前三段重攻击共用的静态伤害与范围配置。
    [SerializeField] private PlayerAttackDefinition heavyAttackDefinition;
    // 当前三段轻攻击共用的静态伤害与范围配置。
    [SerializeField] private PlayerAttackDefinition lightAttackDefinition;
    // 范围型攻击检测原点，未绑定时使用玩家自身 Transform。
    [SerializeField] private Transform attackOrigin;
    // 连段输入窗口起始归一化时间。
    [SerializeField, Range(0f, 1f)] private float comboInputStart = 0.45f;
    // 连段输入窗口结束归一化时间。
    [SerializeField, Range(0f, 1f)] private float comboInputEnd = 0.9f;
    // 等待 Animator 进入第一段攻击状态的最长时间，单位为秒。
    [SerializeField, Min(0.05f)] private float attackStartTimeout = 0.3f;

    // 当前攻击中是否已缓存下一段连段输入。
    private bool comboQueued;
    // 当前帧是否已请求开始攻击但 Animator 尚未切入状态。
    private bool attackRequested;
    // 攻击起手请求失效的绝对时间。
    private float attackRequestExpiresAt;

    // 当前是否正在执行重攻击或其切换过程。
    public bool IsAttacking { get; private set; }

    // 缓存输入、动画和表现组件引用。
    private void Awake()
    {
        input = input != null ? input : GetComponent<InputManager>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        hitDetection = hitDetection != null ? hitDetection : GetComponent<HitDetection>();
        weaponHitbox = weaponHitbox != null ? weaponHitbox : GetComponentInChildren<WeaponHitbox>(true);
        attackOrigin = attackOrigin != null ? attackOrigin : transform;
    }

    // 启用时订阅重攻击输入事件。
    private void OnEnable()
    {
        if (input != null)
            input.HeavyAttackPressed += OnHeavyAttackPressed;
        if (input != null)
            input.LightAttackPressed += OnLightAttackPressed;
    }

    // 禁用时取消订阅重攻击输入事件。
    private void OnDisable()
    {
        if (input != null)
            input.HeavyAttackPressed -= OnHeavyAttackPressed;
        if (input != null)
            input.LightAttackPressed -= OnLightAttackPressed;

        weaponHitbox?.EndAttack();
    }

    // 每帧同步攻击状态，并在有效窗口消费连段输入。
    private void Update()
    {
        if (playerStats != null && !playerStats.IsAlive)
        {
            comboQueued = false;
            attackRequested = false;
            IsAttacking = false;
            currentAttack = null;
            weaponHitbox?.EndAttack();
            return;
        }

        if (animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inHeavyAttack = IsHeavyAttackState(state);
        bool inLightAttack = IsLightAttackState(state);

        if (!inHeavyAttack && !inLightAttack)
        {
            // Animator triggers are consumed after Update. Keep the attack active
            // briefly while the Animator starts its transition into Attack_01.
            if (attackRequested && Time.time <= attackRequestExpiresAt)
            {
                IsAttacking = true;
                return;
            }

            attackRequested = false;
            IsAttacking = animator.IsInTransition(0) && IsAttacking;
            if (!animator.IsInTransition(0))
            {
                IsAttacking = false;
                currentAttack = null;
                weaponHitbox?.EndAttack();
            }
            return;
        }

        attackRequested = false;
        IsAttacking = true;
        float normalizedTime = state.normalizedTime % 1f;

        if (comboQueued && !animator.IsInTransition(0) &&
            normalizedTime >= comboInputStart && normalizedTime <= comboInputEnd)
        {
            if (inHeavyAttack && state.shortNameHash != Attack14Hash)
            {
                comboQueued = false;
                playerAnimation.ContinueHeavyAttack();
            }
            else if (inLightAttack && state.shortNameHash != Attack06Hash)
            {
                comboQueued = false;
                playerAnimation.ContinueLightAttack();
            }
        }
    }

    // 响应重攻击输入，开始攻击或缓存下一段连招。
    private void OnHeavyAttackPressed()
    {
        if (playerAnimation == null || (playerStats != null && !playerStats.IsAlive))
            return;

        if (!playerAnimation.IsEquipped)
        {
            Debug.Log("Heavy attack ignored: equip the weapon first (R / gamepad north button).", this);
            return;
        }

        if (!IsAttacking)
        {
            if (heavyAttackDefinition == null)
            {
                Debug.LogWarning("Heavy attack definition is missing.", this);
                return;
            }

            comboQueued = false;
            attackRequested = true;
            attackRequestExpiresAt = Time.time + attackStartTimeout;
            IsAttacking = true;
            currentAttack = heavyAttackDefinition;
            playerAnimation.StartHeavyAttack();
            return;
        }

        if (currentAttack != null && currentAttack.AttackType == AttackType.HeavyAttack)
            comboQueued = true;
    }

    // 响应轻攻击输入，启动轻攻击或缓存轻攻击的下一段连招。
    private void OnLightAttackPressed()
    {
        if (playerAnimation == null || (playerStats != null && !playerStats.IsAlive))
            return;

        if (!playerAnimation.IsEquipped)
        {
            Debug.Log("Light attack ignored: equip the weapon first (R / gamepad north button).", this);
            return;
        }

        if (!IsAttacking)
        {
            if (lightAttackDefinition == null)
            {
                Debug.LogWarning("Light attack definition is missing.", this);
                return;
            }

            comboQueued = false;
            attackRequested = true;
            attackRequestExpiresAt = Time.time + attackStartTimeout;
            IsAttacking = true;
            currentAttack = lightAttackDefinition;
            playerAnimation.StartLightAttack();
            return;
        }

        if (currentAttack != null && currentAttack.AttackType == AttackType.LightAttack)
            comboQueued = true;
    }

    // 当前攻击段使用的配置。
    private PlayerAttackDefinition currentAttack;

    /// <summary>由动画事件开启当前攻击段的实体武器命中窗口。</summary>
    public void AnimationEvent_AttackHit()
    {
        if (!IsAttacking || currentAttack == null || playerStats == null)
            return;

        hitDetection?.BeginAttack();
        weaponHitbox?.BeginAttack(playerStats, currentAttack);
    }

    /// <summary>由动画事件关闭当前攻击段的实体武器命中窗口。</summary>
    public void AnimationEvent_AttackHitEnd()
    {
        weaponHitbox?.EndAttack();
    }

    /// <summary>由攻击动画结束事件调用，结束当前攻击流程。</summary>
    public void AnimationEvent_AttackFinished()
    {
        if (animator != null && IsAttackState(animator.GetCurrentAnimatorStateInfo(0)))
            return;

        comboQueued = false;
        attackRequested = false;
        IsAttacking = false;
        currentAttack = null;
        weaponHitbox?.EndAttack();
    }

    // 判断 Animator 当前状态是否属于本组件管理的重攻击连段。
    private static bool IsHeavyAttackState(AnimatorStateInfo state)
    {
        return state.shortNameHash == Attack01Hash ||
            state.shortNameHash == Attack16Hash ||
            state.shortNameHash == Attack14Hash;
    }

    // 判断 Animator 当前状态是否属于本组件管理的轻攻击连段。
    private static bool IsLightAttackState(AnimatorStateInfo state)
    {
        return state.shortNameHash == Attack02Hash ||
            state.shortNameHash == Attack03Hash ||
            state.shortNameHash == Attack06Hash;
    }

    // 判断 Animator 当前状态是否属于任意由本组件驱动的攻击连段。
    private static bool IsAttackState(AnimatorStateInfo state)
    {
        return IsHeavyAttackState(state) || IsLightAttackState(state);
    }
}
