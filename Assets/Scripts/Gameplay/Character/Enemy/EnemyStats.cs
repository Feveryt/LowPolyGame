using UnityEngine;

/// <summary>
/// 敌人专用运行时属性组件。
/// 当前复用共享的生命、攻击和防御结算；掉落、韧性和死亡回收将在敌人战斗迭代中接入。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStats : CharacterStats
{
    // 在父类初始化资源前，优先读取 EnemyBase 配置中引用的基础数值资产。
    protected override void Awake()
    {
        EnemyBase enemy = GetComponent<EnemyBase>();
        if (enemy != null && enemy.Config != null && enemy.Config.StatsDefinition != null)
            SetDefinition(enemy.Config.StatsDefinition);

        base.Awake();
    }
}
