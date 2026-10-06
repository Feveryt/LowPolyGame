using UnityEngine;

/// <summary>
/// 石头人的专属攻击组件。
/// 负责按距离与权重选择招式（拳法连段 / 跺地 AoE / 震波弹道 / 跳跃砸地 / 石肤格挡）、
/// 触发动画、在时窗内结算伤害，并管理二阶段狂暴强化。
/// 拳法命中沿用动画事件（AttackHitFrame），新招式使用归一化时间窗判定。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemDefend))]
public sealed class StoneGolemAttack : EnemyAttackBehaviour
{
    // 石头人专属攻击数值与判定配置资产。
    [SerializeField] private StoneGolemAttackDefinition definition;

    /// <summary>当前使用的攻击配置资产；供同宿主的石肤组件解析引用。</summary>
    public StoneGolemAttackDefinition DefinitionAsset => definition;
    // 广播攻击命中关键帧的通用动画适配组件。
    [SerializeField] private StoneGolemAnimation enemyAnimation;
    // 石头人 Animator，用于触发招式并检查专属攻击状态。
    [SerializeField] private Animator animator;
    // 攻击预警组件（红/黄光与地面圈）。
    [SerializeField] private AttackTelegraph telegraph;
    // 石肤格挡组件。
    [SerializeField] private StoneGolemDefend defend;

    // 石头人拳法 Trigger 参数哈希值。
    private static readonly int PunchTriggerHash = Animator.StringToHash("Punch");
    private static readonly int DoublePunchTriggerHash = Animator.StringToHash("Double Punch");
    private static readonly int SlamTriggerHash = Animator.StringToHash("Hit Ground");
    private static readonly int ShockwaveTriggerHash = Animator.StringToHash("Spell Cast");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
    // 石头人招式状态短名称哈希值。
    private static readonly int PunchStateHash = Animator.StringToHash("Punch");
    private static readonly int DoublePunchStateHash = Animator.StringToHash("DoublePunch");
    private static readonly int SlamStateHash = Animator.StringToHash("Hit Ground");
    private static readonly int ShockwaveStateHash = Animator.StringToHash("Spell Cast");
    private static readonly int JumpStateHash = Animator.StringToHash("Jump W Root");

    // 复用命中检测缓冲区，避免攻击关键帧产生 GC 分配。
    private readonly Collider[] hitBuffer = new Collider[16];
    // 当前宿主。
    private StoneGolem enemy;
    // 本轮攻击的招式队列；空队列表示无攻击进行。
    private readonly System.Collections.Generic.List<QueuedMove> moveQueue =
        new System.Collections.Generic.List<QueuedMove>();
    // 队列中相邻招式间的停顿剩余时间。
    private float linkDelayRemaining;
    // 队列中当前招式是否已触发动画。
    private bool currentMoveTriggered;
    // Animator 尚未进入攻击状态时的保底截止时间。
    private float actionStartDeadline;
    // 当前攻击是否已经观察到 Animator 进入攻击状态。
    private bool attackStateObserved;
    // 当前是否有一轮已开始但尚未结束的攻击。
    private bool attackActive;
    // 是否已进入二阶段。
    private bool phaseTwo;
    // 震波冷却截止时间。
    private float shockwaveReadyAt = float.NegativeInfinity;
    // 跳跃砸地冷却截止时间。
    private float jumpSlamReadyAt = float.NegativeInfinity;
    // 玩家连续命中计数，用于触发石肤。
    private int consecutivePlayerHits;
    // 玩家最近一次命中的时间。
    private float lastPlayerHitTime = float.NegativeInfinity;

    /// <summary>单个排队招式的运行时数据。</summary>
    private sealed class QueuedMove
    {
        public MoveType Type;
        public int StateHash;
        public float DamageMultiplier;
        public float GuardStamina;
        public bool Unblockable;
        public float HitWindowStart;
        public float HitWindowEnd;
        public float AoERadius;
        public Vector3 HitOffset;
        public bool HitConsumed;
    }

    // 石头人可选招式类型。
    private enum MoveType
    {
        Punch,
        DoublePunch,
        Slam,
        Shockwave,
        JumpSlam,
        Defend,
    }

