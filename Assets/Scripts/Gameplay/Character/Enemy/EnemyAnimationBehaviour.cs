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
}
