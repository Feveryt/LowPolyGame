using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人血条的数据入口：登记可显示的敌人，并以 AI 战斗状态判断是否正在锁定玩家。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase), typeof(EnemyAI))]
public sealed class EnemyBossHealthTarget : MonoBehaviour
{
    // 当前启用的血条目标，避免 HUD 每帧扫描整个场景。
    private static readonly List<EnemyBossHealthTarget> activeTargets = new List<EnemyBossHealthTarget>();
    // 可选的 UI 名称；留空时读取敌人数值配置。
    [SerializeField] private string displayNameOverride;
    // 宿主及决策组件缓存。
    private EnemyBase enemy;
    private EnemyAI ai;

    /// <summary>当前启用的血条目标，只供 HUD 遍历。</summary>
    public static IReadOnlyList<EnemyBossHealthTarget> ActiveTargets => activeTargets;
    /// <summary>血量的真实来源。</summary>
    public EnemyStats Stats => enemy != null ? enemy.Stats : null;
    /// <summary>血条下方显示的名称。</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(displayNameOverride) ? displayNameOverride :
        Stats != null && Stats.Definition != null ? Stats.Definition.DisplayName : gameObject.name;

    /// <summary>只有追击、攻击、攻击等待和受击中的敌人，才视为正在与玩家交战。</summary>
    public bool IsEngagedWithPlayer
    {
        get
        {
            if (!isActiveAndEnabled || ai == null || !ai.isActiveAndEnabled || Stats == null || !Stats.IsAlive)
                return false;
            PlayerStats player = enemy.Target != null ? enemy.Target.GetComponentInParent<PlayerStats>() : null;
            if (player == null || !player.IsAlive)
                return false;
            EnemyAI.EnemyState state = ai.CurrentState;
            return state == EnemyAI.EnemyState.Chase || state == EnemyAI.EnemyState.Attack ||
                state == EnemyAI.EnemyState.AttackWait || state == EnemyAI.EnemyState.Hurt;
        }
    }

    // 清理跨次 Play 的静态登记，兼容关闭域重载的编辑器配置。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTargets() => activeTargets.Clear();

    // 缓存宿主，组件之间的 Awake 顺序不影响后续属性读取。
    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        ai = GetComponent<EnemyAI>();
    }

    // 敌人激活后进入 HUD 候选集合。
    private void OnEnable()
    {
        if (!activeTargets.Contains(this))
            activeTargets.Add(this);
    }

    // 禁用或销毁后移除候选，防止血条保留失效对象。
    private void OnDisable() => activeTargets.Remove(this);
}
