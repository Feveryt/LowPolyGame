using System;
using UnityEngine;

/// <summary>
/// 龙 BOSS 的 Animator 参数适配器。
/// 将通用移动、受击和死亡语义映射到为 BOSS 定制的控制器；
/// 攻击状态由 DragonBossAttack 直接以状态名轮询。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DragonBossAnimation : EnemyAnimationBehaviour
{
    // 移动布尔参数哈希值。
    private static readonly int WalkForwardHash = Animator.StringToHash("WalkForward");
    private static readonly int StrafeLeftHash = Animator.StringToHash("StrafeLeft");
    private static readonly int StrafeRightHash = Animator.StringToHash("StrafeRight");
    // 移动速度倍率参数哈希值（走路动画加速播放以近似小跑）。
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    // 攻击与状态触发器参数哈希值。
    private static readonly int BiteTriggerHash = Animator.StringToHash("Bite");
    private static readonly int FireballTriggerHash = Animator.StringToHash("Fireball");
    private static readonly int SpellAoETriggerHash = Animator.StringToHash("SpellAoE");
    private static readonly int FireBreathTriggerHash = Animator.StringToHash("FireBreath");
    private static readonly int TakeDamageTriggerHash = Animator.StringToHash("TakeDamage");
    private static readonly int DieTriggerHash = Animator.StringToHash("Die");
    // 受击与死亡状态短名称哈希值。
    private static readonly int HurtStateHash = Animator.StringToHash("Hurt");
    private static readonly int DieStateHash = Animator.StringToHash("Die");

    // BOSS Animator 引用。
    [SerializeField] private Animator animator;

    /// <summary>攻击伤害时窗到达时广播（由攻击组件判定具体结算）。</summary>
    public event Action AttackHitFrame;

    /// <summary>当前 Animator 是否处于受击状态。</summary>
    public override bool IsPlayingHurt => IsPlayingState(HurtStateHash);
    /// <summary>当前 Animator 是否处于死亡状态。</summary>
    public override bool IsPlayingDie => IsPlayingState(DieStateHash);

    // 缓存 Animator 引用。
    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
    }

    /// <summary>按语义播放循环移动动画。</summary>
    public override void SetMovement(EnemyMovementAnimation movement)
    {
        if (animator == null)
            return;

        ResetMovement();

        // 素材包没有龙的跑步动画：奔跑语义映射为走路动画 + 1.6 倍速播放。
        animator.SetFloat(MoveSpeedHash, movement == EnemyMovementAnimation.RunForward ? 1.6f : 1f);

        switch (movement)
        {
            case EnemyMovementAnimation.WalkForward:
            case EnemyMovementAnimation.RunForward:
                animator.SetBool(WalkForwardHash, true);
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
        ResetMovement();
    }

    /// <summary>触发受击动画。</summary>
    public override void PlayHurt()
    {
        if (animator == null || IsPlayingHurt)
            return;

        ResetMovement();
        animator.SetTrigger(TakeDamageTriggerHash);
    }

    /// <summary>触发死亡动画。</summary>
    public override void PlayDie()
    {
        if (animator == null)
            return;

        ResetMovement();
        animator.SetTrigger(DieTriggerHash);
    }

    /// <summary>触发撕咬动画。</summary>
    public void PlayBite() => animator?.SetTrigger(BiteTriggerHash);

    /// <summary>触发火球动画。</summary>
    public void PlayFireball() => animator?.SetTrigger(FireballTriggerHash);

    /// <summary>触发地火 AoE 动画。</summary>
    public void PlaySpellAoE() => animator?.SetTrigger(SpellAoETriggerHash);

    /// <summary>触发龙息动画。</summary>
    public void PlayFireBreath() => animator?.SetTrigger(FireBreathTriggerHash);

    /// <summary>供需要外部广播命中帧时使用。</summary>
    public void BroadcastHitFrame() => AttackHitFrame?.Invoke();

    // 重置所有移动布尔。
    private void ResetMovement()
    {
        if (animator == null)
            return;

        animator.SetBool(WalkForwardHash, false);
        animator.SetBool(StrafeLeftHash, false);
        animator.SetBool(StrafeRightHash, false);
    }

    // 检查当前状态或过渡目标是否为指定短名称。
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
