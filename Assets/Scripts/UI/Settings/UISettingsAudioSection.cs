using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板的"声音"卡片构建器：在面板标题下方生成一个自带圆角背板的独立分组，
/// 内含分组标题与三行设置（全部静音 / 音效 / 音乐）。
/// 三行共用同一套列宽（标签列 / 滑条列 / 数值列），保证左边缘与滑条起点严格对齐；
/// 卡片与按钮的纵向排布由 UISettings 统一按这里的常量计算，避免两处硬编码互相冲突。
/// 滑动条采用 uGUI 标准结构（Background / Fill Area / Handle Slide Area），数值读写走 AudioVolumeSettings。
/// </summary>
public static class UISettingsAudioSection
{
    // —— 卡片在设置面板中的位置与尺寸（参考分辨率 1920×1080 下的像素值）——
    /// <summary>卡片上边缘距面板顶部的距离。</summary>
    public const float CardTopOffset = 96f;
    /// <summary>卡片高度。</summary>
    public const float CardHeight = 184f;
    /// <summary>卡片左右两侧距面板边缘的距离。</summary>
    public const float CardSideInset = 96f;

    // —— 卡片内部留白与行距（Build 内部使用）——
    private const float PaddingLeftRight = 18f;
    private const float PaddingTop = 14f;
    private const float HeaderHeight = 26f;
    private const float HeaderGap = 8f;
    private const float RowHeight = 34f;
    private const float RowGap = 6f;

    // —— 三行共用的列宽 ——
    private const float LabelColumnWidth = 84f;
    private const float LabelToSliderGap = 8f;
    private const float ValueColumnWidth = 56f;
    private const float ValueToSliderGap = 8f;

    // —— 滑条比例 ——
    private const float SliderHeight = 14f;
    private const float KnobWidth = 20f;
    private const float KnobHeight = 26f;

    // —— 静音勾选框 ——
    private const float CheckboxSize = 26f;
    private const float CheckmarkSize = 13f;
    private const float MuteHitWidth = 200f;

    // —— 配色：深色玻璃背板 + 暖色文字，与石质主题既有容器保持一致 ——
    private static readonly Color CardFill = new Color(0.05f, 0.045f, 0.04f, 0.42f);
    private static readonly Color LabelColor = new Color(0.92f, 0.90f, 0.85f);
    private static readonly Color ValueColor = new Color(0.96f, 0.87f, 0.62f);
    private static readonly Color HeaderColor = new Color(0.95f, 0.88f, 0.68f);
    private static readonly Color RuleColor = new Color(0.78f, 0.72f, 0.60f, 0.30f);
    private static readonly Color KnobColor = new Color(0.96f, 0.90f, 0.72f);
    private static readonly Color CheckboxFill = new Color(0.10f, 0.09f, 0.08f, 0.85f);
    private static readonly Color CheckmarkColor = new Color(0.62f, 0.84f, 0.42f);

    /// <summary>卡片下边缘相对面板顶部的距离（供面板排布按钮使用）。</summary>
    public static float CardBottomOffset => CardTopOffset + CardHeight;

    /// <summary>
    /// 在设置面板顶部（标题下方）构建"声音"卡片，返回卡片根节点。
    /// </summary>
    public static RectTransform Build(RectTransform parent, StoneUiTheme theme, TMP_FontAsset font)
    {
        TMP_FontAsset useFont = font != null ? font : (theme != null ? theme.ChineseFont : null);

        RectTransform card = UIRuntimeGraphics.CreateRoundedBackdrop(parent, "Sound Settings Card", CardFill);
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.sizeDelta = new Vector2(CardSideInset * -2f, CardHeight);
        card.anchoredPosition = new Vector2(0f, -CardTopOffset);

        // 静音联动状态组件：静音时音效/音乐两行整体调暗且不可拖动。
        UISettingsAudioCard cardState = card.gameObject.AddComponent<UISettingsAudioCard>();

        float rowWidth = -(PaddingLeftRight * 2f);
        float cursor = -PaddingTop;

        BuildHeader(card, useFont, cursor);
        cursor -= HeaderHeight + HeaderGap;

        BuildMuteRow(card, useFont, cursor, rowWidth, cardState);
        cursor -= RowHeight + RowGap;

        BuildVolumeRow(card, theme, useFont, cursor, rowWidth, "音效",
            AudioVolumeSettings.SfxVolume, AudioVolumeSettings.SetSfxVolume, cardState);
        cursor -= RowHeight + RowGap;

        BuildVolumeRow(card, theme, useFont, cursor, rowWidth, "音乐",
            AudioVolumeSettings.BgmVolume, AudioVolumeSettings.SetBgmVolume, cardState);

        cardState.Refresh();
        return card;
    }