    /// <summary>石头人允许发起攻击的最大水平距离；震波可用时扩大到远程范围。</summary>
    public override float AttackRange
    {
        get
        {
            if (definition == null)
                return 0f;
            return Time.time >= shockwaveReadyAt && definition.ShockwaveWeight > 0f
                ? definition.ShockwaveRange
                : Mathf.Max(definition.AttackRange, definition.JumpSlamMaxRange);
        }
    }

    /// <summary>返回石头人本轮攻击结束后的随机连击等待时间（二阶段缩短）。</summary>
    public override float GetNextAttackDelay()
    {
        if (definition == null)
            return 0f;

        float interval = Random.Range(definition.AttackIntervalMin, definition.AttackIntervalMax);
        return phaseTwo ? interval * definition.PhaseTwoIntervalScale : interval;
    }

    /// <summary>当前石头人攻击动作是否结束或配置缺失。</summary>
    public override bool IsAttackFinished
    {
        get
        {
            if (!attackActive || animator == null || definition == null)
                return true;

            // 队列已清空：本轮攻击结束。
            if (moveQueue.Count == 0)
                return true;

            // 石肤招式期间等待石肤结束。
            if (moveQueue[0].Type == MoveType.Defend && currentMoveTriggered)
                return !(defend != null && defend.IsActive) && linkDelayRemaining <= 0f;

            bool isPlayingMove = IsPlayingCurrentMoveState();
            if (isPlayingMove)
            {
                attackStateObserved = true;
                return false;
            }

            // 未观察到状态进入时按保底截止时间等待。
            return attackStateObserved || Time.time >= actionStartDeadline;
        }
    }

    // 缓存石头人、动画组件和 Animator 引用。
    private void Awake()
    {
        enemy = GetComponent<StoneGolem>();
        enemyAnimation = enemyAnimation != null ? enemyAnimation : GetComponentInChildren<StoneGolemAnimation>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        telegraph = telegraph != null ? telegraph : GetComponent<AttackTelegraph>();
        defend = defend != null ? defend : GetComponent<StoneGolemDefend>();
    }

    // 订阅通用动画组件广播的攻击关键帧与玩家命中事件（石肤触发）。
    private void OnEnable()
    {
        if (enemyAnimation != null)
            enemyAnimation.AttackHitFrame += OnAttackHitFrame;
        if (enemy != null && enemy.Stats != null)
            enemy.Stats.DamageReceived += OnPlayerHitGolem;
    }

    // 取消关键帧订阅，避免禁用或销毁后仍响应动画事件。
    private void OnDisable()
    {
        if (enemyAnimation != null)
            enemyAnimation.AttackHitFrame -= OnAttackHitFrame;
        if (enemy != null && enemy.Stats != null)
            enemy.Stats.DamageReceived -= OnPlayerHitGolem;
    }

    // 每帧推进招式队列：触发下一招、判定时窗伤害、检查二阶段。
    private void Update()
    {
        if (!attackActive || animator == null || definition == null)
            return;

        UpdatePhaseTwo();

        if (moveQueue.Count == 0)
            return;

        QueuedMove move = moveQueue[0];

        if (!currentMoveTriggered)
        {
            if (linkDelayRemaining > 0f)
            {
                linkDelayRemaining -= Time.deltaTime;
                return;
            }

            TriggerMove(move);
            return;
        }

        // 石肤招式由 StoneGolemDefend 自行管理。
        if (move.Type == MoveType.Defend)
            return;

        PollDamageWindow(move);

        // 当前招式状态已播完（或保底超时仍未进入状态）：弹出并准备下一招。
        bool deadlinePassed = Time.time >= actionStartDeadline;
        if (!IsPlayingState(move.StateHash) && (attackStateObserved || deadlinePassed))
        {
            moveQueue.RemoveAt(0);
            currentMoveTriggered = false;
            attackStateObserved = false;
            actionStartDeadline = Time.time + definition.ActionStartTimeout;
            linkDelayRemaining = moveQueue.Count > 0 ? definition.ComboLinkDelay : 0f;
        }
    }

