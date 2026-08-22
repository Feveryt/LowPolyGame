using UnityEngine;

/// <summary>
/// Handles the three-hit heavy attack combo: Attack_17, Attack_18, Attack_14.
/// Animator transitions decide the exact cancel window; this component buffers
/// one input until that transition becomes eligible.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerAnimation))]
public sealed class PlayerCombat : MonoBehaviour
{
    // 第一段重攻击状态的 Animator 哈希值。
    private static readonly int Attack17Hash = Animator.StringToHash("Attack_17");
    // 第二段重攻击状态的 Animator 哈希值。
    private static readonly int Attack18Hash = Animator.StringToHash("Attack_18");
    // 第三段重攻击状态的 Animator 哈希值。
    private static readonly int Attack14Hash = Animator.StringToHash("Attack_14");

    // 接收重攻击按键事件的输入组件。
    [SerializeField] private InputManager input;
    // 负责触发攻击动画的组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 用于查询当前攻击状态与切换进度的 Animator。
    [SerializeField] private Animator animator;
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
    }

    // 启用时订阅重攻击输入事件。
    private void OnEnable()
    {
        if (input != null)
            input.HeavyAttackPressed += OnHeavyAttackPressed;
    }

    // 禁用时取消订阅重攻击输入事件。
    private void OnDisable()
    {
        if (input != null)
            input.HeavyAttackPressed -= OnHeavyAttackPressed;
    }

    // 每帧同步攻击状态，并在有效窗口消费连段输入。
    private void Update()
    {
        if (animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inHeavyAttack = IsHeavyAttackState(state);

        if (!inHeavyAttack)
        {
            // Animator triggers are consumed after Update. Keep the attack active
            // briefly while the Animator starts its transition into Attack_17.
            if (attackRequested && Time.time <= attackRequestExpiresAt)
            {
                IsAttacking = true;
                return;
            }

            attackRequested = false;
            IsAttacking = animator.IsInTransition(0) && IsAttacking;
            if (!animator.IsInTransition(0))
                IsAttacking = false;
            return;
        }

        attackRequested = false;
        IsAttacking = true;
        float normalizedTime = state.normalizedTime % 1f;

        if (comboQueued && !animator.IsInTransition(0) &&
            normalizedTime >= comboInputStart && normalizedTime <= comboInputEnd &&
            !state.shortNameHash.Equals(Attack14Hash))
        {
            comboQueued = false;
            playerAnimation.ContinueHeavyAttack();
        }
    }

    // 响应重攻击输入，开始攻击或缓存下一段连招。
    private void OnHeavyAttackPressed()
    {
        if (playerAnimation == null)
            return;

        if (!playerAnimation.IsEquipped)
        {
            Debug.Log("Heavy attack ignored: equip the weapon first (R / gamepad north button).", this);
            return;
        }

        if (!IsAttacking)
        {
            comboQueued = false;
            attackRequested = true;
            attackRequestExpiresAt = Time.time + attackStartTimeout;
            IsAttacking = true;
            playerAnimation.StartHeavyAttack();
            return;
        }

        comboQueued = true;
    }

    // 判断 Animator 当前状态是否属于本组件管理的重攻击连段。
    private static bool IsHeavyAttackState(AnimatorStateInfo state)
    {
        return state.shortNameHash == Attack17Hash ||
            state.shortNameHash == Attack18Hash ||
            state.shortNameHash == Attack14Hash;
    }
}
