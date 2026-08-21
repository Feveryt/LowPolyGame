using UnityEngine;
using UnityEngine.UI;

/// <summary>运行时生成的 NPC 交互按键提示。</summary>
public sealed class DialoguePromptUI : MonoBehaviour
{
    // 懒加载的全局提示实例。
    private static DialoguePromptUI instance;
    // 显示按键和 NPC 名称的文本控件。
    private Text promptText;
    // 控制提示可见性和射线阻挡的画布组。
    private CanvasGroup canvasGroup;

    /// <summary>全局交互提示实例。</summary>
    public static DialoguePromptUI Instance
    {
        get
        {
            if (instance == null)
            {
                var root = new GameObject(nameof(DialoguePromptUI));
                instance = root.AddComponent<DialoguePromptUI>();
            }

            return instance;
        }
    }

    // 创建提示画布，并保证它跨场景保留。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        Hide();
    }

    /// <summary>显示当前可交互 NPC 的按键提示。</summary>
    public void Show(string message)
    {
        promptText.text = message;
        canvasGroup.alpha = 1f;
    }

    /// <summary>隐藏交互提示。</summary>
    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    // 使用基础 UGUI 控件搭建不依赖预制体的底部提示。
    private void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;

        var label = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(transform, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.18f);
        rect.anchorMax = new Vector2(0.5f, 0.18f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 44f);

        promptText = label.GetComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 26;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
    }
}
