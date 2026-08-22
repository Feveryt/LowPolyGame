using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 跨场景复用的设置面板，负责暂停、保存、返回菜单与输入焦点管理。
/// </summary>
[DisallowMultipleComponent]
public sealed class UISettings : SingletonPanel<UISettings>
{
    // 设置面板显示时使用的场景上下文。
    private enum SettingsContext
    {
        MainMenu,
        Gameplay
    }

    // 设置面板整体显示和射线拦截控制组件。
    [Header("Panel References")]
    [SerializeField] private CanvasGroup panelCanvasGroup;
    // 仅在游戏场景显示的继续、保存和返回菜单按钮容器。
    [SerializeField] private GameObject gameplayButtonsRoot;
    // 恢复游戏的按钮。
    [SerializeField] private Button continueButton;
    // 手动保存当前单槽进度的按钮。
    [SerializeField] private Button saveButton;
    // 自动保存后异步返回 GameStart 的按钮。
    [SerializeField] private Button returnToMenuButton;
    // 在 GameStart 场景关闭设置面板的按钮。
    [SerializeField] private Button returnButton;
    // 回到主菜单时加载的场景路径。
    [Header("Scene Settings")]
    [SerializeField] private string mainMenuScenePath = "Assets/Scenes/GameScene/GameStart.unity";

    // 当前场景的玩家输入转发组件。
    private InputManager inputManager;
    // 当前场景的鼠标锁定管理组件。
    private CursorManager cursorManager;
    // 设置打开前 CursorManager 的启用状态。
    private bool cursorManagerWasEnabled;
    // 面板是否处于可见状态。
    private bool isOpen;
    // 当前打开面板的场景上下文。
    private SettingsContext context;

    /// <summary>设置面板当前是否正在显示。</summary>
    public bool IsOpen => isOpen;

    // 初始化唯一实例、场景监听和静态按钮事件。
    private void Awake()
    {
        if (!TryInitializeSingleton())
            return;

        SetPanelVisible(false);
        WireButtons();
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshSceneReferences();
    }

    // 释放输入和场景事件订阅，并清理单例引用。
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DetachInputManager();
        ReleaseSingleton();
    }

    /// <summary>
    /// 从游戏场景打开设置面板，并冻结游戏输入与世界时间。
    /// </summary>
    public void OpenFromGameplay()
    {
        if (isOpen)
            return;

        RefreshSceneReferences();
        if (inputManager == null || inputManager.IsUiInputEnabled)
            return;

        context = SettingsContext.Gameplay;
        isOpen = true;
        SetContextButtons();
        SetPanelVisible(true);
        inputManager.SetPlayerInputEnabled(false);
        inputManager.SetUiInputEnabled(true);
        inputManager.SetLookInputEnabled(false);
        SetGameState(GameState.Paused);
        SetCursorForSettings(true);
        SelectButton(continueButton);
    }

    /// <summary>
    /// 从 GameStart 场景打开设置面板，不改变菜单场景的时间缩放状态。
    /// </summary>
    public void OpenFromMainMenu()
    {
        if (isOpen)
            return;

        RefreshSceneReferences();
        context = SettingsContext.MainMenu;
        isOpen = true;
        SetContextButtons();
        SetPanelVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SelectButton(returnButton);
    }

    /// <summary>
    /// 根据打开场景关闭设置面板，并恢复对应的输入与光标状态。
    /// </summary>
    public void CloseSettings()
    {
        if (!isOpen)
            return;

        bool wasGameplay = context == SettingsContext.Gameplay;
        isOpen = false;
        SetPanelVisible(false);

        if (!wasGameplay)
            return;

        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetPlayerInputEnabled(true);
        inputManager?.SetLookInputEnabled(true);
        SetGameState(GameState.Playing);
        SetCursorForSettings(false);
    }

    // 绑定按钮到设置面板的固定操作。
    private void WireButtons()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(CloseSettings);
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveGame);
        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (returnButton != null)
            returnButton.onClick.AddListener(CloseSettings);
    }

    // 场景激活后重新绑定新场景的输入与光标组件。
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneReferences();
        if (isOpen && scene.path == mainMenuScenePath)
            CloseSettings();
    }

    // 绑定当前场景 InputManager，避免跨场景残留旧事件订阅。
    private void RefreshSceneReferences()
    {
        InputManager foundInputManager = FindFirstObjectByType<InputManager>();
        if (foundInputManager != inputManager)
        {
            DetachInputManager();
            inputManager = foundInputManager;
            if (inputManager != null)
            {
                inputManager.SettingsPressed += HandleSettingsPressed;
                inputManager.UiCancelPressed += HandleUiCancelPressed;
            }
        }

        cursorManager = FindFirstObjectByType<CursorManager>();
    }

    // 解除当前 InputManager 的设置和取消回调。
    private void DetachInputManager()
    {
        if (inputManager == null)
            return;

        inputManager.SettingsPressed -= HandleSettingsPressed;
        inputManager.UiCancelPressed -= HandleUiCancelPressed;
        inputManager = null;
    }

    // 仅在未被背包或对话占用时响应游戏设置快捷键。
    private void HandleSettingsPressed()
    {
        if (!isOpen)
            OpenFromGameplay();
    }

    // 设置打开时由 UI Cancel 关闭面板。
    private void HandleUiCancelPressed()
    {
        if (isOpen)
            CloseSettings();
    }

    // 依据场景上下文切换可见按钮和 UGUI 导航关系。
    private void SetContextButtons()
    {
        bool gameplay = context == SettingsContext.Gameplay;
        if (gameplayButtonsRoot != null)
            gameplayButtonsRoot.SetActive(gameplay);
        if (returnButton != null)
            returnButton.gameObject.SetActive(!gameplay);

        if (!gameplay || continueButton == null || saveButton == null || returnToMenuButton == null)
            return;

        SetVerticalNavigation(continueButton, returnToMenuButton, saveButton);
        SetVerticalNavigation(saveButton, continueButton, returnToMenuButton);
        SetVerticalNavigation(returnToMenuButton, saveButton, continueButton);
    }

    // 配置一个按钮的循环上下导航。
    private static void SetVerticalNavigation(Button button, Button up, Button down)
    {
        Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        button.navigation = navigation;
    }

    // 将当前 UGUI 焦点切换到指定按钮。
    private static void SelectButton(Button button)
    {
        if (button != null)
            EventSystem.current?.SetSelectedGameObject(button.gameObject);
    }

    // 保存当前游戏进度，保存失败由 SaveManager 输出具体原因。
    private void SaveGame()
    {
        SaveManager.Instance?.SaveGame();
    }

    // 自动保存并通过统一加载器返回开始菜单。
    private void ReturnToMainMenu()
    {
        if (context != SettingsContext.Gameplay || string.IsNullOrWhiteSpace(mainMenuScenePath))
            return;

        SaveGame();
        isOpen = false;
        SetPanelVisible(false);
        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetPlayerInputEnabled(true);
        inputManager?.SetLookInputEnabled(true);
        SetGameState(GameState.MainMenu);
        SetCursorForSettings(false);
        SceneLoader.GetOrCreate().LoadSceneAsync(mainMenuScenePath);
    }

    // 通过 CanvasGroup 切换面板可见性和 UI 射线拦截。
    private void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    // 设置打开或关闭时同步 CursorManager 与系统光标状态。
    private void SetCursorForSettings(bool active)
    {
        if (active)
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

    // 通过 QFramework 统一切换游戏运行状态。
    private static void SetGameState(GameState state)
    {
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));
    }
}
