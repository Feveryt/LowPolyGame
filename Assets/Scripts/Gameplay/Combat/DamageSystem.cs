using UnityEngine;

/// <summary>
/// 基础伤害计算器。
/// 当前只处理攻击、防御与倍率，保持为无状态纯函数，供命中检测和技能系统复用。
/// </summary>
public static class DamageSystem
{
    // 防御减伤公式的固定系数；防御等于该值时伤害减半。
    private const float DefenseScale = 100f;

    /// <summary>
    /// 使用递减减伤公式计算最终伤害。
    /// 公式为 Round(攻击 × 倍率 × 100 / (100 + 防御))，正伤害最少造成 1 点。
    /// </summary>
    public static DamageResult Calculate(DamageRequest request, int defense)
    {
        float rawDamage = Mathf.Max(0f, request.AttackPower) * Mathf.Max(0f, request.Multiplier);
        if (rawDamage <= 0f)
            return DamageResult.Ignored;

        float mitigation = DefenseScale / (DefenseScale + Mathf.Max(0, defense));
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * mitigation));
        return new DamageResult(rawDamage, finalDamage, true, false);
    }
}