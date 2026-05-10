using UnityEngine;

/// <summary>
/// 伤害计算系统
/// 职责：统一伤害公式计算、元素属性克制、伤害类型判定
/// </summary>
public static class DamageSystem
{
    // 伤害类型枚举：Physical, Fire, Ice, Lightning, Poison, Holy, Dark
    // 伤害信息结构体：DamageInfo（伤害值、类型、来源、是否暴击、击退力）

    // 计算最终伤害：CalculateDamage(AttackInfo attacker, DefenseInfo defender)
    // 公式示例：基础攻击力 * 技能倍率 * (1 - 防御减免) * 元素克制系数 * 暴击系数 + 随机浮动
    // 元素克制表：火克冰、冰克雷、雷克火等
    // 获取克制系数：GetElementBonus(ElementType attack, ElementType defense)
    // 是否触发暴击：RollCrit(float critRate)
}
