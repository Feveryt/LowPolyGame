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
    private static readonly int Attack17Hash = Animator.StringToHash("Attack_17");
    private static readonly int Attack18Hash = Animator.StringToHash("Attack_18");
    private static readonly int Attack14Hash = Animator.StringToHash("Attack_14");

    [SerializeField] private InputManager input;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private Animator animator;
    [SerializeField, Range(0f, 1f)] private float comboInputStart = 0.45f;
    [SerializeField, Range(0f, 1f)] private float comboInputEnd = 0.9f;
    [SerializeField, Min(0.05f)] private float attackStartTimeout = 0.3f;

    private bool comboQueued;
    private bool attackRequested;
    private float attackRequestExpiresAt;

    public bool IsAttacking { get; private set; }

    private void Awake()
    {
        input = input != null ? input : GetComponent<InputManager>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (input != null)
            input.HeavyAttackPressed += OnHeavyAttackPressed;
    }

    private void OnDisable()
    {
        if (input != null)
            input.HeavyAttackPressed -= OnHeavyAttackPressed;
    }

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

    private static bool IsHeavyAttackState(AnimatorStateInfo state)
    {
        return state.shortNameHash == Attack17Hash ||
            state.shortNameHash == Attack18Hash ||
            state.shortNameHash == Attack14Hash;
    }
}
