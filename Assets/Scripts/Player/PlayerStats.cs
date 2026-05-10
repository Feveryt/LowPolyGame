using UnityEngine;

/// <summary>
/// 玩家属性/数值系统
/// 职责：生命值、魔法值、体力值、等级经验、属性点（力量/敏捷/智力/体力）
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // ---- 基础属性 ----
    // 最大生命值 / 当前生命值
    // 最大魔法值 / 当前魔法值
    // 最大体力值 / 当前体力值
    // 攻击力 / 防御力 / 暴击率 / 暴击伤害
    // 等级 / 当前经验 / 升级所需经验

    // ---- 方法 ----
    // 受到伤害：TakeDamage(float damage, GameObject attacker)
    // 治疗：Heal(float amount)
    // 消耗魔法：ConsumeMana(float amount)
    // 消耗体力：ConsumeStamina(float amount)
    // 获得经验：GainExp(int exp)
    // 升级：LevelUp()（触发属性成长）
    // 死亡：Die()
    // 属性变化回调（通知 UI 更新）

    // 体力恢复协程（未使用时自动回复）
    // 受击无敌时间
}
