using UnityEngine;

/// <summary>
/// 敌人动画控制器
/// 职责：驱动敌人 Animator，根据 AI 状态切换动画
/// </summary>
public class EnemyAnimation : MonoBehaviour
{
    // Animator 引用
    // 动画参数：Idle, Walk, Run, Attack, Skill, Hit, Die
    // 根据 AI 状态更新动画参数
    // 攻击动画事件（关键帧回调，用于命中检测）
    // 死亡动画播完后触发回收
    // 动画速度控制（受减速 Buff 影响）
}
