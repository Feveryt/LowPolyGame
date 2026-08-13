using UnityEngine;

/// <summary>
/// Drives the locomotion parameters shared by the equipped and unequipped trees.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimation : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveAmountHash = Animator.StringToHash("MoveAmount");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsEquippedHash = Animator.StringToHash("IsEquipped");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
    private static readonly int HeavyAttackComboHash = Animator.StringToHash("HeavyAttackCombo");

    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float dampTime = 0.1f;

    public bool IsEquipped { get; private set; }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void SetLocomotion(Vector2 localMove, bool isRunning)
    {
        if (animator == null)
            return;

        Vector2 animationMove = isRunning ? Vector2.up : localMove;
        animator.SetFloat(MoveXHash, animationMove.x, dampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, animationMove.y, dampTime, Time.deltaTime);
        animator.SetFloat(MoveAmountHash, animationMove.magnitude, dampTime, Time.deltaTime);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsEquippedHash, IsEquipped);
    }

    public void SetEquipped(bool equipped)
    {
        IsEquipped = equipped;

        if (animator != null)
            animator.SetBool(IsEquippedHash, equipped);
    }

    public void ToggleEquipped()
    {
        SetEquipped(!IsEquipped);
    }

    public void StartHeavyAttack()
    {
        animator?.SetTrigger(HeavyAttackHash);
    }

    public void ContinueHeavyAttack()
    {
        animator?.SetTrigger(HeavyAttackComboHash);
    }
}
