using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能系统
/// 职责：技能学习/升级/释放，技能冷却管理，技能连招判断
/// </summary>
public class PlayerSkill : MonoBehaviour
{
    // 已学习技能列表（4个主动技能 + 被动技能）
    // 技能槽映射（按键 1~4 -> 技能ID）
    // 释放技能：CastSkill(int slotIndex)
    // 学习技能：LearnSkill(int skillId)
    // 升级技能：UpgradeSkill(int skillId)
    // 检查冷却：IsOnCooldown(int slotIndex)
    // 取消当前技能（闪避打断后摇）
    // 被动技能效果汇总（统计所有被动加成）
}
