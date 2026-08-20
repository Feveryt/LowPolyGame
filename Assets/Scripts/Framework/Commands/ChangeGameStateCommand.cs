using QFramework;

/// <summary>
/// 切换游戏状态命令（Command 层）
///
/// 职责：所有"游戏状态切换"必须通过本命令完成，禁止在表现层直接修改 Model。
/// Command 是无状态的：只接收参数、执行逻辑、不保存任何实例字段（除构造参数外）。
///
/// 用法：
///   GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(GameState.Paused));
///   或在任意 IController / ISystem 内：this.SendCommand(new ChangeGameStateCommand(GameState.Paused));
/// </summary>
public class ChangeGameStateCommand : AbstractCommand
{
    // 本次命令请求切换到的目标状态。
    private readonly GameState mTargetState;

    // 使用指定的目标状态创建切换命令。
    public ChangeGameStateCommand(GameState targetState)
    {
        mTargetState = targetState;
    }

    // 修改状态模型；状态系统会负责广播后续通知。
    protected override void OnExecute()
    {
        var model = this.GetModel<GameStateModel>();

        // 相同状态直接忽略，避免无意义的广播
        if (model.CurrentState.Value == mTargetState) return;

        // 只修改 Model —— 状态的"广播通知"由 GameStateSystem 负责
        model.CurrentState.Value = mTargetState;
    }
}
