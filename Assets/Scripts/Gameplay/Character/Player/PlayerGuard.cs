using System;
using UnityEngine;

/// <summary>
/// 玩家防御与精准防御（弹反）组件。
/// 按住 Block 进入防御：正面攻击被格挡为穿透伤害并消耗体力，
/// 体力不足触发破防硬直；命中前 perfectGuardWindow 秒内按下格挡判定为精准防御，
/// 化解攻击并使可弹反的敌人进入硬直。
/// 通过 PlayerStats.IncomingDamageHandler 接入统一伤害管线。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerAnimation))]
public sealed class PlayerGuard : MonoBehaviour, IIncomingDamageHandler
{
    // 判定精准防御的时间窗口：命中时距按下格挡不超过该值即为弹反，单位为秒。
    [SerializeField, Range(0.05f, 0.5f)] private float perfectGuardWindow = 0.2f;
    // 普通格挡吸收的伤害比例；0.8 表示只承受 20% 穿透伤害。
    [SerializeField, Range(0f, 1f)] private float blockAbsorption = 0.8f;
    // 格挡生效的最大正面扇形半角，单位为度；来自身后的攻击无法格挡。
    [SerializeField, Range(30f, 180f)] private float frontalArcHalfAngle = 60f;
    // 精准防御后玩家攻击的增伤倍率窗口时长，单位为秒。
    [SerializeField, Range(0f, 3f)] private float riposteWindow = 1f;
    // 精准防御增伤倍率。
    [SerializeField, Min(1f)] private float riposteDamageMultiplier = 1.5f;
    // 破防硬直持续时间，单位为秒。
    [SerializeField, Range(0.5f, 3f)] private float guardBreakStunDuration = 1.2f;
    // 格挡期间的移动速度倍率。
    [SerializeField, Range(0.1f, 1f)] private float guardMoveSpeedMultiplier = 0.4f;
    // 精准防御命中特效预制体；为空时跳过特效。
    [SerializeField] private GameObject perfectGuardEffectPrefab;
    // 特效生成位置相对角色的高度偏移，单位为米。
    [SerializeField, Min(0f)] private float effectHeight = 1.2f;

    // 提供格挡按键与移动轴输入的组件。
    [SerializeField] private InputManager input;
    // 提供存活状态、体力消耗与免伤状态的玩家数值组件。
    [SerializeField] private PlayerStats playerStats;
    // 负责触发防御动画的组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 提供攻击中限制条件的战斗组件。
    [SerializeField] private PlayerCombat playerCombat;
    // 提供翻滚状态的组件。
    [SerializeField] private PlayerRoll playerRoll;
    // 提供输入开关状态与装备状态的控制器。
    [SerializeField] private PlayerController playerController;
    // 提供弹反与破防顿帧反馈的组件。
    [SerializeField] private CombatFeedback combatFeedback;

    // 最近一次按下格挡键的游戏时间，用于精准防御判定。
    private float lastBlockPressTime = float.NegativeInfinity;
    // 破防硬直结束的游戏时间。
    private float guardBreakEndsAt = float.NegativeInfinity;
    // 精准防御增伤窗口结束的游戏时间。
    private float riposteEndsAt = float.NegativeInfinity;
    // 当前是否处于防御姿态。
    private bool guarding;

    /// <summary>当前是否处于防御姿态（按下格挡且未被破防）。</summary>
    public bool IsGuarding => guarding && !IsStunned;
    /// <summary>当前是否处于破防硬直。</summary>
    public bool IsStunned => Time.time < guardBreakEndsAt;
    /// <summary>格挡期间传给移动控制器的速度倍率。</summary>
    public float GuardMoveSpeedMultiplier => guardMoveSpeedMultiplier;
    /// <summary>当前攻击应使用的增伤倍率（精准防御后的惩罚窗口内提升）。</summary>
    public float RiposteDamageMultiplier => Time.time < riposteEndsAt ? riposteDamageMultiplier : 1f;

    /// <summary>精准防御成功时触发，供特效与镜头反馈订阅。</summary>
    public event Action PerfectGuardPerformed;
    /// <summary>普通格挡吸收一次攻击时触发。</summary>
    public event Action<DamageResult> BlockedAttack;
    /// <summary>破防（格挡体力不足）时触发。</summary>
    public event Action GuardBroken;

    // 缓存协作组件引用。
    private void Awake()
    {
        input = input != null ? input : GetComponent<InputManager>();
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        playerCombat = playerCombat != null ? playerCombat : GetComponent<PlayerCombat>();
        playerRoll = playerRoll != null ? playerRoll : GetComponent<PlayerRoll>();
        playerController = playerController != null ? playerController : GetComponent<PlayerController>();
        combatFeedback = combatFeedback != null ? combatFeedback : GetComponent<CombatFeedback>();
    }

    // 启用时注册输入事件并接入伤害管线。
    private void OnEnable()
    {
        if (input != null)
        {
            input.BlockPressed += OnBlockPressed;
            input.BlockReleased += OnBlockReleased;
        }

        if (playerStats != null)
        {
            playerStats.IncomingDamageHandler = this;
            playerStats.Died += OnDied;
        }
    }

    // 禁用时解除注册并退出防御姿态。
    private void OnDisable()
    {
        if (input != null)
        {
            input.BlockPressed -= OnBlockPressed;
            input.BlockReleased -= OnBlockReleased;
        }

        if (playerStats != null)
        {
            if (ReferenceEquals(playerStats.IncomingDamageHandler, this))
                playerStats.IncomingDamageHandler = null;
            playerStats.Died -= OnDied;
        }

        CancelGuardImmediate();
    }

    // 玩家死亡时立即解除防御，避免死亡动画与举盾动画叠加。
    private void OnDied(CharacterStats stats)
    {
        CancelGuardImmediate();
    }

