using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 自动生成的 UGUI 对话面板。
/// 未绑定预制体时仍可显示姓名、台词、头像、继续按钮与玩家选项。
/// </summary>
public sealed class DialoguePanel : MonoBehaviour
{
    // 控制整个面板显示和射线交互的画布组。
    private CanvasGroup canvasGroup;
    // 发言角色名称文本。
    private Text speakerNameText;
    // 当前台词文本。
    private Text dialogueText;
    // 左侧头像图片。
    private Image leftPortrait;
    // 右侧头像图片。
    private Image rightPortrait;
    // 推进无选项台词的按钮。
    private Button continueButton;
    // 选项按钮的垂直布局父节点。
    private RectTransform optionsRoot;
    // 已创建的选项按钮，用于下一句前清理。
    private readonly List<Button> optionButtons = new List<Button>();

    // 创建 UI 层级并将其保持为全局面板。
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildUi();
        Hide();
    }

    /// <summary>显示一条台词及其发言角色头像。</summary>
    public void ShowLine(string speakerName, string text, Sprite portrait, DialoguePortraitSide portraitSide, Action onContinue)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        speakerNameText.text = speakerName;
        dialogueText.text = text;
        SetPortrait(portrait, portraitSide);
        ClearOptions();
        continueButton.gameObject.SetActive(true);
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() => onContinue?.Invoke());
        Focus(continueButton.gameObject);
    }

    /// <summary>显示当前 NPC 台词后的玩家回答选项。</summary>
    public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<int> onChoiceSelected)
    {
        ClearOptions();
        continueButton.gameObject.SetActive(false);

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            Button button = CreateOptionButton(choices[i].Text);
            button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));
            optionButtons.Add(button);
        }

        if (optionButtons.Count > 0)
            Focus(optionButtons[0].gameObject);
    }

    /// <summary>隐藏对话面板并清除旧选项。</summary>
    public void Hide()
    {
        if (canvasGroup == null)
            return;

        ClearOptions();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // 按说话者配置显示单侧头像，另一侧始终隐藏。
    private void SetPortrait(Sprite portrait, DialoguePortraitSide side)
    {
        leftPortrait.gameObject.SetActive(portrait != null && side == DialoguePortraitSide.Left);
        rightPortrait.gameObject.SetActive(portrait != null && side == DialoguePortraitSide.Right);
        if (side == DialoguePortraitSide.Left)
            leftPortrait.sprite = portrait;
        else
            rightPortrait.sprite = portrait;
    }

    // 回收上一节点生成的所有选项按钮。
    private void ClearOptions()
    {
        foreach (Button button in optionButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        optionButtons.Clear();
    }

    // 让键盘和手柄 UI Submit 始终作用到当前可用按钮。
    private static void Focus(GameObject target)
    {
        EventSystem.current?.SetSelectedGameObject(target);
    }

    // 使用基础 UGUI 控件构建可运行的默认对话界面。
    private void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image box = CreateImage(transform, "Dialogue Box", new Color(0.04f, 0.05f, 0.08f, 0.92f));
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = new Vector2(0.12f, 0.06f);
        boxRect.anchorMax = new Vector2(0.88f, 0.34f);
        boxRect.offsetMin = Vector2.zero;
        boxRect.offsetMax = Vector2.zero;

        speakerNameText = CreateText(box.transform, "Speaker Name", 28, TextAnchor.UpperLeft);
        SetStretch(speakerNameText.rectTransform, new Vector2(28f, -46f), new Vector2(-28f, -8f));
        speakerNameText.color = new Color(1f, 0.86f, 0.42f);

        dialogueText = CreateText(box.transform, "Dialogue Text", 26, TextAnchor.UpperLeft);
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        SetStretch(dialogueText.rectTransform, new Vector2(28f, 52f), new Vector2(-28f, -52f));

        continueButton = CreateButton(box.transform, "Continue Button", "继续");
        RectTransform continueRect = continueButton.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-24f, 14f);
        continueRect.sizeDelta = new Vector2(120f, 36f);

        var options = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        options.transform.SetParent(box.transform, false);
        optionsRoot = options.GetComponent<RectTransform>();
        optionsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRoot.pivot = new Vector2(0.5f, 0.5f);
        optionsRoot.sizeDelta = new Vector2(720f, 200f);
        var layout = options.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        options.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        leftPortrait = CreatePortrait("Left Portrait", DialoguePortraitSide.Left);
        rightPortrait = CreatePortrait("Right Portrait", DialoguePortraitSide.Right);
    }

    // 创建固定在对话框左右两侧的头像槽位。
    private Image CreatePortrait(string name, DialoguePortraitSide side)
    {
        Image image = CreateImage(transform, name, Color.white);
        RectTransform rect = image.rectTransform;
        float anchorX = side == DialoguePortraitSide.Left ? 0.02f : 0.98f;
        rect.anchorMin = new Vector2(anchorX, 0.06f);
        rect.anchorMax = new Vector2(anchorX, 0.06f);
        rect.pivot = new Vector2(side == DialoguePortraitSide.Left ? 0f : 1f, 0f);
        rect.sizeDelta = new Vector2(210f, 210f);
        image.preserveAspect = true;
        return image;
    }

    // 创建一枚自适应高度的玩家选项按钮。
    private Button CreateOptionButton(string text)
    {
        Button button = CreateButton(optionsRoot, "Choice", text);
        var layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = 48f;
        return button;
    }

    // 创建带默认背景的 UGUI Image。
    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // 创建使用 Unity 内置字体的 UGUI Text。
    private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        Text text = item.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    // 创建带文本子节点的基础按钮。
    private static Button CreateButton(Transform parent, string name, string label)
    {
        Image image = CreateImage(parent, name, new Color(0.17f, 0.28f, 0.38f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.27f, 0.45f, 0.58f, 1f);
        colors.pressedColor = new Color(0.1f, 0.18f, 0.25f, 1f);
        button.colors = colors;
        Text text = CreateText(image.transform, "Label", 22, TextAnchor.MiddleCenter);
        text.text = label;
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    // 以父节点四边的像素偏移铺满 RectTransform。
    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
