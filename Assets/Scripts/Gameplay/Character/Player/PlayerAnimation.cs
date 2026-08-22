using UnityEngine;

/// <summary>
/// Drives the locomotion parameters shared by the equipped and unequipped trees.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimation : MonoBehaviour
{
    // Animator 中横向移动参数的哈希值。
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    // Animator 中纵向移动参数的哈希值。
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    // Animator 中移动总幅度参数的哈希值。
    private static readonly int MoveAmountHash = Animator.StringToHash("MoveAmount");
    // Animator 中奔跑状态参数的哈希值。
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    // Animator 中持武器状态参数的哈希值。
    private static readonly int IsEquippedHash = Animator.StringToHash("IsEquipped");
    // Animator 中重攻击起手触发器的哈希值。
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
    // Animator 中重攻击连段触发器的哈希值。
    private static readonly int HeavyAttackComboHash = Animator.StringToHash("HeavyAttackCombo");

    // 角色模型上的 Animator 组件引用。
    [SerializeField] private Animator animator;
    // Animator 浮点参数的平滑时间，单位为秒。
    [SerializeField, Min(0f)] private float dampTime = 0.1f;

    // 当前是否处于持武器动画树。
    public bool IsEquipped { get; private set; }

    // 缓存子物体中的 Animator 引用。
    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // 写入移动、奔跑和装备参数以驱动移动动画树。
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

    // 设置持武器状态并同步至 Animator。
    public void SetEquipped(bool equipped)
    {
        IsEquipped = equipped;

        if (animator != null)
            animator.SetBool(IsEquippedHash, equipped);
    }

    // 翻转当前持武器状态。
    public void ToggleEquipped()
    {
        SetEquipped(!IsEquipped);
    }

    // 触发重攻击第一段动画。
    public void StartHeavyAttack()
    {
        animator?.SetTrigger(HeavyAttackHash);
    }

    // 触发重攻击下一段连招动画。
    public void ContinueHeavyAttack()
    {
        animator?.SetTrigger(HeavyAttackComboHash);
    }
}
