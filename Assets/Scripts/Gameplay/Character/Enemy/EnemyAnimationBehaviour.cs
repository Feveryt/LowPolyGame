using System;
using UnityEngine;

/// <summary>
/// 敌人动画适配器的通用抽象基类。
/// EnemyAI 只依赖移动、受击和死亡语义；每种敌人以自己的 Animator 参数实现这些语义。
/// </summary>
public abstract class EnemyAnimationBehaviour : MonoBehaviour
{
    /// <summary>按通用移动语义切换该敌人的循环移动动画。</summary>
    public abstract void SetMovement(EnemyMovementAnimation movement);

    /// <summary>停止该敌人的循环移动动画。</summary>
    public abstract void StopMovement();

    /// <summary>播放该敌人的非致命受击动画。</summary>
    public abstract void PlayHurt();

    /// <summary>播放该敌人的死亡动画。</summary>
    public abstract void PlayDie();

    /// <summary>当前 Animator 是否仍处于受击状态。</summary>
    public abstract bool IsPlayingHurt { get; }

    /// <summary>当前 Animator 是否仍处于死亡状态，用于判断死亡动画是否播放完毕。</summary>
    public abstract bool IsPlayingDie { get; }

    /// <summary>死亡动画播放到末帧时触发，由敌人决策层据此安排销毁。</summary>
    public event Action DeathAnimationFinished;

    /// <summary>供 Animator 死亡动画末帧事件调用，广播死亡动画已经播完。</summary>
    public void AnimationEvent_DeathFinished()
    {
        DeathAnimationFinished?.Invoke();
    }
}
