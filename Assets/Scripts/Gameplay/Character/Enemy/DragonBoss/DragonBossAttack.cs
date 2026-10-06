using UnityEngine;

/// <summary>
/// 龙 BOSS 的攻击行为：撕咬（近身）、火球（远程弹道）、地火 AoE（黄光预判技）与
/// 龙息（红光不可格挡，二阶段解锁）。全部招式以归一化时间窗判定伤害。
/// 生命值低于阈值进入二阶段：间隔缩短、移速提升、解锁龙息与双咬连段。
/// </summary>
[DisallowMultipleComponent]
public sealed class DragonBossAttack : EnemyAttackBehaviour
{
    // 龙 BOSS 攻击配置资产。
    [SerializeField] private DragonBossAttackDefinition definition;
    // 龙 BOSS 动画适配组件。
    [SerializeField] private DragonBossAnimation animationBehaviour;
    // BOSS Animator，用于轮询攻击状态。
    [SerializeField] private Animator animator;
    // 攻击预警组件。
    [SerializeField] private AttackTelegraph telegraph;

    // 撕咬状态短名称哈希值。
    private static readonly int BiteStateHash = Animator.StringToHash("Bite");
    private static readonly int FireballStateHash = Animator.StringToHash("Fireball");
    private static readonly int SpellAoEStateHash = Animator.StringToHash("SpellAoE");
    private static readonly int FireBreathStateHash = Animator.StringToHash("FireBreath");

