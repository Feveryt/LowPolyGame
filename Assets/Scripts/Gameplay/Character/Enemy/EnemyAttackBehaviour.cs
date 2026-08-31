using UnityEngine;

/// <summary>
/// 敌人专属攻击行为的抽象入口。
/// EnemyAI 只负责攻击状态、面向目标与等待节奏，不依赖具体动画、判定形状或伤害规则。
/// </summary>
[DisallowMultipleComponent]
public abstract class EnemyAttackBehaviour : MonoBehaviour
{
    /// <summary>允许 EnemyAI 进入攻击状态的最大水平距离。</summary>
    public abstract float AttackRange { get; }

    /// <summary>返回本轮攻击结束后到下一次攻击前的等待时间，单位为秒。</summary>
    public abstract float GetNextAttackDelay();

    /// <summary>开始本轮专属攻击动作与命中判定。</summary>
    public abstract void BeginAttack();

    /// <summary>当前攻击动作是否已经结束。</summary>
    public abstract bool IsAttackFinished { get; }
}
