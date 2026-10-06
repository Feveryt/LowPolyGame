using UnityEngine;

/// <summary>
/// 石头人的具体敌人宿主。
/// 负责组合通用 EnemyBase 与 StoneGolemAttack；石头人专属攻击参数由独立组件和配置资产维护。
/// 实现弹反接口：被玩家精准防御后进入可惩罚的硬直窗口。
/// </summary>
[RequireComponent(typeof(StoneGolemAttack))]
[RequireComponent(typeof(StoneGolemDefend))]
public sealed class StoneGolem : EnemyBase, IParriable
{
    // 弹反硬直持续时间，单位为秒。
    [SerializeField, Range(0.5f, 3f)] private float parriedStunDuration = 1.3f;

    /// <summary>被玩家精准防御命中：进入较长的硬直窗口，供玩家惩罚。</summary>
    public bool OnParried(Vector3 parrierPosition)
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai == null)
            return false;

        ai.Stagger(parriedStunDuration);
        return true;
    }
}
