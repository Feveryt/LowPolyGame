using UnityEngine;

/// <summary>
/// 敌人专属攻击行为的抽象入口。
/// EnemyAI 只负责攻击状态、面向目标与等待节奏，不依赖具体动画、判定形状或伤害规则。
/// </summary>
[DisallowMultipleComponent]
public abstract class EnemyAttackBehaviour : MonoBehaviour
{
    // 当前近战前摇标记；跟随攻击者旋转，在取消攻击时清理。
    private GroundTelegraphFX meleeCue;

    /// <summary>允许 EnemyAI 进入攻击状态的最大水平距离。</summary>
    public abstract float AttackRange { get; }

    /// <summary>返回本轮攻击结束后到下一次攻击前的等待时间，单位为秒。</summary>
    public abstract float GetNextAttackDelay();

    /// <summary>开始本轮专属攻击动作与命中判定。</summary>
    public abstract void BeginAttack();

    /// <summary>当前攻击动作是否已经结束。</summary>
    public abstract bool IsAttackFinished { get; }

    /// <summary>死亡或显式硬直时取消尚未结算的招式与前摇表现。</summary>
    public virtual void CancelAttack()
    {
        if (meleeCue != null)
            Destroy(meleeCue.gameObject);
    }

    // 根据现有动画的命中进度计算前摇秒数，避免预警提前消失。
    protected float GetWindupDuration(Animator animator, string clipName, float hitProgress)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                    return clip.length * hitProgress / Mathf.Max(0.01f, animator.speed);
            }
        }

        return 0.4f;
    }

    // 普通近战只用简洁暖色范围圈，位置随攻击者移动和转向，不叠加重击的波纹。
    protected void ShowMeleeCue(Vector3 center, float radius, float duration)
    {
        if (meleeCue != null)
            Destroy(meleeCue.gameObject);
        meleeCue = GroundTelegraphFX.ShowDisc(GetAttackGroundPoint(center), radius,
            new Color(1f, 0.72f, 0.3f), duration, false);
        if (meleeCue != null)
            meleeCue.transform.SetParent(transform, true);
    }

    // 预警探地跳过角色碰撞体，避免把圈画到怪物背部或玩家头顶。
    protected Vector3 GetAttackGroundPoint(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 8f,
            ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 ground = new Vector3(position.x, transform.position.y, position.z);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<CharacterStats>() != null || hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            ground = hit.point;
        }
        return ground;
    }
}
