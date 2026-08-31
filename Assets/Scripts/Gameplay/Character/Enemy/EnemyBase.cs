using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 所有可战斗敌人的公共宿主基类。
/// 负责聚合配置、数值、动画和导航组件，并维护出生点与目标引用；
/// 具体决策行为由同物体上的 EnemyAI 组件承担。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyAI))]
public abstract class EnemyBase : MonoBehaviour
{
    // 该敌人的静态数值、AI、移动与攻击配置资产。
    [SerializeField] private EnemyConfig config;
    // 敌人优先追踪的目标；未绑定时 EnemyAI 会自动查找 PlayerStats。
    [SerializeField] private Transform target;
    // 敌人近战命中检测使用的本地攻击原点；为空时使用敌人根节点。
    [SerializeField] private Transform attackOrigin;
    // 敌人的运行时基础数值组件。
    [SerializeField] private EnemyStats stats;
    // 敌人的 Animator 参数适配组件。
    [SerializeField] private EnemyAnimationBehaviour enemyAnimation;
    // 敌人的 NavMesh 导航组件。
    [SerializeField] private NavMeshAgent navigationAgent;
    // 敌人的专属攻击行为组件，由 EnemyAI 通过抽象接口调用。
    [SerializeField] private EnemyAttackBehaviour attackBehaviour;

    // 敌人首次激活时记录的返回位置。
    private Vector3 spawnPosition;
    // 敌人首次激活时记录的返回朝向。
    private Quaternion spawnRotation;

    /// <summary>当前敌人的静态配置资产。</summary>
    public EnemyConfig Config => config;
    /// <summary>敌人的运行时生命、攻击和防御组件。</summary>
    public EnemyStats Stats => stats;
    /// <summary>驱动该敌人 Animator 的表现组件。</summary>
    public EnemyAnimationBehaviour Animation => enemyAnimation;
    /// <summary>负责追击和返回的导航组件。</summary>
    public NavMeshAgent NavigationAgent => navigationAgent;
    /// <summary>敌人的具体攻击实现。</summary>
    public EnemyAttackBehaviour AttackBehaviour => attackBehaviour != null
        ? attackBehaviour
        : GetComponent<EnemyAttackBehaviour>();
    /// <summary>当前优先追踪的目标，可由 EnemyAI 自动补全。</summary>
    public Transform Target => target;
    /// <summary>近战球形命中检测的空间原点。</summary>
    public Transform AttackOrigin => attackOrigin != null ? attackOrigin : transform;
    /// <summary>固定出生位置；运行时用于脱战返回和巡逻圆心，编辑器中预览当前 Transform 位置。</summary>
    public Vector3 HomePosition => Application.isPlaying ? spawnPosition : transform.position;
    /// <summary>敌人的出生位置，用于兼容已有脱战逻辑。</summary>
    public Vector3 SpawnPosition => HomePosition;
    /// <summary>敌人的出生朝向，用于返回完成后的待机恢复。</summary>
    public Quaternion SpawnRotation => spawnRotation;

    // 缓存依赖组件、应用配置数值并记录出生点。
    protected virtual void Awake()
    {
        stats = stats != null ? stats : GetComponent<EnemyStats>();
        enemyAnimation = enemyAnimation != null ? enemyAnimation : GetComponentInChildren<EnemyAnimationBehaviour>();
        navigationAgent = navigationAgent != null ? navigationAgent : GetComponent<NavMeshAgent>();
        attackBehaviour = attackBehaviour != null ? attackBehaviour : GetComponent<EnemyAttackBehaviour>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        if (config == null)
        {
            Debug.LogError($"[{nameof(EnemyBase)}] {name} 缺少 EnemyConfig 配置资产。", this);
            return;
        }

        if (stats != null && config.StatsDefinition != null)
            stats.SetDefinition(config.StatsDefinition);
        else if (config.StatsDefinition == null)
            Debug.LogError($"[{nameof(EnemyBase)}] {name} 的 EnemyConfig 未绑定 CharacterStatsDefinition。", this);

        ConfigureNavigationAgent();
    }

    /// <summary>为 EnemyAI 指定或替换当前追踪目标。</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // 将配置资产中的通用导航参数应用到 NavMeshAgent。
    private void ConfigureNavigationAgent()
    {
        if (navigationAgent == null || config == null)
            return;

        navigationAgent.acceleration = config.Acceleration;
        navigationAgent.angularSpeed = config.AngularSpeed;
    }
}
