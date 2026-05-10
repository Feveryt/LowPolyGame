using UnityEngine;

/// <summary>
/// 敌人 AI 行为
/// 职责：状态机驱动（巡逻/追击/攻击/技能/逃跑/返回），行为树/FSM 实现
/// </summary>
public class EnemyAI : MonoBehaviour
{
    // 状态枚举：Idle, Patrol, Chase, Attack, Skill, Flee, Return, Dead
    // 当前状态 / 上一个状态

    // ---- 行为参数（来自配置表）----
    // 追击范围（发现玩家距离）
    // 攻击范围
    // 丢失目标范围（超出此距离返回出生点）
    // 巡逻点列表
    // 攻击冷却时间 / 技能冷却时间
    // 血量低于百分比触发逃跑

    // ---- 核心方法 ----
    // 状态切换：ChangeState(EnemyState newState)
    // 巡逻逻辑：巡逻点之间移动
    // 追击逻辑：NavMeshAgent.SetDestination(player.position)
    // 攻击逻辑：进入攻击范围后执行攻击
    // 索敌逻辑：检测玩家是否在视野/听觉范围内
    // LostTarget 后返回出生点
}
