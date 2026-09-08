using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 跨场景 UI 实例管理器：按场景配置确保面板存在并启用面板生命周期。
/// 管理器不会调用面板的打开/关闭接口，面板可见状态由面板自身负责。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour
{
    /// <summary>单个面板的实例化和场景生命周期配置。</summary>
    [Serializable]
    public sealed class PanelEntry
    {
        /// <summary>供代码查询的稳定面板 ID。</summary>
        public string id;
        /// <summary>完整面板预制体，不应填写格子、按钮等子预制体。</summary>
        public GameObject prefab;
        /// <summary>允许该面板出现的场景路径；为空表示所有场景。</summary>
        public string[] scenePaths;
        /// <summary>场景切换时是否保留该实例。</summary>
        public bool keepAlive;
        /// <summary>确保实例后是否激活根 GameObject。</summary>
        public bool activateOnEnsure = true;
    }

    private const string ManagerPrefabPath = "Prefabs/UI/UIManager";
    private static UIManager instance;

    [SerializeField] private List<PanelEntry> panelEntries = new List<PanelEntry>();
    private readonly Dictionary<string, GameObject> panelInstances = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private bool initialized;

    /// <summary>当前跨场景 UIManager 实例。</summary>
    public static UIManager Instance => instance;

    /// <summary>自动创建并持久化 UIManager，确保不依赖场景手动摆放。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnLoad()
    {
        if (instance != null)
            return;

        UIManager prefab = Resources.Load<UIManager>(ManagerPrefabPath);
        if (prefab != null)
        {
            instance = Instantiate(prefab);
            instance.name = nameof(UIManager);
            return;
        }

        GameObject managerObject = new GameObject(nameof(UIManager));
        instance = managerObject.AddComponent<UIManager>();
        Debug.LogError($"[{nameof(UIManager)}] 未找到 Resources/{ManagerPrefabPath}.prefab，无法读取面板配置。", managerObject);
    }

    /// <summary>初始化单例、注册场景回调并准备当前场景面板。</summary>
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        initialized = true;
    }

    /// <summary>首帧时处理首个已加载场景。</summary>
    private void Start()
    {
        EnsurePanelsForScene(SceneManager.GetActiveScene());
    }

    /// <summary>清理场景回调和实例引用。</summary>
    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    /// <summary>确保指定 ID 的面板存在；不会调用面板的 Open/Show 方法。</summary>
    public GameObject EnsurePanel(string id)
    {
        PanelEntry entry = FindEntry(id);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogError($"[{nameof(UIManager)}] 面板配置不存在或 Prefab 未设置：{id}", this);
            return null;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsAvailableInScene(entry, activeScene))
        {
            Debug.LogWarning($"[{nameof(UIManager)}] 面板 {id} 不属于当前场景 {activeScene.path}。", this);
            return null;
        }

        GameObject panel = FindExistingPanel(entry);
        if (panel == null)
        {
            Canvas prefabCanvas = entry.prefab.GetComponent<Canvas>();
            Canvas parentCanvas = prefabCanvas == null ? GetOrCreateCanvas() : null;
            panel = parentCanvas != null
                ? Instantiate(entry.prefab, parentCanvas.transform, false)
                : Instantiate(entry.prefab);
            panel.name = entry.prefab.name;
        }

        AttachMarker(panel, entry.id);
        if (entry.keepAlive && panel.scene.IsValid())
            DontDestroyOnLoad(panel);

        if (entry.activateOnEnsure && !panel.activeSelf)
            panel.SetActive(true);

        panelInstances[entry.id] = panel;
        RemoveDuplicateInstances(entry, panel);
        return panel;
    }

    /// <summary>按 ID 查询已确保的面板实例。</summary>
    public bool TryGetPanel(string id, out GameObject panel)
    {
        if (panelInstances.TryGetValue(id, out panel) && panel != null)
            return true;

        panel = null;
        return false;
    }

    /// <summary>按 ID 查询面板上的指定组件。</summary>
    public T GetPanel<T>(string id) where T : Component
    {
        return TryGetPanel(id, out GameObject panel) ? panel.GetComponent<T>() : null;
    }

    /// <summary>重新按当前场景配置检查所有面板。</summary>
    public void RefreshCurrentScene()
    {
        EnsurePanelsForScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CleanupScenePanels(scene);
        EnsurePanelsForScene(scene);
    }

    private void EnsurePanelsForScene(Scene scene)
    {
        if (!initialized || !scene.IsValid())
            return;

        for (int index = 0; index < panelEntries.Count; index++)
        {
            PanelEntry entry = panelEntries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.prefab == null)
            {
                Debug.LogError($"[{nameof(UIManager)}] 第 {index} 个面板条目无效。", this);
                continue;
            }

            if (IsAvailableInScene(entry, scene))
                EnsurePanel(entry.id);
        }
    }

    private void CleanupScenePanels(Scene activeScene)
    {
        for (int index = 0; index < panelEntries.Count; index++)
        {
            PanelEntry entry = panelEntries[index];
            if (entry == null || entry.keepAlive || IsAvailableInScene(entry, activeScene))
                continue;

            if (panelInstances.TryGetValue(entry.id, out GameObject panel) && panel != null && panel.scene.IsValid())
            {
                Destroy(panel);
                panelInstances.Remove(entry.id);
            }
        }
    }

    private GameObject FindExistingPanel(PanelEntry entry)
    {
        if (panelInstances.TryGetValue(entry.id, out GameObject tracked) && tracked != null)
            return tracked;

        UIManagedPanel[] markers = FindObjectsByType<UIManagedPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < markers.Length; index++)
        {
            if (markers[index].PanelId == entry.id)
                return markers[index].gameObject;
        }

        // 兼容尚未添加标记组件的旧场景实例。
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate.name == entry.prefab.name && candidate.gameObject.scene.IsValid())
                return candidate.gameObject;
        }

        return null;
    }

    private void RemoveDuplicateInstances(PanelEntry entry, GameObject retained)
    {
        UIManagedPanel[] markers = FindObjectsByType<UIManagedPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < markers.Length; index++)
        {
            UIManagedPanel marker = markers[index];
            if (marker.PanelId != entry.id || marker.gameObject == retained)
                continue;

            Debug.LogWarning($"[{nameof(UIManager)}] 检测到重复面板 {entry.id}，已销毁 {marker.gameObject.name}。", marker);
            Destroy(marker.gameObject);
        }
    }

    private static UIManagedPanel AttachMarker(GameObject panel, string id)
    {
        UIManagedPanel marker = panel.GetComponent<UIManagedPanel>();
        if (marker == null)
            marker = panel.AddComponent<UIManagedPanel>();
        marker.SetPanelId(id);
        return marker;
    }

    private PanelEntry FindEntry(string id)
    {
        for (int index = 0; index < panelEntries.Count; index++)
            if (panelEntries[index] != null && string.Equals(panelEntries[index].id, id, StringComparison.Ordinal))
                return panelEntries[index];
        return null;
    }

    private static bool IsAvailableInScene(PanelEntry entry, Scene scene)
    {
        if (entry.scenePaths == null || entry.scenePaths.Length == 0)
            return true;

        for (int index = 0; index < entry.scenePaths.Length; index++)
            if (string.Equals(entry.scenePaths[index], scene.path, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static Canvas GetOrCreateCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < canvases.Length; index++)
        {
            if (canvases[index].gameObject.scene == SceneManager.GetActiveScene())
                return canvases[index];
        }

        GameObject canvasObject = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return canvas;
    }
}
