using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人的通用有限状态机决策控制器。
/// 负责站岗或巡逻、索敌、追击、攻击状态切换、受击、死亡与脱战返回；具体攻击交由 EnemyAttackBehaviour。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAI : MonoBehaviour
{
    /// <summary>敌人第一版通用 AI 状态标识。</summary>
    public enum EnemyState
    {
        /// <summary>在出生点附近等待目标进入索敌范围。</summary>
        Idle,
        /// <summary>在固定出生圆内前往随机 NavMesh 可达点。</summary>
        Patrol,
        /// <summary>向有效目标导航移动。</summary>
        Chase,
        /// <summary>停止移动并等待专属攻击动作结束。</summary>
        Attack,
        /// <summary>攻击结束后原地面向目标，等待下一轮攻击或目标离开攻击范围。</summary>
        AttackWait,
        /// <summary>丢失目标后导航返回固定出生点。</summary>
        Return,
        /// <summary>受到非致命伤害后的短暂硬直。</summary>
        Hurt,
        /// <summary>生命归零后的终止状态。</summary>
        Dead,
    }

    // 包含配置、数值、导航和攻击组件引用的敌人宿主。
    [SerializeField] private EnemyBase enemy;
    // 可选的手动目标；为空时自动查找场景中的 PlayerStats。
    [SerializeField] private Transform targetOverride;

    // 项目自研通用状态机实例。
    private StateMachine<EnemyAI, EnemyState> stateMachine;
    // 攻击结束后允许再次攻击的绝对时间。
    private float nextAttackAllowedTime;
    // 非致命伤害到达时由 DamageReceived 事件置位。
    private bool hurtRequested;
    // 受击动画未启动时的保底截止时间。
    private float hurtStartDeadline;
    // 当前随机巡逻目标点。
    private Vector3 patrolDestination;
    // 是否已经持有一个待抵达的巡逻目标点。
    private bool hasPatrolDestination;
    // 当前巡逻停顿或选点失败后的下一次选点时间。
    private float patrolNextSelectTime;

    /// <summary>当前正在运行的 AI 状态；未初始化时默认 Idle。</summary>
    public EnemyState CurrentState => stateMachine != null && stateMachine.IsInitialized ? stateMachine.CurrentState : EnemyState.Idle;

    // 缓存宿主组件并尝试解析默认目标。
    private void Awake()
    {
        enemy = enemy != null ? enemy : GetComponent<EnemyBase>();
        ResolveTarget();
    }

    // 订阅数值受击事件。
    private void OnEnable()
    {
        SubscribeEvents();
    }

    // 在所有组件 Awake 完成后创建并初始化状态机。
    private void Start()
    {
        if (!ValidateDependencies())
        {
            enabled = false;
            return;
        }

        CreateStateMachine();
        stateMachine.Initialize(this);
    }

    // 每帧驱动一次状态转移和当前状态逻辑。
    private void Update()
    {
        stateMachine?.Tick(this);
    }

    // 禁用时取消订阅并停止导航，避免对象复用后保留路径。
    private void OnDisable()
    {
        UnsubscribeEvents();
        StopNavigation();
    }

    /// <summary>为外部脚本指定或替换当前追踪目标。</summary>
    public void SetTarget(Transform target)
    {
        targetOverride = target;
        enemy?.SetTarget(target);
    }

    // 创建站岗、巡逻、战斗和终止状态及其转移条件。
    private void CreateStateMachine()
    {
        stateMachine = new StateMachine<EnemyAI, EnemyState>();
        stateMachine.AddState(EnemyState.Idle, new State<EnemyAI>(onEnter: _ => EnterIdle()));
        stateMachine.AddState(EnemyState.Patrol, new State<EnemyAI>(onEnter: _ => EnterPatrol(), onLogic: _ => UpdatePatrol()));
        stateMachine.AddState(EnemyState.Chase, new State<EnemyAI>(onEnter: _ => EnterChase(), onLogic: _ => UpdateChase()));
        stateMachine.AddState(
            EnemyState.Attack,
            new State<EnemyAI>(
                onEnter: _ => EnterAttack(),
                onLogic: _ => FaceTarget(),
                canExit: _ => enemy.AttackBehaviour.IsAttackFinished));
        stateMachine.AddState(EnemyState.AttackWait, new State<EnemyAI>(onEnter: _ => EnterAttackWait(), onLogic: _ => UpdateAttackWait()));
        stateMachine.AddState(EnemyState.Return, new State<EnemyAI>(onEnter: _ => EnterReturn(), onLogic: _ => UpdateReturn()));
        stateMachine.AddState(
            EnemyState.Hurt,
            new State<EnemyAI>(
                onEnter: _ => EnterHurt(),
                canExit: _ => IsHurtFinished()));
        stateMachine.AddState(EnemyState.Dead, new State<EnemyAI>(onEnter: _ => EnterDead()));
        stateMachine.SetStartState(GetNonCombatState());

        // 死亡和受击拥有最高优先级，确保生命归零后不继续移动或攻击。
        stateMachine.AddAnyTransition(EnemyState.Dead, _ => enemy.Stats == null || !enemy.Stats.IsAlive, force: true, priority: 100);
        stateMachine.AddAnyTransition(
            EnemyState.Hurt,
            _ => CurrentState != EnemyState.Hurt && hurtRequested && enemy.Stats.IsAlive,
            priority: 50);

        stateMachine.AddTransition(EnemyState.Idle, EnemyState.Chase, _ => CanDetectTarget());
        stateMachine.AddTransition(EnemyState.Patrol, EnemyState.Chase, _ => CanDetectTarget());
        stateMachine.AddTransition(EnemyState.Chase, EnemyState.Attack, _ => IsTargetWithinAttackRange());
        stateMachine.AddTransition(EnemyState.Chase, EnemyState.Return, _ => !HasValidTarget() || IsTargetOutsideLoseRange());
        stateMachine.AddTransition(EnemyState.Attack, EnemyState.AttackWait, _ => HasValidTarget() && !IsTargetOutsideLoseRange());
        stateMachine.AddTransition(EnemyState.Attack, EnemyState.Return, _ => !HasValidTarget() || IsTargetOutsideLoseRange());
        stateMachine.AddTransition(EnemyState.AttackWait, EnemyState.Attack, _ => IsAttackWaitFinished() && IsTargetWithinAttackRange());
        stateMachine.AddTransition(EnemyState.AttackWait, EnemyState.Chase, _ => HasValidTarget() && !IsTargetOutsideLoseRange() && !IsTargetWithinAttackRange());
        stateMachine.AddTransition(EnemyState.AttackWait, EnemyState.Return, _ => !HasValidTarget() || IsTargetOutsideLoseRange());
        stateMachine.AddTransition(EnemyState.Return, GetNonCombatState(), _ => HasReturnedHome());
        stateMachine.AddTransition(EnemyState.Hurt, EnemyState.Chase, _ => HasValidTarget() && !IsTargetOutsideLoseRange());
        stateMachine.AddTransition(EnemyState.Hurt, EnemyState.Return, _ => !HasValidTarget() || IsTargetOutsideLoseRange());
    }

    // 返回当前配置对应的非战斗状态。
    private EnemyState GetNonCombatState()
    {
        return enemy.Config.BehaviourType == EnemyBehaviourType.Patrol ? EnemyState.Patrol : EnemyState.Idle;
    }

    // 进入站岗待机时停止导航并恢复出生朝向。
    private void EnterIdle()
    {
        StopNavigation();
        enemy.Animation?.StopMovement();
        transform.rotation = Quaternion.Euler(0f, enemy.SpawnRotation.eulerAngles.y, 0f);
    }

    // 进入巡逻状态时清空旧路径，并立即尝试选择第一个巡逻点。
    private void EnterPatrol()
    {
        StopNavigation();
        enemy.Animation?.StopMovement();
        hasPatrolDestination = false;
        patrolNextSelectTime = Time.time;
    }

    // 在固定出生圆内随机选择 NavMesh 可达点，并在抵达后随机等待。
    private void UpdatePatrol()
    {
        if (CanDetectTarget())
            return;

        NavMeshAgent agent = enemy.NavigationAgent;
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (hasPatrolDestination && HasReachedPosition(patrolDestination))
        {
            StopNavigation();
            enemy.Animation?.StopMovement();
            hasPatrolDestination = false;
            patrolNextSelectTime = Time.time + Random.Range(enemy.Config.PatrolWaitMin, enemy.Config.PatrolWaitMax);
        }

        if (hasPatrolDestination || Time.time < patrolNextSelectTime)
            return;

        if (!TrySelectPatrolDestination(out patrolDestination))
        {
            patrolNextSelectTime = Time.time + Mathf.Max(0.25f, enemy.Config.PatrolWaitMin);
            return;
        }

        agent.isStopped = false;
        agent.speed = enemy.Config.ReturnSpeed;
        agent.stoppingDistance = enemy.Config.ReturnArrivalDistance;
        agent.SetDestination(patrolDestination);
        enemy.Animation?.SetMovement(EnemyMovementAnimation.WalkForward);
        hasPatrolDestination = true;
    }

    // 在出生圆内采样并验证 NavMesh 可达巡逻点。
    private bool TrySelectPatrolDestination(out Vector3 destination)
    {
        const int attempts = 8;
        float radius = enemy.Config.PatrolRadius;
        if (radius <= Mathf.Epsilon)
        {
            destination = enemy.HomePosition;
            return true;
        }

        for (int index = 0; index < attempts; index++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = enemy.HomePosition + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, Mathf.Max(1f, radius * 0.5f), NavMesh.AllAreas))
                continue;

            Vector3 horizontalDelta = hit.position - enemy.HomePosition;
            horizontalDelta.y = 0f;
            if (horizontalDelta.sqrMagnitude > radius * radius)
                continue;

            destination = hit.position;
            return true;
        }

        destination = transform.position;
        return false;
    }

    // 进入追击时播放前进奔跑动画。
    private void EnterChase()
    {
        enemy.Animation?.SetMovement(EnemyMovementAnimation.RunForward);
    }

    // 持续向目标设置导航目的地，并在攻击距离外追击。
    private void UpdateChase()
    {
        if (!HasValidTarget())
            return;

        NavMeshAgent agent = enemy.NavigationAgent;
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.speed = enemy.Config.ChaseSpeed;
        agent.stoppingDistance = enemy.AttackBehaviour.AttackRange * 0.9f;
        agent.SetDestination(GetTargetPosition());
    }

    // 停止导航、朝向目标并启动本轮专属攻击。
    private void EnterAttack()
    {
        StopNavigation();
        FaceTarget();
        enemy.AttackBehaviour.BeginAttack();
    }

    // 进入攻击等待时保持原地待机，并为下一轮攻击抽取专属随机间隔。
    private void EnterAttackWait()
    {
        StopNavigation();
        enemy.Animation?.StopMovement();
        nextAttackAllowedTime = Time.time + enemy.AttackBehaviour.GetNextAttackDelay();
    }

    // 攻击等待期间持续面向目标，避免玩家在攻击范围内仍触发追击动画。
    private void UpdateAttackWait()
    {
        StopNavigation();
        FaceTarget();
    }

    // 进入返回状态时播放前进步行动画。
    private void EnterReturn()
    {
        enemy.Animation?.SetMovement(EnemyMovementAnimation.WalkForward);
    }

    // 持续导航回固定出生点。
    private void UpdateReturn()
    {
        NavMeshAgent agent = enemy.NavigationAgent;
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.speed = enemy.Config.ReturnSpeed;
        agent.stoppingDistance = enemy.Config.ReturnArrivalDistance;
        agent.SetDestination(enemy.HomePosition);
    }

    // 进入受击状态时停止导航、播放动作并消耗本次受击请求。
    private void EnterHurt()
    {
        hurtRequested = false;
        StopNavigation();
        hurtStartDeadline = Time.time + enemy.Config.HurtFallbackDuration;
        enemy.Animation?.PlayHurt();
    }

    // 判断受击动画已结束，或未播放时达到保底时长。
    private bool IsHurtFinished()
    {
        EnemyAnimationBehaviour animation = enemy.Animation;
        return animation == null || (Time.time >= hurtStartDeadline && !animation.IsPlayingHurt);
    }

    // 进入死亡状态后彻底停止导航并触发死亡动画。
    private void EnterDead()
    {
        StopNavigation();
        enemy.Animation?.PlayDie();
    }

    // 响应非致命伤害并请求下次状态机逻辑切换到受击。
    private void OnDamageReceived(DamageResult result)
    {
        if (result.WasApplied && !result.WasLethal)
            hurtRequested = true;
    }

    // 在巡逻、追击、返回等逻辑中安全停止当前导航路径。
    private void StopNavigation()
    {
        NavMeshAgent agent = enemy != null ? enemy.NavigationAgent : null;
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    // 解析手动目标、EnemyBase 引用或场景中的玩家数值组件。
    private void ResolveTarget()
    {
        if (targetOverride != null)
        {
            enemy?.SetTarget(targetOverride);
            return;
        }

        if (enemy != null && enemy.Target != null)
            return;

        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats != null)
            enemy?.SetTarget(playerStats.transform);
    }

    // 检查状态机必须具备的配置、数值和攻击依赖。
    private bool ValidateDependencies()
    {
        if (enemy == null || enemy.Config == null || enemy.Stats == null || enemy.AttackBehaviour == null)
        {
            Debug.LogError($"[{nameof(EnemyAI)}] {name} 缺少 EnemyBase、EnemyConfig、EnemyStats 或 EnemyAttackBehaviour。", this);
            return false;
        }

        if (enemy.NavigationAgent == null)
            Debug.LogWarning($"[{nameof(EnemyAI)}] {name} 未挂载 NavMeshAgent，敌人将无法巡逻、追击和返回。", this);
        if (enemy.Animation == null)
            Debug.LogWarning($"[{nameof(EnemyAI)}] {name} 未挂载 EnemyAnimationBehaviour，敌人将不播放动画。", this);

        return true;
    }

    // 订阅敌人数值组件的受击事件。
    private void SubscribeEvents()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyBase>();

        if (enemy?.Stats != null)
            enemy.Stats.DamageReceived += OnDamageReceived;
    }

    // 取消敌人数值组件的受击事件订阅。
    private void UnsubscribeEvents()
    {
        if (enemy?.Stats != null)
            enemy.Stats.DamageReceived -= OnDamageReceived;
    }

    // 判断目标存在、已激活且仍有生命值。
    private bool HasValidTarget()
    {
        ResolveTarget();
        Transform target = enemy != null ? enemy.Target : null;
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        CharacterStats targetStats = target.GetComponentInParent<CharacterStats>();
        return targetStats == null || targetStats.IsAlive;
    }

    // 判断待机或巡逻敌人是否能够发现目标。
    private bool CanDetectTarget()
    {
        return HasValidTarget() && GetHorizontalDistanceToTarget() <= enemy.Config.DetectionRange && HasLineOfSight();
    }

    // 判断当前目标是否超过追击允许距离。
    private bool IsTargetOutsideLoseRange()
    {
        return GetHorizontalDistanceToTarget() > enemy.Config.LoseTargetRange;
    }

    // 判断有效目标是否位于当前敌人的攻击距离内。
    private bool IsTargetWithinAttackRange()
    {
        return HasValidTarget() && GetHorizontalDistanceToTarget() <= enemy.AttackBehaviour.AttackRange;
    }

    // 判断攻击后的随机等待时间是否已经结束。
    private bool IsAttackWaitFinished()
    {
        return Time.time >= nextAttackAllowedTime;
    }

    // 判断敌人是否已回到固定出生点附近。
    private bool HasReturnedHome()
    {
        return HasReachedPosition(enemy.HomePosition);
    }

    // 判断敌人是否已到达指定位置的水平容差范围内。
    private bool HasReachedPosition(Vector3 position)
    {
        Vector3 delta = transform.position - position;
        delta.y = 0f;
        return delta.sqrMagnitude <= enemy.Config.ReturnArrivalDistance * enemy.Config.ReturnArrivalDistance;
    }

    // 返回敌人与目标之间的水平距离。
    private float GetHorizontalDistanceToTarget()
    {
        Vector3 delta = GetTargetPosition() - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    // 返回当前目标位置；无目标时使用自身位置避免空引用。
    private Vector3 GetTargetPosition()
    {
        return enemy != null && enemy.Target != null ? enemy.Target.position : transform.position;
    }

    // 可选地检测敌人与目标之间是否被障碍物阻挡。
    private bool HasLineOfSight()
    {
        if (!enemy.Config.RequireLineOfSight)
            return true;

        Vector3 origin = transform.position + Vector3.up * enemy.Config.SightHeight;
        Vector3 destination = GetTargetPosition() + Vector3.up * enemy.Config.SightHeight;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        return distance <= Mathf.Epsilon || !Physics.Raycast(
            origin,
            direction / distance,
            distance,
            enemy.Config.ObstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    // 在攻击状态期间平滑面向当前目标。
    private void FaceTarget()
    {
        if (!HasValidTarget())
            return;

        Vector3 direction = GetTargetPosition() - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            enemy.Config.AngularSpeed * Time.deltaTime);
    }

    // 在 Scene 视图中显示索敌、攻击、脱战和巡逻范围，便于调整配置。
    private void OnDrawGizmosSelected()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyBase>();
        if (enemy == null || enemy.Config == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemy.Config.DetectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemy.AttackBehaviour != null ? enemy.AttackBehaviour.AttackRange : 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, enemy.Config.LoseTargetRange);

        if (enemy.Config.BehaviourType == EnemyBehaviourType.Patrol)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(enemy.HomePosition, enemy.Config.PatrolRadius);
        }
    }
}
