using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 场景内静态搭建的石质对话面板，负责显示台词、头像、继续操作与动态选项。
/// </summary>
public sealed class DialoguePanel : MonoBehaviour
{
    // 控制整张对话画布可见性和射线交互的画布组。
    [Header("Panel References")]
    [SerializeField] private CanvasGroup canvasGroup;
    // 对话内容区域，用于在无头像时扩展台词宽度。
    [SerializeField] private RectTransform dialogueContentRoot;
    // 发言角色名称文本。
    [SerializeField] private TMP_Text speakerNameText;
    // 当前台词内容文本。
    [SerializeField] private TMP_Text dialogueText;
    // 左侧头像框容器。
    [SerializeField] private GameObject leftPortraitFrame;
    // 右侧头像框容器。
    [SerializeField] private GameObject rightPortraitFrame;
    // 左侧头像图片。
    [SerializeField] private Image leftPortrait;
    // 右侧头像图片。
    [SerializeField] private Image rightPortrait;
    // 推进无选项台词的确认按钮。
    [SerializeField] private Button continueButton;
    // 动态选项按钮的父节点。
    [SerializeField] private RectTransform optionsRoot;
    // 每个对话选项复用的石质按钮预制体。
    [SerializeField] private Button choiceButtonPrefab;

    // 石质主题提供统一中文字体，避免运行时 UI 使用默认字体。
    private StoneUiTheme stoneUiTheme;

    // 当前节点生成的选项按钮，用于切换台词前回收。
    private readonly List<Button> optionButtons = new List<Button>();
    // 缓存静态 UI 引用并在开局保持隐藏。
    private void Awake()
    {
        stoneUiTheme = Resources.Load<StoneUiTheme>("UI/StoneUiTheme");
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        Hide();
    }

    /// <summary>显示一条台词、角色名称和可选的左右头像。</summary>
    public void ShowLine(string speakerName, string text, Sprite portrait, DialoguePortraitSide portraitSide, Action onContinue)
    {
        if (!HasRequiredReferences())
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        speakerNameText.text = speakerName;
        dialogueText.text = text ?? string.Empty;
        SetPortrait(portrait, portraitSide);
        ConfigureLineLayout(false);
        ApplyTextStyle(speakerNameText, 26, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(dialogueText, 24, TextAlignmentOptions.TopLeft);
        dialogueText.maxVisibleCharacters = int.MaxValue;
        ClearOptions();
        continueButton.gameObject.SetActive(true);
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() => onContinue?.Invoke());
        Focus(continueButton.gameObject);
    }

    /// <summary>显示当前 NPC 台词对应的玩家选择按钮。</summary>
    public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<int> onChoiceSelected)
    {
        if (!HasRequiredReferences())
            return;

        ClearOptions();
        continueButton.gameObject.SetActive(false);
        if (choices == null || choiceButtonPrefab == null || optionsRoot == null)
            return;

        ConfigureLineLayout(true);

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            Button button = Instantiate(choiceButtonPrefab, optionsRoot);
            button.name = $"Choice {index + 1}";
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = choices[i].Text;
                ApplyTextStyle(label, 23, TextAlignmentOptions.Center);
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));
            optionButtons.Add(button);
        }

        if (optionButtons.Count > 0)
        {
            Canvas.ForceUpdateCanvases();
            Focus(optionButtons[0].gameObject);
        }
    }

    /// <summary>隐藏对话面板并回收当前节点的所有选项按钮。</summary>
    public void Hide()
    {
        ClearOptions();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // 按说话者配置显示单侧头像，并在无头像时扩展台词区域。
    private void SetPortrait(Sprite portrait, DialoguePortraitSide side)
    {
        bool hasPortrait = portrait != null;
        if (leftPortraitFrame != null)
            leftPortraitFrame.SetActive(hasPortrait && side == DialoguePortraitSide.Left);
        if (rightPortraitFrame != null)
            rightPortraitFrame.SetActive(hasPortrait && side == DialoguePortraitSide.Right);

        if (leftPortrait != null && side == DialoguePortraitSide.Left)
            leftPortrait.sprite = portrait;
        if (rightPortrait != null && side == DialoguePortraitSide.Right)
            rightPortrait.sprite = portrait;

        if (dialogueContentRoot == null)
            return;

        // 内容区必须收在对话框边框内。预制体历史尺寸使用了负偏移，
        // 会让角色名和台词越过顶部边框，并压缩出可见文本区域。
        float leftInset = hasPortrait && side == DialoguePortraitSide.Left ? 210f : 48f;
        float rightInset = hasPortrait && side == DialoguePortraitSide.Right ? 210f : 48f;
        dialogueContentRoot.anchorMin = Vector2.zero;
        dialogueContentRoot.anchorMax = Vector2.one;
        dialogueContentRoot.offsetMin = new Vector2(leftInset, 18f);
        dialogueContentRoot.offsetMax = new Vector2(-rightInset, -42f);
    }

    // 将台词区与底部选项区分开，避免选项按钮覆盖正在显示的台词。
    private void ConfigureLineLayout(bool showingChoices)
    {
        if (speakerNameText != null)
        {
            RectTransform speakerRect = speakerNameText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = Vector2.zero;
            speakerRect.sizeDelta = new Vector2(0f, 34f);
        }

        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0f, showingChoices ? 82f : 54f);
            textRect.offsetMax = new Vector2(0f, -40f);
        }

        if (optionsRoot != null)
        {
            optionsRoot.anchorMin = new Vector2(0.5f, 0f);
            optionsRoot.anchorMax = new Vector2(0.5f, 0f);
            optionsRoot.pivot = new Vector2(0.5f, 0f);
            optionsRoot.anchoredPosition = new Vector2(0f, 18f);
            optionsRoot.sizeDelta = new Vector2(700f, 0f);
        }
    }

    // 应用项目石质主题字体和可读的 TMP 文本设置。
    private void ApplyTextStyle(TMP_Text text, float fontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        if (stoneUiTheme != null && stoneUiTheme.ChineseFont != null)
        {
            text.font = stoneUiTheme.ChineseFont;
            // 预制体可能保留旧字体的材质；字体和材质必须成对切换，否则字形会不可见。
            text.fontSharedMaterial = stoneUiTheme.ChineseFont.material;
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = new Color(0.93f, 0.9f, 0.8f, 1f);
    }

    // 销毁上一条台词遗留的动态选项按钮。
    private void ClearOptions()
    {
        foreach (Button button in optionButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        optionButtons.Clear();
    }

    // 将键盘和手柄 UI 焦点切换到指定控件。
    private static void Focus(GameObject target)
    {
        EventSystem.current?.SetSelectedGameObject(target);
    }

    // 检查静态对话预制体是否已经正确完成 Inspector 绑定。
    private bool HasRequiredReferences()
    {
        if (canvasGroup != null && speakerNameText != null && dialogueText != null && continueButton != null)
            return true;

        Debug.LogError($"[{nameof(DialoguePanel)}] Dialogue Canvas references are incomplete.", this);
        return false;
    }
}
