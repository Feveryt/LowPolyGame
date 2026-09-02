using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>在任意可玩场景自动创建《未署名的守卫》的开场文字和最小任务 HUD。</summary>
public static class UnsignedGuardianUiBootstrap
{
    // 在首个场景载入前创建常驻 UI，避免手工修改每个 Demo 场景。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/GameScene/Demo 1.unity")
            return;
        if (Object.FindFirstObjectByType<OpeningNarrative>() != null)
            return;
        GameObject canvasObject = new GameObject("Unsigned Guardian UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(canvasObject);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        CreateOpening(canvasObject.transform);
        CreateQuestHud(canvasObject.transform);
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
