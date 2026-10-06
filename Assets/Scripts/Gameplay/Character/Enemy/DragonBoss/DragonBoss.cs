using UnityEngine;

/// <summary>
/// 龙 BOSS 的敌人宿主：深渊守卫龙。
/// 组合通用 EnemyBase、DragonBossAttack 与龙息博弈招式；实现弹反接口（龙息不可弹反，
/// 其他招式被弹反后短暂硬直）。
/// </summary>
[RequireComponent(typeof(DragonBossAttack))]
public sealed class DragonBoss : EnemyBase, IParriable
{
    // 被弹反后的硬直时长，单位为秒。
    [SerializeField, Range(0.3f, 2f)] private float parriedStunDuration = 0.9f;

    /// <summary>被玩家精准防御命中：BOSS 硬直较短，避免被无限压制。</summary>
    public bool OnParried(Vector3 parrierPosition)
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai == null)
            return false;

        ai.Stagger(parriedStunDuration);
        return true;
    }
}