    // 本轮攻击的招式队列。
    private readonly System.Collections.Generic.List<QueuedMove> moveQueue =
        new System.Collections.Generic.List<QueuedMove>();
    // 队列相邻招式间的停顿剩余时间。
    private float linkDelayRemaining;
    // 队列头部招式是否已触发。
    private bool currentMoveTriggered;
    // 当前攻击轮次是否激活。
    private bool attackActive;
    // 未进入攻击状态时的保底截止时间。
    private float actionStartDeadline;
    // 是否已观察到攻击状态。
    private bool attackStateObserved;
    // 是否处于二阶段。
    private bool phaseTwo;
    // 火球冷却截止时间。
    private float fireballReadyAt = float.NegativeInfinity;
    // 地火冷却截止时间。
    private float spellAoEReadyAt = float.NegativeInfinity;
    // 龙息冷却截止时间。
    private float fireBreathReadyAt = float.NegativeInfinity;
    // 地火落点的世界坐标（施法瞬间锁定玩家位置）。
    private Vector3 spellAoETargetPosition;

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
        public bool HitConsumed;
    }

    // 龙 BOSS 招式类型。
    private enum MoveType
    {
        Bite,
        Fireball,
        SpellAoE,
        FireBreath,
    }

    /// <summary>允许发起攻击的最大水平距离（撕咬范围与火球范围取大者）。</summary>
    public override float AttackRange
    {
        get
        {
            if (definition == null)
                return 0f;
            return Time.time >= fireballReadyAt ? definition.FireballRange : definition.AttackRange;
        }
    }

    /// <summary>本轮攻击结束后的等待时间（二阶段缩短）。</summary>
    public override float GetNextAttackDelay()
    {
        if (definition == null)
            return 0f;

        float interval = Random.Range(definition.AttackIntervalMin, definition.AttackIntervalMax);
        return phaseTwo ? interval * definition.PhaseTwoIntervalScale : interval;
    }

    /// <summary>当前攻击是否结束。</summary>
    public override bool IsAttackFinished
    {
        get
        {
            if (!attackActive || animator == null || definition == null)
                return true;

            if (moveQueue.Count == 0)
                return true;

            bool isPlayingMove = IsPlayingState(moveQueue[0].StateHash);
            if (isPlayingMove)
            {
                attackStateObserved = true;
                return false;
            }

            return attackStateObserved || Time.time >= actionStartDeadline;
        }
    }

    // 缓存组件引用。
    private void Awake()
    {
        animationBehaviour = animationBehaviour != null
            ? animationBehaviour
            : GetComponentInChildren<DragonBossAnimation>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        telegraph = telegraph != null ? telegraph : GetComponent<AttackTelegraph>();
    }

    // 订阅宿主生命事件以检测二阶段。
    private void OnEnable()
    {
        if (animationBehaviour != null)
            animationBehaviour.AttackHitFrame += OnAnimationHitFrame;
        if (enemyStats == null)
            enemyStats = GetComponentInParent<CharacterStats>();
        if (enemyStats != null)
            enemyStats.ResourceChanged += OnResourceChanged;
    }

    // 取消订阅。
    private void OnDisable()
    {
        if (animationBehaviour != null)
            animationBehaviour.AttackHitFrame -= OnAnimationHitFrame;
        if (enemyStats != null)
            enemyStats.ResourceChanged -= OnResourceChanged;
    }

    // 宿主数值组件缓存。
    private CharacterStats enemyStats;

    // 每帧推进招式队列与二阶段检测。
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

        // 地火落点判定独立于动画时窗（预警延迟决定时机）。
        PollSpellAoEImpact(move);
        PollDamageWindow(move);

        bool deadlinePassed = Time.time >= actionStartDeadline;
        if (!IsPlayingState(move.StateHash) && (attackStateObserved || deadlinePassed))
        {
            moveQueue.RemoveAt(0);
            currentMoveTriggered = false;
            attackStateObserved = false;
            actionStartDeadline = Time.time + definition.ActionStartTimeout;
            linkDelayRemaining = moveQueue.Count > 0 ? 0.25f : 0f;
        }
    }

    /// <summary>按距离、冷却与阶段选择招式并开始本轮攻击。</summary>
    public override void BeginAttack()
    {
        if (definition == null || animator == null)
        {
            attackActive = false;
            return;
        }

        animationBehaviour?.StopMovement();
        BuildAttackQueue();

        attackActive = moveQueue.Count > 0;
        attackStateObserved = false;
        currentMoveTriggered = false;
        linkDelayRemaining = 0f;
        actionStartDeadline = Time.time + definition.ActionStartTimeout;
    }

    // 构建招式队列。
    private void BuildAttackQueue()
    {
        moveQueue.Clear();
        float distance = GetDistanceToTarget();

        // 红光龙息：中距离范围技，二阶段解锁。
        if (phaseTwo && definition.PhaseTwoUnlocksBreath && distance <= definition.FireBreathRange &&
            Time.time >= fireBreathReadyAt)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.FireBreath,
                StateHash = FireBreathStateHash,
                DamageMultiplier = definition.FireBreathDamageMultiplier,
                GuardStamina = 0f,
                Unblockable = true,
                HitWindowStart = 0.5f,
                HitWindowEnd = 0.7f,
            });
            fireBreathReadyAt = Time.time + definition.FireBreathCooldown;
            return;
        }

        // 近身：撕咬，二阶段概率双咬。
        if (distance <= definition.AttackRange)
        {
            moveQueue.Add(MakeBite());
            if (phaseTwo && Random.value < 0.4f)
                moveQueue.Add(MakeBite());
            return;
        }

        // 中距离：地火 AoE（预判走位）。
        if (distance >= definition.SpellAoERange.x && distance <= definition.SpellAoERange.y &&
            Time.time >= spellAoEReadyAt)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.SpellAoE,
                StateHash = SpellAoEStateHash,
                DamageMultiplier = definition.SpellAoEDamageMultiplier,
                GuardStamina = definition.SpellAoEGuardStamina,
                Unblockable = false,
                HitWindowStart = 0f,
                HitWindowEnd = 0f,
            });
            spellAoEReadyAt = Time.time + definition.SpellAoECooldown;
            return;
        }

        // 远距离：火球弹道。
        if (distance <= definition.FireballRange && Time.time >= fireballReadyAt)
        {
            moveQueue.Add(new QueuedMove
            {
                Type = MoveType.Fireball,
                StateHash = FireballStateHash,
                DamageMultiplier = definition.FireballDamageMultiplier,
                GuardStamina = definition.FireballGuardStamina,
                Unblockable = false,
                HitWindowStart = 0.5f,
                HitWindowEnd = 0.7f,
            });
            fireballReadyAt = Time.time + definition.FireballCooldown;
        }
    }

    // 构建撕咬招式。
    private QueuedMove MakeBite()
    {
        return new QueuedMove
        {
            Type = MoveType.Bite,
            StateHash = BiteStateHash,
            DamageMultiplier = definition.BiteDamageMultiplier,
            GuardStamina = definition.BiteGuardStamina,
            Unblockable = false,
            HitWindowStart = definition.BiteHitWindowStart,
            HitWindowEnd = definition.BiteHitWindowEnd,
        };
    }

    // 触发队列头部招式：动画与预警。
    private void TriggerMove(QueuedMove move)
    {
        currentMoveTriggered = true;
        attackStateObserved = false;
        actionStartDeadline = Time.time + definition.ActionStartTimeout;

        switch (move.Type)
        {
            case MoveType.Bite:
                animationBehaviour?.PlayBite();
                break;
            case MoveType.Fireball:
                animationBehaviour?.PlayFireball();
                break;
            case MoveType.SpellAoE:
                // 施法瞬间锁定玩家当前位置作为落点并展示预警圈。
                Transform castTarget = GetTargetTransform();
                spellAoETargetPosition = castTarget != null
                    ? castTarget.position
                    : transform.position + transform.forward * 5f;
                telegraph?.Flash(Color.yellow, definition.SpellAoETelegraphDelay,
                    GetGroundPoint(spellAoETargetPosition), definition.SpellAoERadius);
                animationBehaviour?.PlaySpellAoE();
                break;
            case MoveType.FireBreath:
                telegraph?.Flash(Color.red, 0.9f,
                    GetGroundPoint(transform.position + transform.forward * 3f),
                    definition.FireBreathRange * 0.4f);
                BreathFireVfx();
                animationBehaviour?.PlayFireBreath();
                break;
        }
    }

    // 龙息喷射表现：嘴部朝目标方向喷出素材包自带的火焰粒子流。
    private void BreathFireVfx()
    {
        if (definition.FireBreathVfxPrefab == null)
            return;

        Vector3 mouth = transform.TransformPoint(definition.FireballSpawnOffset);
        Vector3 direction = transform.forward;
        Transform target = GetTargetTransform();
        if (target != null)
        {
            Vector3 toTarget = target.position + Vector3.up * 0.8f - mouth;
            if (toTarget.sqrMagnitude > 0.0001f)
                direction = toTarget.normalized;
        }

        // 喷射流模式：保留持续发射率，跟随命中窗时长后销毁。
        OneShotParticle.Play(
            definition.FireBreathVfxPrefab,
            mouth,
            2f,
            definition.FireBreathRange / 8f + 0.7f,
            null,
            Quaternion.LookRotation(direction, Vector3.up),
            convertToBurst: false);
    }

    // 在归一化时间窗内结算一次伤害。
    private void PollDamageWindow(QueuedMove move)
    {
        if (move.HitConsumed || move.HitWindowEnd <= 0f)
            return;

        if (!TryGetStateNormalizedTime(move.StateHash, out float normalizedTime))
            return;

        if (normalizedTime >= move.HitWindowStart && normalizedTime <= move.HitWindowEnd)
        {
            move.HitConsumed = true;
            ApplyMoveDamage(move);
        }
    }

    // 地火 AoE：动画播到中段后延迟落下，命中预警圈内的玩家。
    private void PollSpellAoEImpact(QueuedMove move)
    {
        if (move.Type != MoveType.SpellAoE || move.HitConsumed)
            return;

        if (!TryGetStateNormalizedTime(move.StateHash, out float normalizedTime))
            return;

        if (normalizedTime < 0.4f)
            return;

        // 以预警延迟决定落下时间；简化为进入中段后按延迟计时。
        move.HitConsumed = true;
        StartCoroutine(SpellAoEImpactRoutine(move));
    }

    // 地火落地协程：延迟后对落点范围结算，并播放冲击波与烟尘表现。
    private System.Collections.IEnumerator SpellAoEImpactRoutine(QueuedMove move)
    {
        yield return new WaitForSeconds(definition.SpellAoETelegraphDelay);

        Vector3 center = GetGroundPoint(spellAoETargetPosition);
        SpawnSpellAoEImpactPresentation(center);

        Collider[] hits = Physics.OverlapSphere(
            center,
            definition.SpellAoERadius,
            definition.DamageableMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hits.Length; index++)
        {
            CharacterStats target = hits[index].GetComponentInParent<CharacterStats>();
            if (target == null || !target.IsAlive)
                continue;

            target.TakeDamage(new DamageRequest(
                GetAttackPower(),
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                gameObject,
                move.GuardStamina,
                false,
                true));
        }
    }

    // 地火落地的视觉表现：橙色冲击波 + 烟尘。
    private void SpawnSpellAoEImpactPresentation(Vector3 groundPoint)
    {
        if (definition.SpellAoEWavePrefab != null)
        {
            OneShotParticle.Play(
                definition.SpellAoEWavePrefab,
                groundPoint + Vector3.up * 0.05f,
                Mathf.Max(1f, definition.SpellAoERadius * 1.2f),
                1.2f,
                new Color(1f, 0.5f, 0.1f));
        }

        if (definition.SpellAoEDustPrefab != null)
        {
            OneShotParticle.Play(
                definition.SpellAoEDustPrefab,
                groundPoint + Vector3.up * 0.3f,
                Mathf.Max(1f, definition.SpellAoERadius * 0.5f),
                2f,
                new Color(1f, 0.6f, 0.25f));
        }
    }

    // 按招式类型结算伤害。
    private void ApplyMoveDamage(QueuedMove move)
    {
        switch (move.Type)
        {
            case MoveType.Bite:
                if (ApplySphereHit(move, definition.BiteHitOffset, definition.BiteRadius))
                {
                    // 命中反馈：嘴前红色斩击弧 + 方块粒子爆。
                    Vector3 mouth = transform.TransformPoint(definition.BiteHitOffset * 0.7f);
                    SimpleAttackFX.SlashArc(
                        mouth,
                        transform.forward,
                        definition.BiteRadius * 1.6f,
                        new Color(1f, 0.2f, 0.15f, 0.9f));
                    SimpleAttackFX.CubeBurst(
                        mouth + transform.forward * 0.5f,
                        14,
                        new Color(1f, 0.25f, 0.1f),
                        5f);
                }
                break;
            case MoveType.Fireball:
                SpawnFireball(move);
                break;
            case MoveType.FireBreath:
                ApplyBreathCone(move);
                break;
        }
    }

    // 球形范围命中（撕咬）。返回是否命中了目标。
    private bool ApplySphereHit(QueuedMove move, Vector3 offset, float radius)
    {
        Transform origin = transform;
        Vector3 center = origin.TransformPoint(offset);

        Collider[] hits = Physics.OverlapSphere(
            center,
            radius,
            definition.DamageableMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hits.Length; index++)
        {
            CharacterStats target = hits[index].GetComponentInParent<CharacterStats>();
            if (target == null || !target.IsAlive)
                continue;

            target.TakeDamage(new DamageRequest(
                GetAttackPower(),
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                gameObject,
                move.GuardStamina,
                move.Unblockable,
                true));
            return true;
        }

        return false;
    }

    // 生成火球投射物。
    private void SpawnFireball(QueuedMove move)
    {
        Vector3 spawnPosition = transform.TransformPoint(definition.FireballSpawnOffset);
        Vector3 direction = transform.forward;
        Transform target = GetTargetTransform();
        if (target != null)
        {
            Vector3 toTarget = target.position + Vector3.up * 0.8f - spawnPosition;
            if (toTarget.sqrMagnitude > 0.0001f)
                direction = toTarget.normalized;
        }

        GameObject fireballObject = new GameObject("DragonFireball");
        fireballObject.transform.position = spawnPosition;
        fireballObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        SphereCollider collider = fireballObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.6f;

        if (definition.FireballVisualPrefab != null)
        {
            GameObject visual = Instantiate(definition.FireballVisualPrefab, fireballObject.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 1.8f;
        }

        // 火球挂红色点光：昏暗地牢里飞行轨迹一眼可见。
        GameObject glowObject = new GameObject("FireballGlow");
        glowObject.transform.SetParent(fireballObject.transform, false);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.4f, 0.15f);
        glow.range = 5f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        MinimalProjectile projectile = fireballObject.AddComponent<MinimalProjectile>();
        projectile.Impacted += OnFireballImpact;
        projectile.Launch(
            direction,
            GetComponent<CharacterStats>(),
            new DamageRequest(
                GetAttackPower(),
                move.DamageMultiplier,
                AttackType.Skill,
                0,
                gameObject,
                move.GuardStamina,
                false,
                true),
            definition.FireballSpeed,
            definition.FireballLifetime);
    }

    // 火球命中/消失：在消失点播放大号火花爆花 + 方块爆。
    private void OnFireballImpact(Vector3 impactPoint)
    {
        if (definition.FireballImpactPrefab != null)
        {
            OneShotParticle.Play(
                definition.FireballImpactPrefab,
                impactPoint,
                2.2f,
                1.6f,
                new Color(1f, 0.55f, 0.15f));
        }

        SimpleAttackFX.CubeBurst(impactPoint, 16, new Color(1f, 0.4f, 0.1f), 5f);
    }

    // 龙息锥形判定：距离与朝向双重过滤。
    private void ApplyBreathCone(QueuedMove move)
    {
        Transform target = GetTargetTransform();
        if (target == null)
            return;

        Vector3 toTarget = target.position + Vector3.up * 0.8f - transform.position;
        float distance = toTarget.magnitude;
        if (distance > definition.FireBreathRange)
            return;

        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        if (Vector3.Angle(transform.forward, toTarget) > definition.FireBreathHalfAngle)
            return;

        CharacterStats targetStats = target.GetComponentInParent<CharacterStats>();
        if (targetStats == null || !targetStats.IsAlive)
            return;

        targetStats.TakeDamage(new DamageRequest(
            GetAttackPower(),
            move.DamageMultiplier,
            AttackType.Skill,
            0,
            gameObject,
            0f,
            true,
            false));

        // 命中反馈：目标身上橙色方块爆。
        SimpleAttackFX.CubeBurst(
            targetStats.transform.position + Vector3.up * 0.8f,
            18,
            new Color(1f, 0.35f, 0.1f),
            6f);
    }

    // 动画事件占位（当前伤害全部走时间窗）。
    private void OnAnimationHitFrame()
    {
    }

    // 二阶段检测：生命比例低于阈值后强化。
    private void UpdatePhaseTwo()
    {
        if (phaseTwo || enemyStats == null || definition == null)
            return;

        float healthFraction = enemyStats.CurrentHealth / Mathf.Max(1f, enemyStats.MaxHealth);
        if (healthFraction <= definition.PhaseTwoHealthThreshold)
        {
            phaseTwo = true;
            DragonBoss boss = GetComponentInParent<DragonBoss>();
            if (boss != null)
                boss.SpeedMultiplier = definition.PhaseTwoSpeedScale;
            telegraph?.Flash(Color.red, 1.4f);
        }
    }

    // 响应宿主资源变化（供二阶段立即检测）。
    private void OnResourceChanged(ResourceChangedEvent change)
    {
        if (change.ResourceType == ResourceType.Health && !phaseTwo)
            UpdatePhaseTwo();
    }

    // 攻击力取宿主数值组件的配置值。
    private float GetAttackPower()
    {
        return enemyStats != null ? enemyStats.Attack : 10f;
    }

    // 返回与目标的水平距离。
    private float GetDistanceToTarget()
    {
        Transform target = GetTargetTransform();
        if (target == null)
            return float.MaxValue;

        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    // 返回目标 Transform（从 EnemyBase 宿主读取）。
    private Transform GetTargetTransform()
    {
        DragonBoss boss = GetComponentInParent<DragonBoss>();
        return boss != null && boss.Target != null ? boss.Target : null;
    }

    // 返回指定点的地面高度。
    private Vector3 GetGroundPoint(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return position;
    }

    // 轮询指定状态的归一化时间。
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

    // 判断当前状态或过渡目标是否为指定短名称。
    private bool IsPlayingState(int stateHash)
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash)
            return true;

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
    }
}
