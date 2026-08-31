using QFramework;
using UnityEngine;

/// <summary>
/// 基于 Input System 的角色移动控制器。
/// 未持武器时按相机相对方向移动并转向；持武器时保留八方向战斗移动。
///
/// Exploration and combat movement rules:
/// - 未持武器（探索）：相机相对移动，角色平滑面向实际移动方向并播放前进动画
/// - 持武器（战斗）：相机相对横移；仅纯前向输入可奔跑，有锁定目标时始终面朝目标
///
/// 架构说明：IController 默认无 ICanSendEvent 能力（QFramework 分层规则：
/// 表现层只监听事件，事件应由 Model/System 产生）。本控制器广播的
/// EquipmentChangedEvent 属于"实时玩法表现事件"，与业务数据无关，
/// 故显式声明 ICanSendEvent 作为工程折衷。业务数据变更仍须走 Model/Command。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerHitReaction))]
public sealed class PlayerController : MonoBehaviour, IController, ICanSendEvent
{
    [Header("References")]
    // 负责角色碰撞与实际位移的控制器。
    [SerializeField] private CharacterController controller;
    // 提供移动、奔跑和装备等玩家输入。
    [SerializeField] private InputManager input;
    // 驱动移动、装备和攻击动画参数的组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 提供攻击中限制移动所需的战斗状态。
    [SerializeField] private PlayerCombat playerCombat;
    // 提供奔跑体力状态与持续消耗接口的玩家属性组件。
    [SerializeField] private PlayerStats playerStats;

