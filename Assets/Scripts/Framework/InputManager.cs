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
    // Input Action Asset 中玩家动作映射的固定名称。
    private const string PlayerMapName = "Player";
    // Input Action Asset 中 UGUI 专用动作映射的固定名称。
    private const string UiMapName = "UI";

    [Header("Input Actions")]
    // 包含玩家动作与各设备绑定的 Input Action Asset。
    [SerializeField] private InputActionAsset actions;
    // 移动输入低于该幅度时视为零，主要用于手柄摇杆。
    [SerializeField] private float moveDeadZone = 0.15f;

    // 已解析并启用的玩家动作映射。
    private InputActionMap playerMap;
    // 仅在背包打开时启用的 UGUI 动作映射。
    private InputActionMap uiMap;
    // 二维移动动作。
    private InputAction moveAction;
    // 二维视角动作。
    private InputAction lookAction;
    // 按住奔跑动作。
    private InputAction sprintAction;
    // 装备切换动作。
    private InputAction equipAction;
    // 重攻击动作。
    private InputAction heavyAttackAction;
    // 锁定目标动作。
    private InputAction lockOnAction;
    // 打开或关闭背包界面的动作。
    private InputAction inventoryAction;
    // 关闭当前 UI 的取消动作。
    private InputAction uiCancelAction;
    // 当前是否允许相机读取 Look 动作。
    private bool lookInputEnabled = true;

    // 读取并应用死区后的移动输入。
    public Vector2 Move => ApplyDeadZone(moveAction?.ReadValue<Vector2>() ?? Vector2.zero);
    // 读取原始视角输入，供相机模块按设备分别缩放。
    public Vector2 Look => lookInputEnabled ? lookAction?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
    // 当前是否按住奔跑键或手柄按键。
    public bool SprintHeld => sprintAction?.IsPressed() ?? false;
    // 当前主导移动或视角动作的设备是否为手柄。
    public bool UsingGamepad => Gamepad.current != null &&
        (moveAction?.activeControl?.device is Gamepad || lookAction?.activeControl?.device is Gamepad);
    /// <summary>当前是否允许鼠标或右摇杆驱动游戏视角。</summary>
    public bool LookInputEnabled => lookInputEnabled;

    // 装备动作完成时触发。
    public event Action EquipPressed;
    // 重攻击动作完成时触发。
    public event Action HeavyAttackPressed;
    // 锁定动作完成时触发。
    public event Action LockOnPressed;
    // 背包动作完成时触发。
    public event Action InventoryPressed;
    // UI 取消动作完成时触发。
    public event Action UiCancelPressed;
    // 视角输入开关变化时触发，供相机立即清空轴输入。
    public event Action<bool> LookInputEnabledChanged;

    // 在对象启用前解析动作引用。
    private void Awake()
    {
        ResolveActions();
    }

    // 启用动作映射并注册按键完成回调。
    private void OnEnable()
    {
        ResolveActions();
        playerMap?.Enable();
        uiMap?.Disable();

        if (equipAction != null)
            equipAction.performed += OnEquipPerformed;
        if (heavyAttackAction != null)
            heavyAttackAction.performed += OnHeavyAttackPerformed;
        if (lockOnAction != null)
            lockOnAction.performed += OnLockOnPerformed;
        if (uiCancelAction != null)
            uiCancelAction.performed += OnUiCancelPerformed;
    }

    // 注销回调并禁用动作映射。
    private void OnDisable()
    {
        if (equipAction != null)
            equipAction.performed -= OnEquipPerformed;
        if (heavyAttackAction != null)
            heavyAttackAction.performed -= OnHeavyAttackPerformed;
        if (lockOnAction != null)
            lockOnAction.performed -= OnLockOnPerformed;
        if (uiCancelAction != null)
            uiCancelAction.performed -= OnUiCancelPerformed;

        playerMap?.Disable();
        uiMap?.Disable();
    }

    // 仅在背包键从未按下变为按下的首帧触发一次切换事件。
    private void Update()
    {
        if (inventoryAction != null && inventoryAction.WasPressedThisFrame())
            InventoryPressed?.Invoke();
    }

    // 从配置资源解析项目约定的各个玩家动作。
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
        inventoryAction = playerMap.FindAction("Inventory", false);
        uiMap = actions.FindActionMap(UiMapName, false);
        uiCancelAction = uiMap?.FindAction("Cancel", false);

        if (lockOnAction == null)
        {
            // LockOn 为可选动作：缺失时仅禁用锁定功能，不影响其他输入
            Debug.LogWarning("Input map 'Player' has no 'LockOn' action. Lock-on targeting is disabled.", this);
        }
    }

    // 过滤摇杆微小偏移，并将有效输入限制在单位圆内。
    private Vector2 ApplyDeadZone(Vector2 value)
    {
        if (value.sqrMagnitude < moveDeadZone * moveDeadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(value, 1f);
    }

    /// <summary>启用或禁用背包等 UGUI 界面的专用动作映射。</summary>
    public void SetUiInputEnabled(bool enabled)
    {
        if (uiMap == null)
            return;

        if (enabled)
            uiMap.Enable();
        else
            uiMap.Disable();
    }

    /// <summary>启用或禁用游戏视角输入，背包等暂停界面打开时应关闭。</summary>
    public void SetLookInputEnabled(bool enabled)
    {
        if (lookInputEnabled == enabled)
            return;

        lookInputEnabled = enabled;
        LookInputEnabledChanged?.Invoke(enabled);
    }

    // 将 Input System 的装备完成回调转发为 C# 事件。
    private void OnEquipPerformed(InputAction.CallbackContext context)
    {
        EquipPressed?.Invoke();
    }

    // 将 Input System 的重攻击完成回调转发为 C# 事件。
    private void OnHeavyAttackPerformed(InputAction.CallbackContext context)
    {
        HeavyAttackPressed?.Invoke();
    }

    // 将 Input System 的锁定完成回调转发为 C# 事件。
    private void OnLockOnPerformed(InputAction.CallbackContext context)
    {
        LockOnPressed?.Invoke();
    }

    // 将 Input System 的 UI 取消动作转发为 C# 事件。
    private void OnUiCancelPerformed(InputAction.CallbackContext context)
    {
        UiCancelPressed?.Invoke();
    }
}
