using QFramework;
using UnityEngine;

/// <summary>
/// 游戏状态变更事件（跨系统广播用）
/// 由 GameStateSystem 在状态切换后发出，表现层监听后刷新 UI / 行为
/// </summary>
public struct GameStateChangedEvent
{
    /// <summary>切换前的全局游戏状态。</summary>
    public GameState From;
    /// <summary>切换后的全局游戏状态。</summary>
    public GameState To;

    // 使用前后状态创建状态变化事件数据。
    public GameStateChangedEvent(GameState from, GameState to)
    {
        From = from;
        To = to;
    }
}

/// <summary>
/// 游戏状态机系统（System 层）
///
/// 职责：管理游戏全局状态流转。
/// - 监听 Model 数据变化，向全游戏广播 GameStateChangedEvent（让 UI、玩家、敌人等感知状态切换）
/// - 是"数据 → 事件"的桥接层，后续可扩展状态进入/退出时的副作用（如暂停时 Time.timeScale）
///
/// 状态流转路径：
///   Controller --SendCommand--> ChangeGameStateCommand --修改--> GameStateModel
///   GameStateSystem --监听变化--> SendEvent(GameStateChangedEvent) --> 各表现层响应
/// </summary>
public class GameStateSystem : AbstractSystem
{
    // 上一次已广播的游戏状态，用于过滤重复通知。
    private GameState mLastState;

    // 监听状态模型变化并广播跨模块状态事件。
    protected override void OnInit()
    {
        var model = this.GetModel<GameStateModel>();
        mLastState = model.CurrentState.Value;

        // 监听 Model 变化 → 广播全局事件（UI / 玩家输入 / 敌人 AI 都在这里响应）
        model.CurrentState.Register(state =>
        {
            if (state == mLastState) return; // 忽略重复值

            var from = mLastState;
            mLastState = state;
            // 暂停状态冻结世界时间，其他可运行状态恢复正常时间。
            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            this.SendEvent(new GameStateChangedEvent(from, state));
        });

        // 状态副作用示例：暂停时冻结物理时间（取消注释即可启用）
        // model.CurrentState.Register(state => Time.timeScale = state == GameState.Paused ? 0f : 1f);
    }
}
