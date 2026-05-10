using System;
using UnityEngine;

/// <summary>
/// 敌人配置数据定义
/// </summary>
[Serializable]
public class EnemyConfig
{
    // 敌人 ID / 名称
    // 敌人类型：Normal, Elite, Boss
    // 模型预设路径

    // ---- 属性 ----
    // 最大生命值 / 攻击力 / 防御力
    // 移动速度 / 追击速度
    // 索敌范围 / 攻击范围 / 丢失目标范围
    // 韧性值 / 霸体阈值

    // ---- AI 行为 ----
    // 巡逻速度 / 巡逻点列表
    // 攻击间隔 / 技能列表（技能 ID 数组）
    // 低血量逃跑阈值百分比
    // 是否守护特定区域（不追击超出范围）

    // ---- 奖励 ----
    // 掉落物表（ItemDrop[]）
    // 经验值
    // 金币范围
}
