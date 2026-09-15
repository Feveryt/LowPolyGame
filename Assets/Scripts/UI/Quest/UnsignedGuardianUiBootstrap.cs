using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>在任意可玩场景自动创建《未署名的守卫》的开场文字和最小任务 HUD。</summary>
public static class UnsignedGuardianUiBootstrap
{
    // Demo 场景路径，用于判断是否需要创建或显示剧情 UI。
    private const string DemoScenePath = "Assets/Scenes/GameScene/Demo 1.unity";
    // 跨场景保留的剧情 UI 根节点。
    private static GameObject uiRoot;
    // 保证场景加载回调只订阅一次。
    private static bool sceneLoadedSubscribed;

    // 首个场景载入后补建 UI，并订阅后续场景切换。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        SubscribeSceneLoaded();
        ApplyScene(SceneManager.GetActiveScene());
    }

    // 从开始菜单进入 Demo 时 AfterSceneLoad 不会再次执行，需要在场景加载时补建 UI。
    private static void SubscribeSceneLoaded()
    {
        if (sceneLoadedSubscribed)
            return;

        sceneLoadedSubscribed = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 场景加载完成后同步显示或隐藏常驻剧情 UI。
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    // 仅在 Demo 场景创建并显示剧情 UI，回到其他场景时隐藏常驻画布。
    private static void ApplyScene(Scene scene)
    {
        if (scene.path != DemoScenePath)
        {
            if (uiRoot != null)
                uiRoot.SetActive(false);
            return;
        }

        EnsureGameplayCursor();
        if (uiRoot == null)
        {
            CreateUiRoot();
            return;
        }

        uiRoot.SetActive(true);
        CreateQuestPanel(uiRoot.transform);
    }

    // 创建承载开场文字、任务 HUD 与任务面板的常驻画布。
    private static void CreateUiRoot()
    {
        OpeningNarrative existingOpening = Object.FindFirstObjectByType<OpeningNarrative>();
        if (existingOpening != null)
        {
            Transform openingParent = existingOpening.transform.parent;
            uiRoot = openingParent != null ? openingParent.gameObject : existingOpening.gameObject;
            CreateQuestPanel(openingParent);
            return;
        }

        GameObject canvasObject = new GameObject("Unsigned Guardian UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(canvasObject);
        uiRoot = canvasObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        CreateOpening(canvasObject.transform);
        CreateQuestHud(canvasObject.transform);
        CreateQuestPanel(canvasObject.transform);
    }

    // Demo 场景不依赖玩家预制体，单独创建场景级光标管理器接管游戏态光标。
    private static void EnsureGameplayCursor()
    {
        if (Object.FindFirstObjectByType<CursorManager>() != null)
            return;

        GameObject cursorObject = new GameObject("Gameplay Cursor Manager");
        cursorObject.AddComponent<CursorManager>();
    }

    // 从 Resources 加载独立任务面板，避免在场景中重复维护同一份 UI。
    private static void CreateQuestPanel(Transform parent)
    {
        if (Object.FindFirstObjectByType<QuestPanel>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/QuestPanel");
        if (prefab == null)
        {
            Debug.LogError("Missing quest panel prefab at Resources/Prefabs/UI/QuestPanel.");
            return;
        }

        Object.Instantiate(prefab, parent, false);
    }

    // 生成黑色遮罩、叙事文字和明确的继续按钮。
    private static void CreateOpening(Transform parent)
    {
        GameObject panel = CreateUiObject("Opening Narrative", parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Stretch(panelRect);
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.03f, 0.04f, 0.05f, 0.92f);
        CanvasGroup group = panel.AddComponent<CanvasGroup>();
        Text narrative = CreateText("Narrative", panel.transform, 34, TextAnchor.MiddleCenter);
        RectTransform narrativeRect = narrative.rectTransform;
        narrativeRect.anchorMin = new Vector2(0.18f, 0.28f);
        narrativeRect.anchorMax = new Vector2(0.82f, 0.72f);
        narrativeRect.offsetMin = Vector2.zero;
        narrativeRect.offsetMax = Vector2.zero;
        Button dismiss = CreateButton("Continue", panel.transform, "继续");
        RectTransform buttonRect = dismiss.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.16f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.16f);
        buttonRect.sizeDelta = new Vector2(180f, 54f);
        GameObject controller = panel.AddComponent<OpeningNarrative>().gameObject;
        controller.GetComponent<OpeningNarrative>().Configure(group, narrative, dismiss,
            "城邦将这座遗址称作刑场。\n\n但失联的记录员在最后一封报告中写道：\n\n“石头没有审判任何人。它只记得，谁下令遗忘。”");
    }

    // 生成屏幕右上方仅显示当前任务的两行 HUD。
    private static void CreateQuestHud(Transform parent)
    {
        GameObject root = CreateUiObject("Quest HUD", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-44f, -44f);
        rect.sizeDelta = new Vector2(430f, 120f);
        Text title = CreateText("Title", root.transform, 25, TextAnchor.UpperRight);
        title.fontStyle = FontStyle.Bold;
        Stretch(title.rectTransform);
        Text objective = CreateText("Objective", root.transform, 20, TextAnchor.LowerRight);
        Stretch(objective.rectTransform);
        root.AddComponent<QuestHud>().Configure(title, objective);
    }

    // 创建带 RectTransform 的 UI 空对象。
    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    // 创建使用系统默认字体的可读文本。
    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        GameObject root = CreateUiObject(name, parent);
        Text text = root.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.9f, 0.8f, 1f);
        return text;
    }

    // 创建可点击的继续按钮及其中央标签。
    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject root = CreateUiObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.33f, 0.26f, 0.15f, 1f);
        Button button = root.AddComponent<Button>();
        Text text = CreateText("Label", root.transform, 22, TextAnchor.MiddleCenter);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    // 让 RectTransform 填满父容器。
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
