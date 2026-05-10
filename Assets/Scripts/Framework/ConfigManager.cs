using UnityEngine;

/// <summary>
/// 配置表管理器
/// 职责：读取策划配置表（Excel/ScriptableObject/JSON），提供数据查询接口
/// </summary>
public class ConfigManager : MonoBehaviour
{
    // 怪物配置表：Dictionary<int, EnemyConfig> enemyConfigDict
    // 技能配置表：Dictionary<int, SkillConfig> skillConfigDict
    // 物品配置表：Dictionary<int, ItemConfig> itemConfigDict
    // 任务配置表：Dictionary<int, QuestConfig> questConfigDict
    // 关卡配置表：Dictionary<int, LevelConfig> levelConfigDict
    // 对话配置表：Dictionary<int, DialogConfig> dialogConfigDict

    // 初始化加载：LoadAllConfigs()
    // 通用查询：T GetConfig<T>(int id)
    // 模糊查询（用于搜索/配方匹配等）
}
