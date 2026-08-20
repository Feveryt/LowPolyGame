using QFramework;
using UnityEngine;

/// <summary>
/// 游戏全局管理器（单例 + 架构引导器）
///
/// 职责：
/// 1. 场景启动时初始化 QFramework 架构（GameArchitecture.Interface 触发注册）
/// 2. 作为表现层入口，把外部调用转发为 Command（状态切换统一走 ChangeGameStateCommand）
/// 3. 持有场景级引用（角色预制体、出生点等）
///
/// 说明：真正的状态数据与流转逻辑在 GameStateModel / GameStateSystem 中，
/// 本类只负责"引导初始化"和"接收外部请求"，保持轻量。
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>单例实例</summary>
    public static GameManager Instance { get; private set; }

    // 角色预制体引用 & 出生点引用
    [Header("玩家设置")]
    // 场景开始时实例化的玩家预制体。
    [SerializeField] private GameObject playerPrefab;
    // 玩家生成时使用的位置与朝向。
    [SerializeField] private Transform spawnPoint;

    // 初始化单例、保持跨场景存活，并启动游戏架构。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化 QFramework 架构（幂等：已初始化过则直接返回现有实例）
        _ = GameArchitecture.Interface;
    }

    // 首帧前完成玩家生成。
    private void Start()
    {
        SpawnPlayer();
    }

    /// <summary>在出生点生成玩家</summary>
    private void SpawnPlayer()
    {
        if (playerPrefab != null && spawnPoint != null)
        {
            Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }

    // ---- 游戏状态切换（表现层入口 → Command）----

    // 通过命令将游戏切换到进行状态。
    public void StartGame() => SendStateCommand(GameState.Playing);

    // 通过命令将游戏切换到暂停状态。
    public void PauseGame() => SendStateCommand(GameState.Paused);

    // 通过命令将游戏从暂停恢复到进行状态。
    public void ResumeGame() => SendStateCommand(GameState.Playing);

    // 通过命令将游戏切换到结束状态。
    public void GameOver() => SendStateCommand(GameState.GameOver);

    // 创建并发送统一处理状态变更的命令。
    private void SendStateCommand(GameState state) =>
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));

    /// <summary>返回主菜单（供 UI 调用）</summary>
    public void BackToMainMenu() => SendStateCommand(GameState.MainMenu);

    // 应用退出前保留架构清理扩展点。
    private void OnApplicationQuit()
    {
        // 退出前清理架构（可选）
        // GameArchitecture.Interface.Deinit();
    }
}
