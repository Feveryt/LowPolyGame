using QFramework;

/// <summary>
/// 游戏架构入口（QFramework Architecture）
///
/// 职责：
/// 1. 作为全游戏唯一的架构实例，持有所有 Model / System / Utility
/// 2. 定义依赖注册：所有业务模块在此登记，运行期通过接口获取，实现低耦合
/// 3. 提供统一的命令 / 查询 / 事件通道
///
/// 使用方式：
///   - 获取架构：GameArchitecture.Interface
///   - 获取数据：this.GetModel<T>() / this.GetSystem<T>() / this.GetUtility<T>()
///   - 变更状态：this.SendCommand(new ChangeGameStateCommand(...))
///   - 全局通知：this.SendEvent<T>() / this.RegisterEvent<T>(...)
///
/// 分层规则（QFramework）：
///   表现层(IController) → 发送 Command → 修改 Model → System 监听变化 → 广播事件 → 表现层响应
/// </summary>
public class GameArchitecture : Architecture<GameArchitecture>
{
    /// <summary>
    /// 架构初始化：注册所有 Model / System / Utility（按依赖顺序）
    /// </summary>
    protected override void Init()
    {
        // ---- Model 层：游戏数据 ----
        RegisterModel(new GameStateModel());

        // ---- System 层：业务逻辑 ----
        RegisterSystem(new GameStateSystem());

        // ---- Utility 层：基础设施（后续按需接入）----
        // RegisterUtility(new SaveUtility());      // 存档
        // RegisterUtility(new ConfigUtility());    // 配置表
        // RegisterUtility(new PoolUtility());      // 对象池

        // ---- 已有 MonoBehaviour 管理器（后续迁移为 System）----
        // AudioManager / UIManager / PoolManager / SaveManager / SceneLoader
        // 迁移方式：把纯逻辑抽成 System，场景表现保留在 MonoBehaviour 中
    }
}
