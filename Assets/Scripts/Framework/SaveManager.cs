using UnityEngine;

/// <summary>
/// 存档系统管理器
/// 职责：玩家数据存档/读档/删档，自动保存，多存档槽位
/// </summary>
public class SaveManager : MonoBehaviour
{
    // 存档数据结构：SaveData（包含玩家属性、背包、技能、任务进度、场景位置等）
    // 存档槽位数量（3~5个）
    // 保存：Save(int slotIndex)
    // 加载：Load(int slotIndex)
    // 删除：DeleteSave(int slotIndex)
    // 获取存档信息列表（用于 UI 显示存档时间、等级等）
    // 自动保存（关键节点触发：BOSS战前、传送点、定时）
    // PlayerPrefs 存储设置项（音量、画质、按键绑定）

    // 存档加密（防止作弊）
    // 存档版本号（防止旧版本存档崩溃）
}
