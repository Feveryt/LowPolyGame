using UnityEngine;

/// <summary>
/// 玩家 Animator 的参数适配组件。
/// 负责装备/未装备移动树、重攻击、受击和死亡动画参数，不处理伤害数值判断。
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
    // Animator 中轻攻击起手触发器的哈希值。
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    // Animator 中轻攻击连段触发器的哈希值。
    private static readonly int LightAttackComboHash = Animator.StringToHash("LightAttackCombo");
    // Animator 中非致命受击触发器的哈希值。
    private static readonly int DamagedHash = Animator.StringToHash("Damaged");
    // Animator 中死亡触发器的哈希值。
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    // 受击状态的短名称与完整路径哈希，用于强制从头重播。
    private static readonly int HurtStateHash = Animator.StringToHash("Damage_01");
    private static readonly int HurtStatePathHash = Animator.StringToHash("Base Layer.Damage_01");
    // 两个移动状态的完整路径哈希，用于霸体触发时退出受击状态。
    private static readonly int EquippedLocomotionStateHash = Animator.StringToHash("Base Layer.Equipped Locomotion");
    private static readonly int UnequippedLocomotionStateHash = Animator.StringToHash("Base Layer.Unequipped Locomotion");
    // 负责接收攻击动画事件并执行命中检测的玩家战斗组件。
    [SerializeField] private PlayerCombat playerCombat;

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
        playerCombat = playerCombat != null ? playerCombat : GetComponentInParent<PlayerCombat>();
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

    /// <summary>触发轻攻击第一段动画。</summary>
    public void StartLightAttack()
    {
        animator?.SetTrigger(LightAttackHash);
    }

    /// <summary>触发轻攻击下一段连招动画。</summary>
    public void ContinueLightAttack()
    {
        animator?.SetTrigger(LightAttackComboHash);
    }

    /// <summary>将 Animator 的攻击命中事件转发给 PlayerCombat。</summary>
    public void AnimationEvent_AttackHit()
    {
        playerCombat?.AnimationEvent_AttackHit();
    }

    /// <summary>将 Animator 的攻击命中结束事件转发给 PlayerCombat。</summary>
    public void AnimationEvent_AttackHitEnd()
    {
        playerCombat?.AnimationEvent_AttackHitEnd();
    }

    /// <summary>将 Animator 的攻击结束事件转发给 PlayerCombat。</summary>
    public void AnimationEvent_AttackFinished()
    {
        playerCombat?.AnimationEvent_AttackFinished();
    }

    /// <summary>触发玩家的非致命受击动画。</summary>
    public void PlayHurt()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(DamagedHash);
        animator.CrossFadeInFixedTime(HurtStatePathHash, 0.05f, 0, 0f);
    }

    /// <summary>判断当前或即将进入的动画是否为受击状态。</summary>
    public bool IsPlayingHurt()
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == HurtStateHash || current.fullPathHash == HurtStatePathHash)
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return next.shortNameHash == HurtStateHash || next.fullPathHash == HurtStatePathHash;
    }

    /// <summary>取消受击动画并回到当前装备状态对应的移动树。</summary>
    public void CancelHurt()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(DamagedHash);
        animator.SetFloat(MoveXHash, 0f);
        animator.SetFloat(MoveYHash, 0f);
        animator.SetFloat(MoveAmountHash, 0f);
        int locomotionState = IsEquipped ? EquippedLocomotionStateHash : UnequippedLocomotionStateHash;
        animator.CrossFadeInFixedTime(locomotionState, 0.05f, 0, 0f);
    }

    /// <summary>停止移动与待处理动作参数，并触发玩家死亡动画。</summary>
    public void PlayDie()
    {
        if (animator == null)
            return;

        animator.SetFloat(MoveXHash, 0f);
        animator.SetFloat(MoveYHash, 0f);
        animator.SetFloat(MoveAmountHash, 0f);
        animator.SetBool(IsRunningHash, false);
        animator.ResetTrigger(DamagedHash);
        animator.ResetTrigger(HeavyAttackHash);
        animator.ResetTrigger(HeavyAttackComboHash);
        animator.ResetTrigger(LightAttackHash);
        animator.ResetTrigger(LightAttackComboHash);
        animator.SetTrigger(DeadHash);
    }
}
