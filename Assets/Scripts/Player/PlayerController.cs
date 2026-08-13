using QFramework;
using UnityEngine;

/// <summary>
/// Eight-direction character movement plus forward-only running.
/// Direction bindings are supplied by Input System through InputManager.
///
/// 双模式移动（与 CameraModeController 的相机模式对应）：
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
    [SerializeField] private Transform facingReference;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0f)] private float runSpeed = 5.5f;
    [SerializeField, Min(0f)] private float runTurnSpeed = 180f;
    [SerializeField, Range(0f, 1f)] private float runForwardThreshold = 0.15f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float combatTurnSpeed = 12f;

    private float verticalVelocity;
    private bool inputEnabled = true;
    private bool isEquipped;
    private Transform lockTarget;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        controller = controller != null ? controller : GetComponent<CharacterController>();
        input = input != null ? input : GetComponent<InputManager>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
        playerCombat = playerCombat != null ? playerCombat : GetComponent<PlayerCombat>();

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
        bool isRunning = inputEnabled && input != null && input.SprintHeld &&
            moveInput.y > runForwardThreshold;

        bool canMove = playerCombat == null || !playerCombat.IsAttacking;

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
        {
            horizontalMotion = isRunning ? GetRunningMotion(moveInput) : GetWalkingMotion(moveInput);
        }

        ApplyMovement(horizontalMotion);

        if (isEquipped)
            UpdateCombatRotation();

        playerAnimation?.SetLocomotion(canMove ? moveInput : Vector2.zero, canMove && isRunning);
    }

    private Vector3 GetWalkingMotion(Vector2 moveInput)
    {
        if (moveInput == Vector2.zero)
            return Vector3.zero;

        Transform reference = facingReference;
        if (reference == null && Camera.main != null)
            reference = Camera.main.transform;
        if (reference == null)
            reference = transform;

        Vector3 forward = Vector3.Scale(reference.forward, new Vector3(1f, 0f, 1f)).normalized;
        Vector3 right = Vector3.Scale(reference.right, new Vector3(1f, 0f, 1f)).normalized;
        return (right * moveInput.x + forward * moveInput.y) * walkSpeed;
    }

    private Vector3 GetRunningMotion(Vector2 moveInput)
    {
        transform.Rotate(Vector3.up, moveInput.x * runTurnSpeed * Time.deltaTime, Space.World);

        // Running has one forward clip, so sideways input steers instead of strafing.
        return transform.forward * (runSpeed * Mathf.Clamp01(moveInput.magnitude));
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

        // 同步本地状态并广播，驱动相机模式切换（CameraModeController）与锁定解除（LockOnController）
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
