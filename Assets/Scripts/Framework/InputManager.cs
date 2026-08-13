using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime input facade. Gameplay code reads actions here and remains independent
/// from concrete keyboard or gamepad bindings.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputManager : MonoBehaviour
{
    private const string PlayerMapName = "Player";

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private float moveDeadZone = 0.15f;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction equipAction;
    private InputAction heavyAttackAction;
    private InputAction lockOnAction;

    public Vector2 Move => ApplyDeadZone(moveAction?.ReadValue<Vector2>() ?? Vector2.zero);
    public Vector2 Look => lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
    public bool SprintHeld => sprintAction?.IsPressed() ?? false;
    public bool UsingGamepad => Gamepad.current != null &&
        (moveAction?.activeControl?.device is Gamepad || lookAction?.activeControl?.device is Gamepad);

    public event Action EquipPressed;
    public event Action HeavyAttackPressed;
    public event Action LockOnPressed;

    private void Awake()
    {
        ResolveActions();
    }

    private void OnEnable()
    {
        ResolveActions();
        playerMap?.Enable();

        if (equipAction != null)
            equipAction.performed += OnEquipPerformed;
        if (heavyAttackAction != null)
            heavyAttackAction.performed += OnHeavyAttackPerformed;
        if (lockOnAction != null)
            lockOnAction.performed += OnLockOnPerformed;
    }

    private void OnDisable()
    {
        if (equipAction != null)
            equipAction.performed -= OnEquipPerformed;
        if (heavyAttackAction != null)
            heavyAttackAction.performed -= OnHeavyAttackPerformed;
        if (lockOnAction != null)
            lockOnAction.performed -= OnLockOnPerformed;

        playerMap?.Disable();
    }

    private void ResolveActions()
    {
        if (actions == null)
        {
            Debug.LogError("InputManager requires PlayerAction.inputactions.", this);
            return;
        }

        playerMap = actions.FindActionMap(PlayerMapName, false);
        if (playerMap == null)
        {
            Debug.LogError($"Input action map '{PlayerMapName}' was not found.", this);
            return;
        }

        moveAction = playerMap.FindAction("Move", false);
        lookAction = playerMap.FindAction("Look", false);
        sprintAction = playerMap.FindAction("Sprint", false);
        equipAction = playerMap.FindAction("Equip", false);
        heavyAttackAction = playerMap.FindAction("HeavyAttack", false);
        lockOnAction = playerMap.FindAction("LockOn", false);

        if (lockOnAction == null)
        {
            // LockOn 为可选动作：缺失时仅禁用锁定功能，不影响其他输入
            Debug.LogWarning("Input map 'Player' has no 'LockOn' action. Lock-on targeting is disabled.", this);
        }
    }

    private Vector2 ApplyDeadZone(Vector2 value)
    {
        if (value.sqrMagnitude < moveDeadZone * moveDeadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(value, 1f);
    }

    private void OnEquipPerformed(InputAction.CallbackContext context)
    {
        EquipPressed?.Invoke();
    }

    private void OnHeavyAttackPerformed(InputAction.CallbackContext context)
    {
        HeavyAttackPressed?.Invoke();
    }

    private void OnLockOnPerformed(InputAction.CallbackContext context)
    {
        LockOnPressed?.Invoke();
    }
}
