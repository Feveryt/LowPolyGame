using UnityEngine;

/// <summary>
/// 游戏全局管理器（单例）
/// 职责：游戏状态机（主菜单/游戏中/暂停/过场），全局入口，协调各子系统初始化
/// </summary>
public class GameManager : MonoBehaviour
{
    // 单例实例
    // 游戏状态枚举：MainMenu, Playing, Paused, Cutscene, GameOver
    // 当前状态
    // 状态切换方法：StartGame(), PauseGame(), ResumeGame(), GameOver()
    // 各子系统初始化顺序：AudioManager -> ConfigManager -> PoolManager -> SceneManager -> UIManager
    // 角色预制体引用 & 出生点引用
    // 退出游戏处理
}