    // 构建分组标题：居中文字 + 左右两条细分隔线（替代素材包里尺寸不匹配的飘带图标）。
    private static void BuildHeader(RectTransform card, TMP_FontAsset font, float topOffset)
    {
        const float textWidth = 96f;
        const float ruleMargin = 16f;
        float centerY = topOffset - HeaderHeight * 0.5f;

        RectTransform title = CreateTextNode(card, "Header", "声音", 20f, textWidth, font, HeaderColor);
        title.anchorMin = new Vector2(0.5f, 1f);
        title.anchorMax = new Vector2(0.5f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(textWidth, HeaderHeight);
        title.anchoredPosition = new Vector2(0f, topOffset);
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // 左分隔线：从中线向左延伸到卡片左内边距。
        RectTransform leftRule = CreateRule(card, "Rule Left");
        leftRule.anchorMin = new Vector2(0f, 1f);
        leftRule.anchorMax = new Vector2(0.5f, 1f);
        leftRule.offsetMin = new Vector2(PaddingLeftRight, centerY - 1f);
        leftRule.offsetMax = new Vector2(-(textWidth * 0.5f + ruleMargin), centerY + 1f);

        // 右分隔线：从中线向右延伸到卡片右内边距。
        RectTransform rightRule = CreateRule(card, "Rule Right");
        rightRule.anchorMin = new Vector2(0.5f, 1f);
        rightRule.anchorMax = new Vector2(1f, 1f);
        rightRule.offsetMin = new Vector2(textWidth * 0.5f + ruleMargin, centerY - 1f);
        rightRule.offsetMax = new Vector2(-PaddingLeftRight, centerY + 1f);
    }

    // 构建"全部静音"开关行：勾选框与音量行同一左边缘起始，整行（勾选框 + 文字）可点击。
    private static void BuildMuteRow(RectTransform card, TMP_FontAsset font,
        float topOffset, float rowWidth, UISettingsAudioCard cardState)
    {
        RectTransform row = CreateRow(card, "Mute Row", topOffset, rowWidth);

        GameObject toggleObject = new GameObject("Mute Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.SetParent(row, false);
        toggleRect.anchorMin = new Vector2(0f, 0f);
        toggleRect.anchorMax = new Vector2(0f, 1f);
        toggleRect.pivot = new Vector2(0f, 0.5f);
        toggleRect.offsetMin = new Vector2(0f, 0f);
        toggleRect.offsetMax = new Vector2(MuteHitWidth, 0f);

        // 透明命中层：只负责接收点击，不参与视觉。
        Image hitArea = toggleObject.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        // 勾选框底。
        GameObject boxObject = new GameObject("Checkbox", typeof(RectTransform), typeof(Image));
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.SetParent(toggleRect, false);
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(CheckboxSize, CheckboxSize);
        boxRect.anchoredPosition = Vector2.zero;
        Image boxImage = boxObject.GetComponent<Image>();
        boxImage.sprite = UIRuntimeGraphics.RoundedPanel;
        boxImage.type = Image.Type.Sliced;
        boxImage.color = CheckboxFill;
        boxImage.raycastTarget = false;

        // 勾选标记（关闭时由 Toggle.graphic 自动隐藏）。
        GameObject markObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        RectTransform markRect = markObject.GetComponent<RectTransform>();
        markRect.SetParent(boxRect, false);
        markRect.anchorMin = new Vector2(0.5f, 0.5f);
        markRect.anchorMax = new Vector2(0.5f, 0.5f);
        markRect.pivot = new Vector2(0.5f, 0.5f);
        markRect.sizeDelta = new Vector2(CheckmarkSize, CheckmarkSize);
        markRect.anchoredPosition = Vector2.zero;
        Image markImage = markObject.GetComponent<Image>();
        markImage.sprite = UIRuntimeGraphics.RoundedPanel;
        markImage.type = Image.Type.Sliced;
        markImage.color = CheckmarkColor;
        markImage.raycastTarget = false;

        // 标签与音量行标签同一起始位置，保证整列左对齐。
        RectTransform label = CreateTextNode(row, "Label", "全部静音", 19f, 0f, font, LabelColor);
        label.anchorMin = new Vector2(0f, 0f);
        label.anchorMax = new Vector2(0f, 1f);
        label.offsetMin = new Vector2(CheckboxSize + 10f, 0f);
        label.offsetMax = new Vector2(LabelColumnWidth, 0f);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = hitArea;
        toggle.graphic = markImage;
        toggle.isOn = AudioVolumeSettings.Muted;

        // 用局部变量捕获，避免闭包读到后续可能被替换的引用。
        Toggle wiredToggle = toggle;
        wiredToggle.onValueChanged.AddListener(value =>
        {
            AudioVolumeSettings.SetMuted(value);
            AudioManager.Instance?.PlayUiConfirm();
            cardState.Refresh();
        });

        cardState.RegisterToggle(wiredToggle);
    }

    // 构建一行音量设置：左标签 + 标准滑动条 + 右侧百分比。
    private static void BuildVolumeRow(RectTransform card, StoneUiTheme theme, TMP_FontAsset font,
        float topOffset, float rowWidth, string label, float initialValue,
        System.Action<float> applyValue, UISettingsAudioCard cardState)
    {
        RectTransform row = CreateRow(card, label + " Row", topOffset, rowWidth);

        RectTransform labelRect = CreateTextNode(row, "Label", label, 19f, LabelColumnWidth, font, LabelColor);
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.offsetMin = new Vector2(0f, 0f);
        labelRect.offsetMax = new Vector2(LabelColumnWidth, 0f);

        RectTransform valueRect = CreateTextNode(row, "Value", string.Empty, 18f, ValueColumnWidth, font, ValueColor);
        TMP_Text valueText = valueRect.GetComponent<TextMeshProUGUI>();
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.offsetMin = new Vector2(-ValueColumnWidth, 0f);
        valueRect.offsetMax = new Vector2(0f, 0f);
        valueText.alignment = TextAlignmentOptions.Right;

        RectTransform sliderRect = BuildSlider(row, theme, initialValue, out Slider slider);
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.offsetMin = new Vector2(LabelColumnWidth + LabelToSliderGap, -SliderHeight * 0.5f);
        sliderRect.offsetMax = new Vector2(-(ValueColumnWidth + ValueToSliderGap), SliderHeight * 0.5f);

        TMP_Text capturedValue = valueText;
        slider.onValueChanged.AddListener(value =>
        {
            applyValue(value);
            capturedValue.text = FormatPercent(value);
        });
        capturedValue.text = FormatPercent(initialValue);

        cardState.RegisterVolumeRow(slider);
    }

    // 构建标准 uGUI 滑动条结构（轨道 / 填充区 / 手柄滑动区），返回滑条根节点。
    private static RectTransform BuildSlider(RectTransform row, StoneUiTheme theme, float initialValue, out Slider slider)
    {
        GameObject sliderObject = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.SetParent(row, false);

        slider = sliderObject.GetComponent<Slider>();

        // 轨道背景。
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.SetParent(sliderRect, false);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        ApplySprite(backgroundImage, theme != null ? theme.ResourceBackgroundSprite : null);
        backgroundImage.raycastTarget = false;

        // 填充区：左右各内缩手柄半径，避免填充条超出滑动手柄范围。
        float handleInset = KnobWidth * 0.5f;
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.SetParent(sliderRect, false);
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(handleInset, 0f);
        fillAreaRect.offsetMax = new Vector2(-handleInset, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(fillAreaRect, false);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        ApplySprite(fillImage, theme != null ? theme.StaminaFillSprite : null);
        fillImage.raycastTarget = false;

        // 手柄滑动区。
        GameObject slideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        RectTransform slideAreaRect = slideArea.GetComponent<RectTransform>();
        slideAreaRect.SetParent(sliderRect, false);
        slideAreaRect.anchorMin = Vector2.zero;
        slideAreaRect.anchorMax = Vector2.one;
        slideAreaRect.offsetMin = new Vector2(handleInset, 0f);
        slideAreaRect.offsetMax = new Vector2(-handleInset, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.SetParent(slideAreaRect, false);
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(KnobWidth, KnobHeight);
        Image handleImage = handle.GetComponent<Image>();
        // 素材包按钮九宫格边界高达 89/67 像素，缩到 20 像素手柄会严重错位，这里改用程序生成的圆角贴图。
        handleImage.sprite = UIRuntimeGraphics.RoundedPanel;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = KnobColor;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;

        return sliderRect;
    }

    // 创建一行容器：横向铺满卡片内宽、固定行高，自卡片顶部向下偏移 topOffset。
    private static RectTransform CreateRow(RectTransform card, string name, float topOffset, float rowWidth)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(card, false);
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(rowWidth, RowHeight);
        rowRect.anchoredPosition = new Vector2(0f, topOffset);
        return rowRect;
    }

    // 创建一条 2 像素高的细分隔线。
    private static RectTransform CreateRule(RectTransform parent, string name)
    {
        GameObject rule = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = rule.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = rule.GetComponent<Image>();
        image.color = RuleColor;
        image.raycastTarget = false;
        return rect;
    }

    // 创建文本节点；默认左对齐、纵向铺满父级，调用方按需覆盖锚点与宽度。
    private static RectTransform CreateTextNode(RectTransform parent, string name, string content,
        float fontSize, float width, TMP_FontAsset font, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(width, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        if (font != null)
            text.font = font;
        return rect;
    }

    // 百分比文案：0 到 100 的整数。
    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    // 赋予精灵；素材缺失时保持原样，避免出现不可见的空 Image。
    private static void ApplySprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }
}
