using QFramework;

/// <summary>
/// 游戏全局状态枚举
/// </summary>
public enum GameState
{
    MainMenu,   // 主菜单
    Playing,    // 游戏中
    Paused,     // 暂停
    Cutscene,   // 过场动画
    GameOver,   // 游戏结束
}

/// <summary>
/// 游戏状态数据模型（Model 层）
///
/// 职责：只负责"数据存储 + 变化通知"，不包含任何逻辑。
/// 使用 BindableProperty 数据绑定：值变化时自动通知所有监听者（如 UI、行为系统）。
///
/// 注意：状态变更请通过 ChangeGameStateCommand 完成，不要在表现层直接改 Value，
/// 以保证"状态变更必须走 Command"的架构约束。
/// </summary>
public class GameStateModel : AbstractModel
{
    /// <summary>当前游戏状态（数据绑定属性，UI 可直接监听）</summary>
    public BindableProperty<GameState> CurrentState { get; } = new(GameState.MainMenu);

    protected override void OnInit()
    {
        // 初始化钩子：可在这里从存档恢复上次的游戏状态（后续接入 SaveManager 时使用）
        // CurrentState.SetValueWithoutEvent(GameState.Playing);
    }
}
