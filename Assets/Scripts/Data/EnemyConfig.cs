using UnityEngine;

/// <summary>
/// 敌人的通用静态配置资产，负责基础数值引用、索敌、导航与非战斗行为参数。
/// 具体攻击方式由各敌人专属的 EnemyAttackBehaviour 配置。
/// </summary>
[CreateAssetMenu(fileName = "Enemy_", menuName = "RPG/Enemy/Enemy Definition")]
public sealed class EnemyConfig : ScriptableObject
{
    [Header("基础数值")]
    // 敌人的生命、攻击与防御配置资产。
    [SerializeField] private CharacterStatsDefinition statsDefinition;

    [Header("行为类型")]
    // 敌人在非战斗状态下采用站岗或巡逻行为。
    [SerializeField] private EnemyBehaviourType behaviourType = EnemyBehaviourType.Guard;
    // 巡逻敌人以出生点为圆心可选择的最大半径，单位为米。
    [SerializeField, Min(0f)] private float patrolRadius = 5f;
    // 巡逻敌人到达一个目标点后的最短等待时间，单位为秒。
    [SerializeField, Min(0f)] private float patrolWaitMin = 1f;
    // 巡逻敌人到达一个目标点后的最长等待时间，单位为秒。
    [SerializeField, Min(0f)] private float patrolWaitMax = 3f;

    [Header("索敌")]
    // 敌人发现目标的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float detectionRange = 10f;
    // 追击期间允许目标离开的最大水平距离，单位为米。
    [SerializeField, Min(0f)] private float loseTargetRange = 16f;
    // 用于射线遮挡检测的观察点高度，单位为米。
    [SerializeField, Min(0f)] private float sightHeight = 1.5f;
    // 是否要求敌人与目标之间没有指定层的遮挡物。
    [SerializeField] private bool requireLineOfSight;
    // 阻挡敌人索敌视线的场景层。
    [SerializeField] private LayerMask obstacleMask;

    [Header("导航移动")]
    // 追击目标时 NavMeshAgent 的移动速度，单位为米每秒。
    [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
    // 返回出生点和巡逻时 NavMeshAgent 的移动速度，单位为米每秒。
    [SerializeField, Min(0f)] private float returnSpeed = 2.5f;
    // NavMeshAgent 的加速度，单位为米每二次方秒。
    [SerializeField, Min(0f)] private float acceleration = 16f;
    // NavMeshAgent 的水平转向速度，单位为度每秒。
    [SerializeField, Min(0f)] private float angularSpeed = 360f;
    // 判定已经抵达返回点或巡逻点的水平距离，单位为米。
    [SerializeField, Min(0f)] private float returnArrivalDistance = 0.2f;

    [Header("受击")]
    // 受击动作未能进入 Animator 时的保底等待时间，单位为秒。
    [SerializeField, Min(0f)] private float hurtFallbackDuration = 0.25f;

    /// <summary>敌人的基础生命、攻击与防御配置。</summary>
    public CharacterStatsDefinition StatsDefinition => statsDefinition;
    /// <summary>敌人的非战斗行为类型。</summary>
    public EnemyBehaviourType BehaviourType => behaviourType;
    /// <summary>巡逻圆的半径。</summary>
    public float PatrolRadius => patrolRadius;
    /// <summary>巡逻到点后的最短等待时间。</summary>
    public float PatrolWaitMin => patrolWaitMin;
    /// <summary>巡逻到点后的最长等待时间。</summary>
    public float PatrolWaitMax => patrolWaitMax;
    /// <summary>待机或巡逻状态的索敌距离。</summary>
    public float DetectionRange => detectionRange;
    /// <summary>追击状态的脱战距离。</summary>
    public float LoseTargetRange => loseTargetRange;
    /// <summary>视线检测使用的观察点高度。</summary>
    public float SightHeight => sightHeight;
    /// <summary>是否启用障碍物遮挡检测。</summary>
    public bool RequireLineOfSight => requireLineOfSight;
    /// <summary>阻挡敌人视线的物理层。</summary>
    public LayerMask ObstacleMask => obstacleMask;
    /// <summary>追击速度。</summary>
    public float ChaseSpeed => chaseSpeed;
    /// <summary>返回和巡逻速度。</summary>
    public float ReturnSpeed => returnSpeed;
    /// <summary>导航转向速度。</summary>
    public float AngularSpeed => angularSpeed;
    /// <summary>导航加速度。</summary>
    public float Acceleration => acceleration;
    /// <summary>返回或巡逻到点的判定距离。</summary>
    public float ReturnArrivalDistance => returnArrivalDistance;
    /// <summary>受击动画未启动时的保底等待时间。</summary>
    public float HurtFallbackDuration => hurtFallbackDuration;

    // 在 Inspector 中限制距离、速度与巡逻等待参数为合法值。
    private void OnValidate()
    {
        patrolRadius = Mathf.Max(0f, patrolRadius);
        patrolWaitMin = Mathf.Max(0f, patrolWaitMin);
        patrolWaitMax = Mathf.Max(patrolWaitMin, patrolWaitMax);
        detectionRange = Mathf.Max(0f, detectionRange);
        loseTargetRange = Mathf.Max(detectionRange, loseTargetRange);
        sightHeight = Mathf.Max(0f, sightHeight);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        angularSpeed = Mathf.Max(0f, angularSpeed);
        returnArrivalDistance = Mathf.Max(0f, returnArrivalDistance);
        hurtFallbackDuration = Mathf.Max(0f, hurtFallbackDuration);
    }
}

/// <summary>敌人在非战斗状态下采用的基础行为。</summary>
public enum EnemyBehaviourType
{
    /// <summary>停留在出生点附近等待目标。</summary>
    Guard,
    /// <summary>在固定出生点半径内随机巡逻。</summary>
    Patrol,
}
