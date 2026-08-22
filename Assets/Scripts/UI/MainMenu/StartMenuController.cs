using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// GameStart 场景的开始菜单控制器。
/// 负责构建首版 UGUI 菜单、管理菜单淡出，并把场景切换委托给 SceneLoader。
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    // 为开始菜单提供导航和确认操作的 Input Action Asset。
    [SerializeField] private InputActionAsset inputActions;
#if UNITY_EDITOR
    // 在 Inspector 中绑定将要进入的场景资源。
    [SerializeField] private SceneAsset targetScene;
#endif
    // 构建版本使用的目标场景路径，由 OnValidate 从场景资源同步。
    [SerializeField] private string targetScenePath;
    // 场景中预先搭建的菜单根节点透明度与交互控制组件。
    [SerializeField] private CanvasGroup menuCanvasGroup;
    // 场景中预先搭建的开始游戏按钮。
    [SerializeField] private Button startButton;
    // 场景中预先搭建的设置按钮。
    [SerializeField] private Button settingsButton;
    // 场景中预先搭建的退出游戏按钮。
    [SerializeField] private Button quitButton;
    // 菜单淡出耗时。
    [SerializeField, Min(0.05f)] private float menuFadeDuration = 0.25f;

    // 菜单 UI 的 CanvasGroup，用于淡出与阻止重复交互。
    // 开始游戏按钮，用于默认焦点和加载期间禁用。
    // 退出游戏按钮，用于加载期间禁用。
    // 当前是否已经触发场景加载。
    private bool isStarting;

    // 启动时构建基础菜单、显示光标并设置默认 UGUI 焦点。
    private void Awake()
    {
        ConfigureEventSystem();
        ConfigureMenu();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 首帧后确保 EventSystem 已就绪，再选中开始按钮。
    private IEnumerator Start()
    {
        yield return null;
        if (startButton != null)
            EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
    }

    /// <summary>淡出菜单并异步进入 Inspector 绑定的游戏场景。</summary>
    // 释放按钮监听，防止组件重复启用时重复注册。
    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);
    }

    /// <summary>淡出菜单并异步进入 Inspector 绑定的游戏场景。</summary>
    public void StartGame()
    {
        if (isStarting)
            return;

        if (string.IsNullOrWhiteSpace(targetScenePath))
        {
            Debug.LogError($"[{nameof(StartMenuController)}] Target scene is not assigned.", this);
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    /// <summary>在构建版本退出应用，并在编辑器 Play Mode 中停止运行。</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>打开跨场景复用的设置面板。</summary>
    public void OpenSettings()
    {
        UISettings.Instance?.OpenFromMainMenu();
    }

#if UNITY_EDITOR
    // 将 Inspector 场景资源转换为构建版本可用的路径。
    private void OnValidate()
    {
        if (targetScene != null)
            targetScenePath = AssetDatabase.GetAssetPath(targetScene);
    }
#endif

    // 锁定菜单交互、完成菜单淡出并启动持久化加载器。
    // 校验场景引用、绑定按钮事件并建立显式菜单导航。
    private void ConfigureMenu()
    {
        if (menuCanvasGroup == null || startButton == null || settingsButton == null || quitButton == null)
        {
            Debug.LogError($"[{nameof(StartMenuController)}] Menu UI references are incomplete.", this);
            enabled = false;
            return;
        }

        menuCanvasGroup.alpha = 1f;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
        ConfigureMenuNavigation();
    }

    private IEnumerator StartGameRoutine()
    {
        isStarting = true;
        startButton.interactable = false;
        quitButton.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;
        menuCanvasGroup.interactable = false;

        float elapsed = 0f;
        while (elapsed < menuFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            menuCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / menuFadeDuration);
            yield return null;
        }

        SceneLoader.GetOrCreate().LoadSceneAsync(targetScenePath);
    }

    // 将场景中旧输入模块替换为 Input System UI 模块，并复用既有 UI Action Map。
    private void ConfigureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null || inputActions == null)
            return;

        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
            standaloneModule.enabled = false;

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputModule.actionsAsset = inputActions;
        inputModule.move = InputActionReference.Create(inputActions.FindAction("UI/Navigate", true));
        inputModule.submit = InputActionReference.Create(inputActions.FindAction("UI/Submit", true));
        inputModule.cancel = InputActionReference.Create(inputActions.FindAction("UI/Cancel", true));
        inputModule.point = InputActionReference.Create(inputActions.FindAction("UI/Point", true));
        inputModule.leftClick = InputActionReference.Create(inputActions.FindAction("UI/Click", true));
    }

    // 为两个菜单按钮建立显式循环导航。
    private void ConfigureMenuNavigation()
    {
        Navigation startNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        startNavigation.selectOnDown = settingsButton;
        startNavigation.selectOnUp = quitButton;
        startButton.navigation = startNavigation;

        Navigation settingsNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        settingsNavigation.selectOnDown = quitButton;
        settingsNavigation.selectOnUp = startButton;
        settingsButton.navigation = settingsNavigation;

        Navigation quitNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        quitNavigation.selectOnDown = startButton;
        quitNavigation.selectOnUp = settingsButton;
        quitButton.navigation = quitNavigation;
    }

}
