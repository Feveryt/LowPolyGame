using UnityEngine;

/// <summary>
/// 敌人属性/数值系统
/// 职责：生命值、攻击力、掉落配置、受击/死亡处理
/// </summary>
public class EnemyStats : MonoBehaviour
{
    // 最大生命值 / 当前生命值
    // 攻击力 / 防御力
    // 掉落物列表（ItemDrop[]）
    // 经验值奖励
    // 受击：TakeDamage(DamageInfo damage)
    // 死亡：Die() -> 触发掉落、经验、事件、对象池回收
    // 韧性值（受击累计到一定值触发硬直）
    // 霸体标志（BOSS技能读条期间不受硬直）
}