    /// <summary>按距离与状态选择招式序列并开始本轮攻击。</summary>
    public override void BeginAttack()
    {
        if (definition == null || animator == null)
        {
            attackActive = false;
            return;
        }

        enemyAnimation?.StopMovement();
        BuildAttackQueue();

        attackActive = moveQueue.Count > 0;
        attackStateObserved = false;
        currentMoveTriggered = false;
        linkDelayRemaining = 0f;
        actionStartDeadline = Time.time + definition.ActionStartTimeout;
    }

    // 依据与目标的距离、冷却和权重构建本轮招式队列。
    private void BuildAttackQueue()
    {
        moveQueue.Clear();
        float distance = GetDistanceToTarget();

        // 玩家连续命中时优先举盾反击姿态。
        if (defend != null && distance <= definition.DefendMaxRange &&
            consecutivePlayerHits >= definition.DefendTriggerHits && defend.IsReady)
        {
            moveQueue.Add(new QueuedMove { Type = MoveType.Defend });
            return;
        }

        // 中距离：跳跃砸地（红光不可格挡）。
        if (distance >= definition.JumpSlamMinRange && distance <= definition.JumpSlamMaxRange &&
            Time.time >= jumpSlamReadyAt && Random.value <= definition.JumpSlamChance)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.JumpSlam,
                StateHash = JumpStateHash,
                DamageMultiplier = definition.JumpSlamDamageMultiplier,
                GuardStamina = 0f,
                Unblockable = true,
                HitWindowStart = 0.62f,
                HitWindowEnd = 0.85f,
                AoERadius = definition.JumpSlamRadius,
                HitOffset = Vector3.zero,
            });
            jumpSlamReadyAt = Time.time + 9f;
            return;
        }

        // 远中距离：震波弹道（黄光）。
        if (distance > definition.AttackRange + 0.5f && distance <= definition.ShockwaveRange &&
            definition.ShockwaveWeight > 0f && Time.time >= shockwaveReadyAt)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.Shockwave,
                StateHash = ShockwaveStateHash,
                DamageMultiplier = definition.ShockwaveDamageMultiplier,
                GuardStamina = definition.ShockwaveGuardStamina,
                Unblockable = false,
                HitWindowStart = 0.55f,
                HitWindowEnd = 0.75f,
            });
            shockwaveReadyAt = Time.time + 6f;
            return;
        }

        // 近身：拳法连段 / 跺地。
        BuildMeleeCombo(distance);
    }

    // 构建近身连段序列。
    private void BuildMeleeCombo(float distance)
    {
        bool inSlamRange = distance <= definition.SlamRadius + definition.SlamHitOffset.magnitude;

        // 跺地优先起手：范围大且破防高。
        if (inSlamRange && definition.SlamWeight > 0f &&
            Random.value * (definition.SlamWeight + definition.PunchWeight) >= definition.PunchWeight)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.Slam,
                StateHash = SlamStateHash,
                DamageMultiplier = definition.SlamDamageMultiplier,
                GuardStamina = definition.SlamGuardStamina,
                Unblockable = false,
                HitWindowStart = 0.55f,
                HitWindowEnd = 0.75f,
                AoERadius = definition.SlamRadius,
                HitOffset = definition.SlamHitOffset,
            });
            if (!definition.EnableComboChains || !phaseTwo && Random.value > definition.ComboChainChance * 0.5f)
                return;
            moveQueue.Add(MakePunchMove());
            return;
        }

        // 拳法连段：单拳或双拳起手，概率追加后续。
        moveQueue.Add(MakePunchMove());

        if (!definition.EnableComboChains || Random.value > definition.ComboChainChance)
            return;

        moveQueue.Add(MakePunchMove());

        // 二阶段解锁第三段追加。
        if (phaseTwo && definition.PhaseTwoExtendedCombo)
            moveQueue.Add(MakeDoublePunchMove());
    }

    // 构建一个拳法招式（按权重选择单拳或双拳）。
    private QueuedMove MakePunchMove()
    {
        bool isDoublePunch = SelectDoublePunch();
        return new QueuedMove
        {
            Type = isDoublePunch ? MoveType.DoublePunch : MoveType.Punch,
            StateHash = isDoublePunch ? DoublePunchStateHash : PunchStateHash,
            DamageMultiplier = isDoublePunch
                ? definition.DoublePunchDamageMultiplier
                : definition.PunchDamageMultiplier,
            GuardStamina = isDoublePunch ? 18f : 12f,
            Unblockable = false,
            HitWindowStart = 0f,
            HitWindowEnd = 0f,
        };
    }

    // 构建一个必为双拳的招式（连段收尾用）。
    private QueuedMove MakeDoublePunchMove()
    {
        return new QueuedMove
        {
            Type = MoveType.DoublePunch,
            StateHash = DoublePunchStateHash,
            DamageMultiplier = definition.DoublePunchDamageMultiplier,
            GuardStamina = 18f,
            Unblockable = false,
            HitWindowStart = 0f,
            HitWindowEnd = 0f,
        };
    }

    // 触发队列头部的招式：动画、预警与冷却。
    private void TriggerMove(QueuedMove move)
    {
        currentMoveTriggered = true;
        attackStateObserved = false;
        actionStartDeadline = Time.time + definition.ActionStartTimeout;

        switch (move.Type)
        {
            case MoveType.Punch:
            case MoveType.DoublePunch:
                AudioManager.Instance?.PlayStoneGolemAttackWindup(transform.position);
                animator.SetTrigger(move.Type == MoveType.DoublePunch ? DoublePunchTriggerHash : PunchTriggerHash);
                break;
            case MoveType.Slam:
                telegraph?.Flash(Color.yellow, 0.7f,
                    GetGroundPoint(transform.TransformPoint(move.HitOffset)), definition.SlamRadius);
                AudioManager.Instance?.PlayStoneGolemAttackWindup(transform.position);
                animator.SetTrigger(SlamTriggerHash);
                break;
            case MoveType.Shockwave:
                telegraph?.Flash(Color.yellow, 0.7f);
                AudioManager.Instance?.PlayStoneGolemAttackWindup(transform.position);
                animator.SetTrigger(ShockwaveTriggerHash);
                break;
            case MoveType.JumpSlam:
                telegraph?.Flash(Color.red, 0.8f,
                    GetGroundPoint(transform.position + transform.forward * 1.5f), definition.JumpSlamRadius);
                AudioManager.Instance?.PlayStoneGolemAttackWindup(transform.position);
                animator.SetTrigger(JumpTriggerHash);
                break;
            case MoveType.Defend:
                if (defend == null || !defend.TryStartDefend())
                {
                    // 石肤不可用则跳过该招式。
                    moveQueue.RemoveAt(0);
                    currentMoveTriggered = false;
                }
                break;
        }
    }

    // 拳法沿用动画事件命中；其余招式在归一化时间窗内结算一次。
    private void PollDamageWindow(QueuedMove move)
    {
        if (move.HitConsumed || move.HitWindowEnd <= 0f)
            return;

        if (!TryGetStateNormalizedTime(move.StateHash, out float normalizedTime))
            return;

        if (normalizedTime >= move.HitWindowStart && normalizedTime <= move.HitWindowEnd)
        {
            move.HitConsumed = true;
            ApplyAreaHit(move);
        }
    }

    // 对 AoE 招式结算一次范围伤害（只命中首个有效目标）。
    private void ApplyAreaHit(QueuedMove move)
    {
        if (enemy == null || enemy.Stats == null || definition == null)
            return;

        if (move.Type == MoveType.Shockwave)
        {
            SpawnShockwave(move);
            return;
        }

        Transform origin = enemy.AttackOrigin;
        Vector3 center = origin.TransformPoint(move.HitOffset);
        AudioManager.Instance?.PlayStoneGolemImpact(center);
        SpawnImpactPresentation(center, move);

        int count = Physics.OverlapSphereNonAlloc(
            center,
            move.AoERadius,
            hitBuffer,
            definition.DamageableMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = hitBuffer[index];
            hitBuffer[index] = null;
            if (hit == null || hit.transform.root == enemy.transform.root)
                continue;

            CharacterStats damageable = hit.GetComponentInParent<CharacterStats>();
            if (damageable == null || !damageable.IsAlive)
                continue;

            damageable.TakeDamage(new DamageRequest(
                enemy.Stats.Attack,
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                enemy,
                move.GuardStamina,
                move.Unblockable,
                true));
            return;
        }
    }

    // 砸地命中的视觉表现：碎石飞溅 + 烟尘 + 地面冲击波（跳砸用红色且更大量）。
    private void SpawnImpactPresentation(Vector3 center, QueuedMove move)
    {
        bool isJumpSlam = move.Type == MoveType.JumpSlam;
        Color waveColor = isJumpSlam ? Color.red : Color.yellow;
        float debrisMultiplier = isJumpSlam ? 1.5f : 1f;
        Vector3 groundPoint = GetGroundPoint(center);

        if (definition.DebrisPrefabs != null && definition.DebrisPrefabs.Length > 0)
        {
            int debrisCount = Mathf.Max(1, Mathf.RoundToInt(definition.DebrisCount * debrisMultiplier));
            RockDebrisBurst.Burst(definition.DebrisPrefabs, groundPoint, debrisCount, isJumpSlam ? 1.2f : 1f);
        }

        if (definition.ImpactDustPrefab != null)
        {
            OneShotParticle.Play(
                definition.ImpactDustPrefab,
                groundPoint + Vector3.up * 0.3f,
                Mathf.Max(1f, move.AoERadius * 0.5f),
                2f);
        }

        if (definition.ImpactWavePrefab != null)
        {
            OneShotParticle.Play(
                definition.ImpactWavePrefab,
                groundPoint + Vector3.up * 0.05f,
                Mathf.Max(1f, move.AoERadius * 1.2f),
                1.2f,
                waveColor);
        }

        // 命中瞬间再用招式颜色闪一次预警灯，强化"这一下打出去了"的反馈。
        telegraph?.Flash(waveColor, 0.35f);
    }

    // 生成震波投射物：旋转的石头 + 黄色飞行光 + 命中爆碎石。
    private void SpawnShockwave(QueuedMove move)
    {
        Transform origin = enemy.AttackOrigin;
        Vector3 spawnPosition = origin.TransformPoint(definition.ShockwaveSpawnOffset);
        Vector3 direction = transform.forward;
        if (enemy.Target != null)
        {
            Vector3 toTarget = enemy.Target.position - spawnPosition;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                direction = toTarget.normalized;
        }

        GameObject waveObject = new GameObject("StoneShockwave");
        waveObject.transform.position = spawnPosition;
        waveObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        SphereCollider collider = waveObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        if (definition.ShockwaveVisualPrefab != null)
        {
            GameObject visual = Instantiate(definition.ShockwaveVisualPrefab, waveObject.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * definition.ShockwaveVisualScale;
        }

        // 石头挂黄色点光：昏暗地牢里飞行轨迹一眼可见（寿命 1.2s，开销可忽略）。
        GameObject glowObject = new GameObject("RockGlow");
        glowObject.transform.SetParent(waveObject.transform, false);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.75f, 0.3f);
        glow.range = 6f;
        glow.intensity = 3.5f;
        glow.shadows = LightShadows.None;

        MinimalProjectile projectile = waveObject.AddComponent<MinimalProjectile>();
        projectile.Impacted += OnShockwaveImpact;
        projectile.Launch(
            direction,
            enemy.Stats,
            new DamageRequest(
                enemy.Stats.Attack,
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                enemy,
                move.GuardStamina,
                false,
                true),
            definition.ShockwaveSpeed,
            definition.ShockwaveLifetime,
            definition.ShockwaveSpinSpeed);
        waveObject.transform.SetParent(null);
    }

    // 震波石头命中/消失：在消失点爆出碎石 + 地面波。
    private void OnShockwaveImpact(Vector3 impactPoint)
    {
        if (definition == null)
            return;

        Vector3 groundPoint = GetGroundPoint(impactPoint);

        if (definition.DebrisPrefabs != null && definition.DebrisPrefabs.Length > 0)
        {
            int debrisCount = Mathf.Max(3, definition.DebrisCount / 2);
            RockDebrisBurst.Burst(definition.DebrisPrefabs, groundPoint, debrisCount, 0.8f);
        }

        if (definition.ImpactWavePrefab != null)
        {
            OneShotParticle.Play(
                definition.ImpactWavePrefab,
                groundPoint + Vector3.up * 0.05f,
                1.2f,
                1f,
                Color.yellow);
        }
    }

    // 拳法动画事件：按当前队列头部招式结算近战伤害。
    private void OnAttackHitFrame()
    {
        if (!attackActive || definition == null || enemy == null || enemy.Stats == null)
            return;
        if (moveQueue.Count == 0 || currentMoveTriggered == false)
            return;

        QueuedMove move = moveQueue[0];
        if (move.Type != MoveType.Punch && move.Type != MoveType.DoublePunch)
            return;
        if (move.HitConsumed)
            return;

        move.HitConsumed = true;
        AudioManager.Instance?.PlayStoneGolemImpact(transform.position);

        Transform origin = enemy.AttackOrigin;
        Vector3 center = origin.TransformPoint(definition.MeleeHitOffset);
        int count = Physics.OverlapSphereNonAlloc(
            center,
            definition.MeleeHitRadius,
            hitBuffer,
            definition.DamageableMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = hitBuffer[index];
            hitBuffer[index] = null;
            if (hit == null || hit.transform.root == enemy.transform.root)
                continue;

            CharacterStats damageable = hit.GetComponentInParent<CharacterStats>();
            if (damageable == null || !damageable.IsAlive)
                continue;

            damageable.TakeDamage(new DamageRequest(
                enemy.Stats.Attack,
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                enemy,
                move.GuardStamina,
                false,
                true));
            return;
        }
    }

    // 记录玩家命中次数，达到阈值后石头人倾向于举盾。
    private void OnPlayerHitGolem(DamageResult result)
    {
        if (!result.WasApplied || result.WasBlocked)
            return;

        if (Time.time - lastPlayerHitTime < 3f)
            consecutivePlayerHits++;
        else
            consecutivePlayerHits = 1;
        lastPlayerHitTime = Time.time;
    }

    // 检查生命值比例并进入二阶段。
    private void UpdatePhaseTwo()
    {
        if (phaseTwo || enemy == null || enemy.Stats == null || definition == null)
            return;

        float healthFraction = enemy.Stats.CurrentHealth / Mathf.Max(1f, enemy.Stats.MaxHealth);
        if (healthFraction <= definition.PhaseTwoHealthThreshold)
        {
            phaseTwo = true;
            enemy.SpeedMultiplier = definition.PhaseTwoSpeedScale;
            telegraph?.Flash(Color.red, 1.2f);
        }
    }

    // 按权重决定单拳或双拳。
    private bool SelectDoublePunch()
    {
        float totalWeight = definition.PunchWeight + definition.DoublePunchWeight;
        return totalWeight > Mathf.Epsilon && Random.value * totalWeight >= definition.PunchWeight;
    }

    // 返回与目标的水平距离。
    private float GetDistanceToTarget()
    {
        if (enemy == null || enemy.Target == null)
            return float.MaxValue;

        Vector3 delta = enemy.Target.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    // 返回指定点的地面高度（射线向下探测），失败时返回原点高度。
    private Vector3 GetGroundPoint(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 1.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0,
            QueryTriggerInteraction.Ignore))
            return hit.point;
        return position;
    }

    // 判断当前 Animator 状态（或过渡目标）是否为队列头部招式的状态。
    private bool IsPlayingCurrentMoveState()
    {
        if (moveQueue.Count == 0)
            return false;
        return IsPlayingState(moveQueue[0].StateHash);
    }

    // 判断当前状态或正在切入的状态是否为指定短名称，并输出归一化时间。
    private bool TryGetStateNormalizedTime(int stateHash, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash)
        {
            normalizedTime = current.normalizedTime % 1f;
            return true;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash == stateHash)
            {
                normalizedTime = 0f;
                return true;
            }
        }

        return false;
    }

    // 判断当前 Animator 或正在切入的状态是否为指定短名称。
    private bool IsPlayingState(int stateHash)
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash)
            return true;

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
    }

    // 在 Scene 视图中预览近战与跺地的命中范围。
    private void OnDrawGizmosSelected()
    {
        if (definition == null)
            return;

        Transform origin = enemy != null ? enemy.AttackOrigin : transform;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin.TransformPoint(definition.MeleeHitOffset), definition.MeleeHitRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.TransformPoint(definition.SlamHitOffset), definition.SlamRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin.position, definition.JumpSlamRadius);
    }
}
