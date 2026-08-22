using UnityEngine;

/// <summary>
/// 敌人基类
/// 职责：敌人通用生命周期（出生/激活/死亡/回收），组件引用聚合
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    // EnemyAI 引用 / EnemyStats 引用 / EnemyAnimation 引用
    // 敌人类型枚举：Normal, Elite, Boss
    // 敌人 ID（用于查配置表）
    // 出生/激活：Spawn(Vector3 position, int enemyId)
    // 死亡处理：Die() -> 播放死亡动画 -> 掉落物 -> 经验奖励 -> 回收进对象池
    // 受击：OnHit(DamageInfo damage) -> 交给 EnemyStats 处理
    // 仇恨目标引用
    // 是否存活
}
