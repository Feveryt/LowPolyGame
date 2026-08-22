using UnityEngine;

/// <summary>
/// 伤害数字显示
/// 职责：在敌人/玩家头顶显示伤害/治疗数值，带飘字动画
/// </summary>
public class UIDamageNumber : MonoBehaviour
{
    // 普通伤害颜色（白色/黄色）
    // 暴击颜色（橙色，更大字号）
    // 治疗颜色（绿色）
    // 显示伤害：ShowDamage(Vector3 worldPosition, int damage, bool isCrit)
    // 显示治疗：ShowHeal(Vector3 worldPosition, int healAmount)
    // 飘字动画（上浮 + 渐隐，使用 DOTween 或协程）
    // 自动回收（动画播完回到对象池）
}
