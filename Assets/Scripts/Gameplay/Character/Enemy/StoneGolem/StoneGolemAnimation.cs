using System;
using UnityEngine;

/// <summary>
/// 石头人 Animator 的专属参数适配器。
/// 将石头人的移动、受击和死亡语义映射到其 Animator Controller；专属攻击动画由 StoneGolemAttack 负责。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class StoneGolemAnimation : EnemyAnimationBehaviour
{
    // 石头人移动动画使用的布尔参数哈希值。
    private static readonly int WalkForwardHash = Animator.StringToHash("Walk Forward");
    private static readonly int WalkBackwardHash = Animator.StringToHash("Walk Backward");
    private static readonly int RunForwardHash = Animator.StringToHash("Run Forward");
    private static readonly int RunBackwardHash = Animator.StringToHash("Run Backward");
    private static readonly int StrafeLeftHash = Animator.StringToHash("Strafe Left");
    private static readonly int StrafeRightHash = Animator.StringToHash("Strafe Right");
    // 敌人通用受击和死亡动作使用的触发器参数哈希值。
    private static readonly int TakeDamageHash = Animator.StringToHash("Take Damage");
    private static readonly int DieHash = Animator.StringToHash("Die");
    // 用于检查 Animator 当前受击状态的短名称哈希值。
    private static readonly int TakeDamageStateHash = Animator.StringToHash("TakeDamage");

    // 石头人 Animator 引用。
    [SerializeField] private Animator animator;

    /// <summary>攻击动画关键帧到达时广播，由专属攻击组件进行伤害检测。</summary>
    public event Action AttackHitFrame;

    /// <summary>当前 Animator 是否正处于受击状态。</summary>
    public override bool IsPlayingHurt => IsPlayingState(TakeDamageStateHash);

    // 缓存 Animator 组件。
    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
    }

    /// <summary>按语义播放敌人的循环移动动画。</summary>
    public override void SetMovement(EnemyMovementAnimation movement)
    {
        if (animator == null)
            return;

        ResetMovement();
        switch (movement)
        {
            case EnemyMovementAnimation.WalkForward:
                animator.SetBool(WalkForwardHash, true);
                break;
            case EnemyMovementAnimation.WalkBackward:
                animator.SetBool(WalkBackwardHash, true);
                break;
            case EnemyMovementAnimation.RunForward:
                animator.SetBool(RunForwardHash, true);
                break;
            case EnemyMovementAnimation.RunBackward:
                animator.SetBool(RunBackwardHash, true);
                break;
            case EnemyMovementAnimation.StrafeLeft:
                animator.SetBool(StrafeLeftHash, true);
                break;
            case EnemyMovementAnimation.StrafeRight:
                animator.SetBool(StrafeRightHash, true);
                break;
        }
    }

    /// <summary>停止所有循环移动参数并回到 Idle。</summary>
    public override void StopMovement()
    {
        if (animator != null)
            ResetMovement();
    }

    /// <summary>触发非致命受击动画。</summary>
    public override void PlayHurt()
    {
        if (animator == null)
            return;

        ResetMovement();
        animator.SetTrigger(TakeDamageHash);
    }

    /// <summary>触发死亡动画并停止移动参数。</summary>
    public override void PlayDie()
    {
        if (animator == null)
            return;

        ResetMovement();
        animator.SetTrigger(DieHash);
    }

    /// <summary>供 Punch 和 DoublePunch 动画命中帧调用，触发本次近战判定。</summary>
    public void AnimationEvent_DealDamage()
    {
        AttackHitFrame?.Invoke();
    }

    // 将所有移动 Bool 重置为 false，保证 Animator 同时只播放一种移动状态。
    private void ResetMovement()
    {
        animator.SetBool(WalkForwardHash, false);
        animator.SetBool(WalkBackwardHash, false);
        animator.SetBool(RunForwardHash, false);
        animator.SetBool(RunBackwardHash, false);
        animator.SetBool(StrafeLeftHash, false);
        animator.SetBool(StrafeRightHash, false);
    }

    // 检查当前状态或正在进入的状态是否为指定动作状态。
    private bool IsPlayingState(int stateHash)
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash)
            return true;

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
    }
}

/// <summary>敌人 Animator 可使用的循环移动动画语义。</summary>
public enum EnemyMovementAnimation
{
    /// <summary>静止待机。</summary>
    Idle,
    /// <summary>向前步行。</summary>
    WalkForward,
    /// <summary>向后步行。</summary>
    WalkBackward,
    /// <summary>向前奔跑。</summary>
    RunForward,
    /// <summary>向后奔跑。</summary>
    RunBackward,
    /// <summary>向左横移。</summary>
    StrafeLeft,
    /// <summary>向右横移。</summary>
    StrafeRight,
}
