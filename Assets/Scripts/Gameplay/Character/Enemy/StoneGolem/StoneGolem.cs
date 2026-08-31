using UnityEngine;

/// <summary>
/// 石头人的具体敌人宿主。
/// 负责组合通用 EnemyBase 与 StoneGolemAttack；石头人专属攻击参数由独立组件和配置资产维护。
/// </summary>
[RequireComponent(typeof(StoneGolemAttack))]
public sealed class StoneGolem : EnemyBase
{
}
