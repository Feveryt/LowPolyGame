using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>首次进入 Demo 时显示一次可跳过的全屏剧情文字。</summary>
public sealed class OpeningNarrative : MonoBehaviour
{
    private const string SeenKey = "LowPolyGame.UnsignedGuardian.OpeningSeen";
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text narrativeText;
    [SerializeField] private Button dismissButton;
    [SerializeField, TextArea(3, 6)] private string text;

    // 玩家输入、镜头输入与系统光标的临时接管对象。
    private InputManager inputManager;
    private CursorManager cursorManager;
    // 显示前光标管理器的启用状态。
    private bool cursorManagerWasEnabled;
    // 是否已经完成动态创建对象的初始化。
    private bool initialized;
    // 开场面板当前是否仍在阻挡游戏。
    private bool isShown;

    /// <summary>配置由启动器生成的开场 UI 引用与固定文本。</summary>
    public void Configure(CanvasGroup group, Text narrative, Button dismiss, string content)
    {
        canvasGroup = group;
        narrativeText = narrative;
        dismissButton = dismiss;
        text = content;
        Initialize();
    }

    // 按本地进度决定显示，并连接点击跳过操作。
    private void Awake()
    {
        Initialize();
    }

    // 输入模块未能转发取消事件时，仍允许用 Esc 或手柄东键关闭开场面板。
    private void Update()
    {
        if (!isShown)
            return;

        bool cursorAttached = AttachRuntimeManagers();
        if (cursorAttached)
            SetCursor(true);

        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
        {
            Dismiss();
            return;
        }

        // 某些场景会在暂停后重置光标状态，开场期间持续保持可见以确保按钮可点击。
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 清理按钮监听，避免销毁对象保留回调。
    private void OnDestroy()
    {
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(Dismiss);
        if (inputManager != null)
        {
            inputManager.UiCancelPressed -= Dismiss;
            inputManager.SetUiInputEnabled(false);
            inputManager.SetPlayerInputEnabled(true);
            inputManager.SetLookInputEnabled(true);
        }
        if (isShown)
        {
            SetGameState(GameState.Playing);
            SetCursor(false);
        }
    }

    /// <summary>关闭开场文字并记录已观看状态。</summary>
    public void Dismiss()
    {
        if (!isShown)
            return;

        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        DismissImmediately();
    }

    // 显示并接管输入的开场面板。
    private void Show()
    {
        if (canvasGroup == null)
            return;
        AttachRuntimeManagers();
        SetGameState(GameState.Paused);
        SetCursor(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        isShown = true;
        EventSystem.current?.SetSelectedGameObject(dismissButton != null ? dismissButton.gameObject : null);
    }

    // 隐藏面板并停止阻挡场景交互。
    private void DismissImmediately()
    {
        if (canvasGroup == null)
            return;
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(Dismiss);
        if (inputManager != null)
        {
            inputManager.UiCancelPressed -= Dismiss;
            inputManager.SetUiInputEnabled(false);
            inputManager.SetPlayerInputEnabled(true);
            inputManager.SetLookInputEnabled(true);
        }
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        if (isShown)
        {
            SetGameState(GameState.Playing);
            SetCursor(false);
        }
        isShown = false;
    }

    // 玩家对象可能在开场面板之后才生成，首次找到时立即接管其输入和光标。
    private bool AttachRuntimeManagers()
    {
        bool cursorAttached = false;
        if (inputManager == null)
        {
            inputManager = FindFirstObjectByType<InputManager>();
            if (inputManager != null)
            {
                inputManager.SetPlayerInputEnabled(false);
                inputManager.SetUiInputEnabled(true);
                inputManager.SetLookInputEnabled(false);
                inputManager.UiCancelPressed -= Dismiss;
                inputManager.UiCancelPressed += Dismiss;
            }
        }

        if (cursorManager == null)
        {
            cursorManager = FindFirstObjectByType<CursorManager>();
            cursorAttached = cursorManager != null;
        }

        return cursorAttached;
    }

    // 动态对象的 Awake 可能早于 Configure，统一在两处调用保证按钮和文本都能初始化。
    private void Initialize()
    {
        if (initialized || canvasGroup == null || dismissButton == null)
            return;

        initialized = true;
        if (narrativeText != null)
            narrativeText.text = text;
        dismissButton.onClick.RemoveListener(Dismiss);
        dismissButton.onClick.AddListener(Dismiss);
        if (PlayerPrefs.GetInt(SeenKey, 0) == 1)
        {
            AttachRuntimeManagers();
            DismissImmediately();
            RestoreGameplayCursor();
        }
        else
            Show();
    }

    // 已看过开场剧情时没有经过显示/关闭流程，直接恢复游戏态光标。
    private void RestoreGameplayCursor()
    {
        if (cursorManager != null)
        {
            cursorManager.enabled = true;
            cursorManager.LockCursor();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 开场文字结束后统一恢复项目的游戏状态和光标状态。
    private void SetCursor(bool opening)
    {
        if (opening)
        {
            if (cursorManager != null)
            {
                cursorManagerWasEnabled = cursorManager.enabled;
                cursorManager.enabled = false;
                cursorManager.UnlockCursor();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        if (cursorManager != null)
        {
            cursorManager.enabled = cursorManagerWasEnabled;
            if (cursorManagerWasEnabled)
                cursorManager.LockCursor();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 通过项目状态机冻结或恢复开场期间的世界时间。
    private static void SetGameState(GameState state)
    {
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }
}