    [Header("Movement")]
    // 探索或战斗慢走时的水平速度，单位为米每秒。
    [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
    // 满足对应奔跑条件时的水平速度，单位为米每秒。
    [SerializeField, Min(0f)] private float runSpeed = 5.5f;
    // 探索奔跑转向速度，单位为度每秒。
    [SerializeField, Min(0f)] private float runTurnSpeed = 360f;
    // 判定前向输入可进入奔跑的最小幅度。
    [SerializeField, Range(0f, 1f)] private float runForwardThreshold = 0.15f;
    // 战斗奔跑允许的最大横向输入幅度，用于手柄死区。
    [SerializeField, Range(0f, 1f)] private float combatRunLateralThreshold = 0.1f;
    // 角色下落使用的重力加速度，单位为米每二次方秒。
    [SerializeField] private float gravity = -20f;
    // 战斗状态面向相机或锁定目标的平滑插值速度。
    [SerializeField] private float combatTurnSpeed = 12f;

    // 当前竖直速度，用于 CharacterController 的重力模拟。
    private float verticalVelocity;
    // 是否允许读取输入并响应角色控制。
    private bool inputEnabled = true;
    // 当前是否处于持武器的战斗移动状态。
    private bool isEquipped;
    // 当前帧是否实际以奔跑速度移动。
    private bool isRunning;
    // 当前锁定目标，用于战斗状态下的朝向计算。
    private Transform lockTarget;

    // 当前探索模式实际水平移动方向，供镜头或特效系统读取。
    public Vector3 ExplorationMoveDirection { get; private set; }
    // 当前是否在未持武器的探索模式中实际移动。
    public bool IsExplorationMoving { get; private set; }
    // 当前是否已持武器。
    public bool IsEquipped => isEquipped;
    // 当前是否在探索模式中实际以奔跑速度移动。
    public bool IsExplorationRunning => CanMove && !isEquipped && isRunning;

    // 死亡或攻击期间禁止角色移动。
    private bool CanMove => (playerStats == null || playerStats.IsAlive) &&
        (playerCombat == null || !playerCombat.IsAttacking);
    // 探索模式下按住奔跑键且存在有效方向输入。
    private bool HasExplorationRunInput => inputEnabled && input != null && input.SprintHeld &&
        input.Move.sqrMagnitude > runForwardThreshold * runForwardThreshold;
    // 战斗模式下仅允许纯前向输入进入奔跑。
    private bool HasCombatRunInput => inputEnabled && input != null && input.SprintHeld &&
        input.Move.y > runForwardThreshold &&
        Mathf.Abs(input.Move.x) <= combatRunLateralThreshold;

    // 返回本控制器所属的 QFramework 游戏架构。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    // 初始化组件缓存、初始装备状态及全局事件监听。
    private void Awake()
    {
        controller = controller != null ? controller : GetComponent<CharacterController>();
        input = input != null ? input : GetComponent<InputManager>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        playerCombat = playerCombat != null ? playerCombat : GetComponent<PlayerCombat>();
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        isEquipped = playerAnimation != null && playerAnimation.IsEquipped;

        this.RegisterEvent<GameStateChangedEvent>(OnGameStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<LockOnTargetChangedEvent>(OnLockOnTargetChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    // 启用时订阅装备按键事件。
    private void OnEnable()
    {
        if (input != null)
            input.EquipPressed += OnEquipPressed;
    }

    // 禁用时取消订阅装备按键事件。
    private void OnDisable()
    {
        if (input != null)
            input.EquipPressed -= OnEquipPressed;
    }

    // 每帧计算移动、朝向和对应的动画参数。
    private void Update()
    {
        Vector2 moveInput = inputEnabled && input != null ? input.Move : Vector2.zero;
        bool canMove = CanMove;
        bool wantsToRun = isEquipped ? HasCombatRunInput : HasExplorationRunInput;
        isRunning = canMove && wantsToRun && (playerStats == null || playerStats.CanSprint);

        Vector3 horizontalMotion;
        if (!canMove)
        {
            horizontalMotion = Vector3.zero;
        }
        else if (isEquipped)
        {
            horizontalMotion = GetCombatMotion(moveInput, isRunning);
        }
        else
            horizontalMotion = GetExplorationMotion(moveInput, isRunning);

        ApplyMovement(horizontalMotion);

        bool isActuallyRunning = isRunning && horizontalMotion.sqrMagnitude > 0.0001f;
        if (isActuallyRunning && playerStats != null)
            playerStats.SpendSprintStamina(playerStats.StaminaDrainPerSecond * Time.deltaTime);

        IsExplorationMoving = !isEquipped && canMove && horizontalMotion.sqrMagnitude > 0.0001f;
        ExplorationMoveDirection = IsExplorationMoving ? horizontalMotion.normalized : Vector3.zero;

        if (IsExplorationMoving)
            UpdateExplorationRotation(ExplorationMoveDirection, isRunning);

        if (isEquipped && canMove)
            UpdateCombatRotation();

        Vector2 animationMove = canMove ? moveInput : Vector2.zero;
        if (IsExplorationMoving)
            animationMove = Vector2.up * Mathf.Clamp01(moveInput.magnitude);

        playerAnimation?.SetLocomotion(animationMove, isActuallyRunning);
    }

    // 将输入转换为相机相对的探索移动速度。
    private Vector3 GetExplorationMotion(Vector2 moveInput, bool isRunning)
    {
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        if (moveInput.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Camera cam = Camera.main;
        Vector3 forward = cam != null
            ? Vector3.Scale(cam.transform.forward, new Vector3(1f, 0f, 1f)).normalized
            : Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f)).normalized;
        Vector3 right = cam != null
            ? Vector3.Scale(cam.transform.right, new Vector3(1f, 0f, 1f)).normalized
            : Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 direction = right * moveInput.x + forward * moveInput.y;
        float speed = isRunning ? runSpeed : walkSpeed;
        return direction.normalized * (speed * moveInput.magnitude);
    }

    // 在探索模式平滑转向实际移动方向，并保持根节点仅绕 Y 轴旋转。
    private void UpdateExplorationRotation(Vector3 direction, bool isRunning)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float turnSpeed = isRunning ? runTurnSpeed : 540f;
        transform.rotation = Quaternion.RotateTowards(
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
            targetRotation,
            turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 战斗模式移动：相机相对横移（strafe），前进方向为相机前方
    /// </summary>
    private Vector3 GetCombatMotion(Vector2 moveInput, bool isRunning)
    {
        if (moveInput == Vector2.zero)
            return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null)
            return Vector3.zero;

        Vector3 camForward = Vector3.Scale(cam.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(cam.transform.right, new Vector3(1, 0, 1)).normalized;

        float speed = isRunning ? runSpeed : walkSpeed;
        return (camRight * moveInput.x + camForward * moveInput.y) * speed;
    }

    /// <summary>
    /// 战斗模式朝向：有锁定目标时面朝目标，否则面朝相机前方
    /// </summary>
    private void UpdateCombatRotation()
    {
        Vector3 direction;

        if (lockTarget != null)
        {
            direction = lockTarget.position - transform.position;
        }
        else
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            direction = Vector3.Scale(cam.transform.forward, new Vector3(1, 0, 1)).normalized;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, combatTurnSpeed * Time.deltaTime);
    }

    // 合并水平移动与重力后交给 CharacterController 执行。
    private void ApplyMovement(Vector3 horizontalMotion)
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontalMotion + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    // 响应装备键，切换持武器状态并广播表现层事件。
    private void OnEquipPressed()
    {
        if (!inputEnabled || (playerStats != null && !playerStats.IsAlive) ||
            (playerCombat != null && playerCombat.IsAttacking))
            return;

        playerAnimation?.ToggleEquipped();

        // Equipment changes affect animation and lock-on availability. The camera stays on one FreeLook rig.
        isEquipped = playerAnimation != null && playerAnimation.IsEquipped;
        this.SendEvent(new EquipmentChangedEvent(isEquipped));
    }

    // 接收锁定目标变化并缓存用于战斗转向。
    private void OnLockOnTargetChanged(LockOnTargetChangedEvent e)
    {
        lockTarget = e.Target;
    }

    // 根据全局游戏状态启用或禁用角色输入。
    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        inputEnabled = e.To == GameState.Playing;
    }
}
