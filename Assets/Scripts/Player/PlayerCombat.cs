using UnityEngine;

/// <summary>
/// 玩家战斗系统
/// 职责：普通攻击连段、重击、技能释放入口、受击/硬直/闪避无敌帧
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    // 攻击状态枚举：Idle, LightAttack1, LightAttack2, LightAttack3, HeavyAttack, Skill
    // 当前连段索引（轻攻击最多3段）
    // 攻击检测（通过 HitDetection 碰撞体）
    // 攻击输入缓冲窗口（允许提前输入）
    // 闪避（无敌帧持续时间、冷却）
    // 受击处理：扣血 -> 击退 -> 硬直 -> 死亡
    // 霸体/韧性系统（防止无限硬直）
}
