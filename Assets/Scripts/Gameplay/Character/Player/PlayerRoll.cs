using UnityEngine;

/// <summary>
/// 玩家战斗翻滚组件。
/// 它负责判定翻滚输入、消耗体力、维持固定方向位移，并通过动画事件切换无敌帧。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerRoll : MonoBehaviour
{
    // 翻滚方向输入判定使用的最小轴值，主要用于过滤手柄轻微偏移。
    [SerializeField, Range(0f, 1f)] private float directionInputThreshold = 0.15f;
    // 单次翻滚完成的水平距离，单位为米。
    [SerializeField, Min(0f)] private float rollDistance = 4f;
    // 单次翻滚的代码位移持续时间，单位为秒。
    [SerializeField, Min(0.01f)] private float rollDuration = 0.55f;
    // 每次成功翻滚消耗的体力值。
    [SerializeField, Min(0f)] private float staminaCost = 20f;

    // 提供翻滚按键和当前移动轴输入的组件。
    [SerializeField] private InputManager input;
    // 统一执行角色重力和 CharacterController 位移的控制器。
    [SerializeField] private PlayerController playerController;
    // 负责触发四个翻滚动画与转发动画事件的组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 提供体力消耗、存活状态和无敌帧状态的数值组件。
    [SerializeField] private PlayerStats playerStats;
    // 提供攻击中限制条件的战斗组件。
    [SerializeField] private PlayerCombat playerCombat;
    // 提供防御姿态状态；翻滚会立即取消防御。
    [SerializeField] private PlayerGuard playerGuard;

    // 当前翻滚的世界平面方向，开始后保持不变。
    private Vector3 rollDirection;
    // 当前翻滚已执行的游戏时间，单位为秒。
    private float elapsedRollTime;

    /// <summary>当前是否正在由翻滚接管角色水平移动。</summary>
    public bool IsRolling { get; private set; }

    /// <summary>当前翻滚向 PlayerController 提供的平面速度。</summary>
    public Vector3 PlanarVelocity => IsRolling ? rollDirection * (rollDistance / rollDuration) : Vector3.zero;

    // 缓存同一玩家对象上的协作组件。
    private void Awake()
    {
        input = input != null ? input : GetComponent<InputManager>();
        playerController = playerController != null ? playerController : GetComponent<PlayerController>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        playerCombat = playerCombat != null ? playerCombat : GetComponent<PlayerCombat>();
        playerGuard = playerGuard != null ? playerGuard : GetComponent<PlayerGuard>();
    }

    // 启用时订阅翻滚输入和死亡通知。
    private void OnEnable()
    {
        if (input != null)
            input.RollPressed += OnRollPressed;
        if (playerStats != null)
            playerStats.Died += OnDied;
    }

    // 禁用时取消订阅并清理可能残留的无敌状态。
    private void OnDisable()
    {
        if (input != null)
            input.RollPressed -= OnRollPressed;
        if (playerStats != null)
            playerStats.Died -= OnDied;

        CancelRoll();
    }

    // 限制 Inspector 中的持续时间与输入阈值范围。
    private void OnValidate()
    {
        directionInputThreshold = Mathf.Clamp01(directionInputThreshold);
        rollDistance = Mathf.Max(0f, rollDistance);
        rollDuration = Mathf.Max(0.01f, rollDuration);
        staminaCost = Mathf.Max(0f, staminaCost);
    }

    /// <summary>由 PlayerController 在完成本帧位移后推进翻滚计时。</summary>
    public void AdvanceRoll(float deltaTime)
    {
        if (!IsRolling)
            return;

        elapsedRollTime += Mathf.Max(0f, deltaTime);
        if (elapsedRollTime >= rollDuration)
            CancelRoll();
    }

    /// <summary>立即结束翻滚位移并关闭翻滚无敌帧。</summary>
    public void CancelRoll()
    {
        if (!IsRolling && playerStats == null)
            return;

        IsRolling = false;
        elapsedRollTime = 0f;
        rollDirection = Vector3.zero;
        playerStats?.SetRollDamageImmunity(false);
    }

    /// <summary>由翻滚动画事件开启当前翻滚的无敌帧。</summary>
    public void AnimationEvent_RollInvincibilityStart()
    {
        if (IsRolling && playerStats != null && playerStats.IsAlive)
            playerStats.SetRollDamageImmunity(true);
    }

    /// <summary>由翻滚动画事件关闭当前翻滚的无敌帧。</summary>
    public void AnimationEvent_RollInvincibilityEnd()
    {
        playerStats?.SetRollDamageImmunity(false);
    }

    // 响应输入事件并在满足战斗条件时开始一次翻滚。
    private void OnRollPressed()
    {
        TryStartRoll();
    }

    // 检查限制条件、支付体力并初始化本次翻滚数据。
    private void TryStartRoll()
    {
        if (IsRolling || playerController == null || playerStats == null ||
            !playerController.IsGameplayInputEnabled || !playerController.IsEquipped ||
            !playerStats.IsAlive || (playerCombat != null && playerCombat.IsAttacking) ||
            (playerGuard != null && playerGuard.IsStunned) ||
            (playerAnimation != null && playerAnimation.IsPlayingHurt()))
        {
            return;
        }

        if (!playerStats.TrySpendStamina(staminaCost))
            return;

        // 翻滚从防御姿态无缝起手：立即取消防御，保持攻击判定关闭。
        playerGuard?.CancelGuardImmediate();

        PlayerRollDirection direction = ResolveDirection(input != null ? input.Move : Vector2.zero);
        rollDirection = ResolveWorldDirection(direction);
        if (rollDirection.sqrMagnitude < 0.0001f)
            return;

        elapsedRollTime = 0f;
        IsRolling = true;
        AudioManager.Instance?.PlayPlayerRoll();
        playerAnimation?.PlayRoll(direction);
    }

    // 按左、右、前、后优先级将移动轴映射为四方向翻滚。
    private PlayerRollDirection ResolveDirection(Vector2 moveInput)
    {
        if (moveInput.x <= -directionInputThreshold)
            return PlayerRollDirection.Left;
        if (moveInput.x >= directionInputThreshold)
            return PlayerRollDirection.Right;
        if (moveInput.y >= directionInputThreshold)
            return PlayerRollDirection.Forward;
        if (moveInput.y <= -directionInputThreshold)
            return PlayerRollDirection.Back;

        return PlayerRollDirection.Back;
    }

    // 使用现有战斗移动规则计算并固定本次翻滚的相机相对方向。
    private Vector3 ResolveWorldDirection(PlayerRollDirection direction)
    {
        Vector2 inputDirection = direction switch
        {
            PlayerRollDirection.Forward => Vector2.up,
            PlayerRollDirection.Back => Vector2.down,
            PlayerRollDirection.Left => Vector2.left,
            PlayerRollDirection.Right => Vector2.right,
            _ => Vector2.zero,
        };

        Vector3 worldDirection = playerController.GetCombatMotion(inputDirection, false);
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude > 0.0001f)
            return worldDirection.normalized;

        return direction switch
        {
            PlayerRollDirection.Forward => transform.forward,
            PlayerRollDirection.Back => -transform.forward,
            PlayerRollDirection.Left => -transform.right,
            PlayerRollDirection.Right => transform.right,
            _ => transform.forward,
        };
    }

    // 玩家死亡时立即取消翻滚，避免死亡后保留无敌状态。
    private void OnDied(CharacterStats stats)
    {
        CancelRoll();
    }
}

/// <summary>玩家四方向翻滚对应的动画与相机相对移动方向。</summary>
public enum PlayerRollDirection
{
    /// <summary>沿相机前方翻滚。</summary>
    Forward,
    /// <summary>沿相机后方翻滚。</summary>
    Back,
    /// <summary>沿相机左侧翻滚。</summary>
    Left,
    /// <summary>沿相机右侧翻滚。</summary>
    Right,
}
