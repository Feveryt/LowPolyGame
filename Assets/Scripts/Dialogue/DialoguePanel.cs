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

    // 当前节点生成的选项按钮，用于切换台词前回收。
    private readonly List<Button> optionButtons = new List<Button>();
    // 内容区域初始的左下偏移，用于恢复头像状态下的排版。
    private Vector2 dialogueContentOffsetMin;
    // 内容区域初始的右上偏移，用于恢复头像状态下的排版。
    private Vector2 dialogueContentOffsetMax;

    // 缓存静态 UI 引用并在开局保持隐藏。
    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (dialogueContentRoot != null)
        {
            dialogueContentOffsetMin = dialogueContentRoot.offsetMin;
            dialogueContentOffsetMax = dialogueContentRoot.offsetMax;
        }

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
        dialogueText.text = text;
        SetPortrait(portrait, portraitSide);
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

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            Button button = Instantiate(choiceButtonPrefab, optionsRoot);
            button.name = $"Choice {index + 1}";
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = choices[i].Text;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));
            optionButtons.Add(button);
        }

        if (optionButtons.Count > 0)
            Focus(optionButtons[0].gameObject);
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

        dialogueContentRoot.offsetMin = hasPortrait ? dialogueContentOffsetMin : new Vector2(0f, dialogueContentOffsetMin.y);
        dialogueContentRoot.offsetMax = hasPortrait ? dialogueContentOffsetMax : new Vector2(0f, dialogueContentOffsetMax.y);
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