    // 每帧处理松键兜底与破防硬直结束。
    private void Update()
    {
        if (guarding && input != null && !input.BlockHeld)
            EndGuard();

        if (IsStunned && Time.time >= guardBreakEndsAt)
            guardBreakEndsAt = float.NegativeInfinity;
    }

    // 响应格挡按下，进入防御姿态。
    private void OnBlockPressed()
    {
        TryStartGuard();
    }

    // 响应格挡松开，退出防御姿态。
    private void OnBlockReleased()
    {
        if (guarding)
            EndGuard();
    }

    // 检查战斗限制条件后进入防御。
    private void TryStartGuard()
    {
        bool inputAllowed = playerController == null || playerController.IsGameplayInputEnabled;
        if (!inputAllowed || guarding || IsStunned ||
            (playerStats != null && !playerStats.IsAlive) ||
            (playerAnimation != null && !playerAnimation.IsEquipped) ||
            (playerRoll != null && playerRoll.IsRolling) ||
            (playerCombat != null && playerCombat.IsAttacking) ||
            (playerAnimation != null && playerAnimation.IsPlayingHurt()))
        {
            return;
        }

        guarding = true;
        lastBlockPressTime = Time.time;
        playerAnimation?.StartGuard();
    }

    // 松开格挡或被翻滚/受击打断时结束防御。
    private void EndGuard()
    {
        if (!guarding)
            return;

        guarding = false;
        playerAnimation?.EndGuard();
    }

    /// <summary>被翻滚、受击等系统无条件打断防御时调用，不播放收势动画。</summary>
    public void CancelGuardImmediate()
    {
        guarding = false;
    }

    // 处理一次入站伤害：弹反、格挡或放行。
    public DamageResult? HandleDamage(DamageRequest request, CharacterStats target)
    {
        if (!IsGuarding || playerStats == null)
            return null;

        if (request.IsUnblockable || !IsFrontalAttack(request))
            return null;

        float rawDamage = Mathf.Max(0f, request.AttackPower) * Mathf.Max(0f, request.Multiplier);

        // 精准防御：命中时距按下格挡不超过窗口即弹反。
        bool withinPerfectWindow = Time.time - lastBlockPressTime <= perfectGuardWindow;
        if (request.IsParryable && withinPerfectWindow)
        {
            riposteEndsAt = Time.time + riposteWindow;
            playerAnimation?.PlayPerfectGuard();
            SpawnParryEffect();
            NotifyAttackerParried(request);
            combatFeedback?.PlayPerfectGuardFeedback();
            PerfectGuardPerformed?.Invoke();
            return DamageResult.Parried(rawDamage);
        }

        // 普通格挡：先支付破防体力，不足则破防并放行完整伤害。
        if (request.GuardStamina > 0f && !playerStats.TrySpendStamina(request.GuardStamina))
        {
            BreakGuard();
            return null;
        }

        // 穿透伤害 = 标准公式结算值的 (1-吸收率)，最少 1 点且不致死（至少保留 1 点生命）。
        DamageResult mitigated = DamageSystem.Calculate(request, target.Defense);
        int chipDamage = Mathf.Max(1, Mathf.CeilToInt(mitigated.FinalDamage * (1f - blockAbsorption)));
        float remainingHealth = Mathf.Max(1f, playerStats.CurrentHealth - chipDamage);
        int appliedDamage = Mathf.Max(0, Mathf.RoundToInt(playerStats.CurrentHealth - remainingHealth));
        playerStats.ApplySettledDamage(appliedDamage);

        DamageResult blocked = new DamageResult(rawDamage, appliedDamage, true, false, true, false);
        BlockedAttack?.Invoke(blocked);
        return blocked;
    }

    // 破防：承受完整伤害并进入硬直，防御姿态立即解除。
    private void BreakGuard()
    {
        guarding = false;
        guardBreakEndsAt = Time.time + guardBreakStunDuration;
        playerAnimation?.PlayGuardBreak();
        combatFeedback?.PlayGuardBreakFeedback();
        GuardBroken?.Invoke();
    }

    // 判断攻击来源是否位于正面扇形内；来源未知时按可格挡处理。
    private bool IsFrontalAttack(DamageRequest request)
    {
        Vector3 sourcePosition = GetSourcePosition(request.Source);
        if (sourcePosition.sqrMagnitude < 0f)
            return true;

        Vector3 toSource = sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(transform.forward, toSource) <= frontalArcHalfAngle;
    }

    // 从伤害请求来源解析世界坐标；无法解析时返回负向量标记未知。
    private static Vector3 GetSourcePosition(object source)
    {
        switch (source)
        {
            case Component component:
                return component.transform.position;
            case GameObject gameObject:
                return gameObject.transform.position;
            default:
                return new Vector3(0f, -1f, 0f);
        }
    }

    // 通知可弹反的攻击来源进入硬直。
    private void NotifyAttackerParried(DamageRequest request)
    {
        if (request.Source is not Component attackerComponent)
            return;

        IParriable parriable = attackerComponent.GetComponentInParent<IParriable>();
        parriable?.OnParried(transform.position);
    }

    // 在角色与攻击者之间的位置生成精准防御特效。
    private void SpawnParryEffect()
    {
        if (perfectGuardEffectPrefab == null)
            return;

        Vector3 spawnPosition = transform.position + Vector3.up * effectHeight;
        GameObject spawned = Instantiate(perfectGuardEffectPrefab, spawnPosition, Quaternion.identity);
        if (spawned != null && spawned.TryGetComponent(out ParticleSystem particles))
            Destroy(spawned, particles.main.duration + 1f);
        else
            Destroy(spawned, 1.5f);
    }
}
