using UnityEngine;

/// <summary>
/// 玩家动画控制器
/// 职责：根据战斗/移动状态驱动 Animator 参数，动画事件回调
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    // Animator 引用
    // 动画参数常量：Idle, Walk, Run, LightAttack, HeavyAttack, Dodge, Hit, Die, Skill_1~4
    // 移动混合树参数：MoveX, MoveY（BlendTree 用于八方向移动）
    // 攻击速度乘数
    // 动画事件回调：OnAttackHit()（动画帧事件触发打击检测）
    // 动画事件回调：OnAttackEnd()（攻击动画结束可取消）
    // 切换动画层（普通/战斗/骑马等）
    // 播放一次性动画（如开门、攀爬）：PlayOneShot(string animName)
}
