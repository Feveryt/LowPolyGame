using QFramework;
using UnityEngine;

/// <summary>
/// Eight-direction character movement plus forward-only running.
/// Direction bindings are supplied by Input System through InputManager.
///
/// Exploration and combat movement rules:
/// - 未持武器（探索）：八方向行走 + 仅前进奔跑（侧向输入转为转向）
/// - 持武器（战斗）：相机相对横移（strafe）；有锁定目标时始终面朝目标
///
/// 架构说明：IController 默认无 ICanSendEvent 能力（QFramework 分层规则：
/// 表现层只监听事件，事件应由 Model/System 产生）。本控制器广播的
/// EquipmentChangedEvent 属于"实时玩法表现事件"，与业务数据无关，
/// 故显式声明 ICanSendEvent 作为工程折衷。业务数据变更仍须走 Model/Command。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(PlayerAnimation))]
public sealed class PlayerController : MonoBehaviour, IController, ICanSendEvent
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private InputManager input;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0f)] private float runSpeed = 5.5f;
    [SerializeField, Min(0f)] private float runTurnSpeed = 360f;
    [SerializeField, Range(0f, 1f)] private float runForwardThreshold = 0.15f;
    [SerializeField, Range(0f, 1f)] private float combatRunLateralThreshold = 0.1f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float combatTurnSpeed = 12f;

    private float verticalVelocity;
    private bool inputEnabled = true;
    private bool isEquipped;
    private Transform lockTarget;

    public Vector3 ExplorationMoveDirection { get; private set; }
    public bool IsExplorationMoving { get; private set; }
    public bool IsEquipped => isEquipped;
    public bool IsExplorationRunning => CanMove && !isEquipped && HasExplorationRunInput;

    private bool CanMove => playerCombat == null || !playerCombat.IsAttacking;
    private bool HasExplorationRunInput => inputEnabled && input != null && input.SprintHeld &&
        input.Move.sqrMagnitude > runForwardThreshold * runForwardThreshold;
    private bool HasCombatRunInput => inputEnabled && input != null && input.SprintHeld &&
        input.Move.y > runForwardThreshold &&
        Mathf.Abs(input.Move.x) <= combatRunLateralThreshold;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        controller = controller != null ? controller : GetComponent<CharacterController>();
        input = input != null ? input : GetComponent<InputManager>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        playerCombat = playerCombat != null ? playerCombat : GetComponent<PlayerCombat>();
        isEquipped = playerAnimation != null && playerAnimation.IsEquipped;

        this.RegisterEvent<GameStateChangedEvent>(OnGameStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<LockOnTargetChangedEvent>(OnLockOnTargetChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnEnable()
    {
        if (input != null)
            input.EquipPressed += OnEquipPressed;
    }

    private void OnDisable()
    {
        if (input != null)
            input.EquipPressed -= OnEquipPressed;
    }

    private void Update()
    {
        Vector2 moveInput = inputEnabled && input != null ? input.Move : Vector2.zero;
        bool canMove = CanMove;
        bool isRunning = isEquipped ? HasCombatRunInput : HasExplorationRunInput;

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

        IsExplorationMoving = !isEquipped && canMove && horizontalMotion.sqrMagnitude > 0.0001f;
        ExplorationMoveDirection = IsExplorationMoving ? horizontalMotion.normalized : Vector3.zero;

        if (IsExplorationMoving)
            UpdateExplorationRotation(ExplorationMoveDirection, isRunning);

        if (isEquipped)
            UpdateCombatRotation();

        Vector2 animationMove = canMove ? moveInput : Vector2.zero;
        if (IsExplorationMoving)
            animationMove = Vector2.up * Mathf.Clamp01(moveInput.magnitude);

        playerAnimation?.SetLocomotion(animationMove, canMove && isRunning);
    }

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

    private void ApplyMovement(Vector3 horizontalMotion)
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontalMotion + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private void OnEquipPressed()
    {
        if (!inputEnabled || (playerCombat != null && playerCombat.IsAttacking))
            return;

        playerAnimation?.ToggleEquipped();

        // Equipment changes affect animation and lock-on availability. The camera stays on one FreeLook rig.
        isEquipped = playerAnimation != null && playerAnimation.IsEquipped;
        this.SendEvent(new EquipmentChangedEvent(isEquipped));
    }

    private void OnLockOnTargetChanged(LockOnTargetChangedEvent e)
    {
        lockTarget = e.Target;
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        inputEnabled = e.To == GameState.Playing;
    }
}
