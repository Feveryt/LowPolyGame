using UnityEngine;

/// <summary>
/// 敌人专用运行时属性组件。
/// 当前复用共享的生命、攻击和防御结算；掉落、韧性和死亡回收将在敌人战斗迭代中接入。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStats : CharacterStats
{
}