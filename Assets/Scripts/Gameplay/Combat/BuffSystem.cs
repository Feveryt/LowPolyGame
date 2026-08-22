using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff/Debuff 系统
/// 职责：管理角色身上的增益/减益效果，定时触发效果，层数叠加
/// </summary>
public class BuffSystem : MonoBehaviour
{
    // 当前 Buff 列表
    // 添加 Buff：AddBuff(BuffData buff, float duration, int stacks)
    // 移除 Buff：RemoveBuff(int buffId)
    // 刷新 Buff 持续时间：RefreshBuff(int buffId)
    // Tick 更新（每秒/每帧检查 Buff 是否到期）
    // 层数叠加逻辑（同类型 Buff 是覆盖、叠加还是刷新）
    // Buff 效果类型：属性修改、持续伤害(DOT)、持续治疗(HOT)、控制(眩晕/冰冻/减速)、护盾

    // Buff 数据结构：
    // - ID、名称、图标、持续时间、最大层数
    // - 效果类型、效果数值
    // - 是否可被驱散
    // - 来源对象
}
