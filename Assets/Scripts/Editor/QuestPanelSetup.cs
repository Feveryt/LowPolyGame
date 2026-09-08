using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>生成石质任务面板及其列表、目标子预制体，并绑定运行时控制器需要的引用。</summary>
public static class QuestPanelSetup
{
    // 石质主题资源的固定位置。
    private const string ThemePath = "Assets/Resources/UI/StoneUiTheme.asset";
    // 任务面板运行时通过 Resources 加载的路径。
    private const string QuestPanelPath = "Assets/Resources/Prefabs/UI/QuestPanel.prefab";
    // 可复用的任务列表单元预制体路径。
    private const string QuestListItemPath = "Assets/Resources/Prefabs/UI/QuestListItem.prefab";
    // 可复用的任务目标单元预制体路径。
    private const string QuestObjectivePath = "Assets/Resources/Prefabs/UI/QuestObjectiveView.prefab";

    // 当前构建使用的石质主题配置。
    private static StoneUiTheme theme;

    /// <summary>从菜单或批处理模式生成任务面板预制体。</summary>
    [MenuItem("RPG/Setup/Create Quest Panel")]
    public static void CreateQuestPanel()
    {
        theme = AssetDatabase.LoadAssetAtPath<StoneUiTheme>(ThemePath);
        if (theme == null)
        {
            Debug.LogError("[QuestPanelSetup] Missing StoneUiTheme at " + ThemePath);
            return;
        }

        QuestListItem listItem = CreateQuestListItemPrefab();
        QuestObjectiveView objective = CreateQuestObjectivePrefab();
        CreateQuestPanelPrefab(listItem, objective);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QuestPanelSetup] Quest panel prefab created at " + QuestPanelPath);
    }

    // 创建左侧滚动列表使用的石质任务条目。
    private static QuestListItem CreateQuestListItemPrefab()
    {
        GameObject root = CreateUiObject("Quest List Item", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 78f);
        Image background = root.AddComponent<Image>();
        ApplySprite(background, theme.PanelSprite, new Color(0.42f, 0.4f, 0.34f, 1f));
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        Image selection = CreateImage("Selection", root.transform, theme.PanelSprite, new Color(0.78f, 0.62f, 0.16f, 0.34f));
        Stretch(selection.rectTransform, 3f);
        selection.raycastTarget = false;
        selection.transform.SetAsFirstSibling();

        TMP_Text title = CreateText("Title", root.transform, "任务标题", 22, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -10f), new Vector2(-124f, -12f), new Vector2(0f, 1f));
        TMP_Text status = CreateText("Status", root.transform, "进行中", 17, TextAlignmentOptions.BottomRight, theme.ChineseFont);
        SetRect(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 8f), new Vector2(-18f, 26f), new Vector2(1f, 0f));

        QuestListItem item = root.AddComponent<QuestListItem>();
        SerializedObject serialized = new SerializedObject(item);
        SetReference(serialized, "button", button);
        SetReference(serialized, "selectionFrame", selection);
        SetReference(serialized, "titleText", title);
        SetReference(serialized, "statusText", status);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, QuestListItemPath);
        return AssetDatabase.LoadAssetAtPath<QuestListItem>(QuestListItemPath);
    }

    // 创建右侧详情中使用的顺序目标条目。
    private static QuestObjectiveView CreateQuestObjectivePrefab()
    {
        GameObject root = CreateUiObject("Quest Objective", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(590f, 46f);
        Image background = root.AddComponent<Image>();
        ApplySprite(background, theme.PanelSprite, new Color(0.18f, 0.2f, 0.18f, 0.78f));
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 46f;
        layout.preferredHeight = 46f;
        TMP_Text text = CreateText("Text", root.transform, "目标", 20, TextAlignmentOptions.MidlineLeft, theme.ChineseFont);
        text.textWrappingMode = TextWrappingModes.Normal;
        Stretch(text.rectTransform, 14f);

        QuestObjectiveView view = root.AddComponent<QuestObjectiveView>();
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "background", background);
        SetReference(serialized, "objectiveText", text);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, QuestObjectivePath);
        return AssetDatabase.LoadAssetAtPath<QuestObjectiveView>(QuestObjectivePath);
    }

    // 创建不含 Canvas 的全屏模态面板，供剧情 UI 启动器挂入既有画布。
    private static void CreateQuestPanelPrefab(QuestListItem listItemPrefab, QuestObjectiveView objectivePrefab)
    {
        GameObject root = CreateUiObject("Quest Panel", null);
        Stretch(root.GetComponent<RectTransform>(), 0f);
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        QuestPanel panel = root.AddComponent<QuestPanel>();

        Image dim = CreateImage("Dim", root.transform, null, new Color(0.015f, 0.02f, 0.025f, 0.78f));
        Stretch(dim.rectTransform, 0f);

        GameObject window = CreateUiObject("Stone Window", root.transform);
        SetRect(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 720f), new Vector2(0.5f, 0.5f));
        Image windowImage = window.AddComponent<Image>();
        ApplySprite(windowImage, theme.PanelSprite);

        CreateTitle(window.transform);
        Button activeButton = CreateButton("Active Filter", window.transform, "进行中", new Vector2(-468f, 224f), new Vector2(170f, 42f));
        Button completedButton = CreateButton("Completed Filter", window.transform, "已完成", new Vector2(-284f, 224f), new Vector2(170f, 42f));
        Image activeSelection = CreateSelection("Active Selection", activeButton.transform);
        Image completedSelection = CreateSelection("Completed Selection", completedButton.transform);
        Button closeButton = CreateButton("Close Button", window.transform, "关闭", new Vector2(490f, 290f), new Vector2(130f, 44f));

        ScrollRect listScrollRect = CreateTaskScrollView(window.transform, out RectTransform listContent);
        GameObject detail = CreateDetailPanel(window.transform, out TMP_Text title, out TMP_Text status, out TMP_Text description,
            out TMP_Text empty, out RectTransform objectiveContent);

        SerializedObject serialized = new SerializedObject(panel);
        SetReference(serialized, "canvasGroup", canvasGroup);
        SetReference(serialized, "listScrollRect", listScrollRect);
        SetReference(serialized, "listContent", listContent);
        SetReference(serialized, "listItemPrefab", listItemPrefab);
        SetReference(serialized, "objectiveContent", objectiveContent);
        SetReference(serialized, "objectivePrefab", objectivePrefab);
        SetReference(serialized, "activeFilterButton", activeButton);
        SetReference(serialized, "completedFilterButton", completedButton);
        SetReference(serialized, "closeButton", closeButton);
        SetReference(serialized, "titleText", title);
        SetReference(serialized, "statusText", status);
        SetReference(serialized, "descriptionText", description);
        SetReference(serialized, "emptyStateText", empty);
        SetReference(serialized, "activeFilterSelection", activeSelection);
        SetReference(serialized, "completedFilterSelection", completedSelection);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, QuestPanelPath);
    }

    // 创建带石质背景和纵向布局的左侧任务滚动列表。
    private static ScrollRect CreateTaskScrollView(Transform parent, out RectTransform content)
    {
        GameObject root = CreateUiObject("Quest List", parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(54f, 52f), new Vector2(510f, 430f), new Vector2(0f, 0f));
        Image frame = root.AddComponent<Image>();
        ApplySprite(frame, theme.PanelSprite, new Color(0.34f, 0.32f, 0.25f, 0.96f));
        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 26f;

        GameObject viewport = CreateUiObject("Viewport", root.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 12f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        content = CreateUiObject("Content", viewport.transform).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content;
        return scrollRect;
    }

    // 创建任务标题、说明、状态和顺序目标的右侧详情区域。
    private static GameObject CreateDetailPanel(Transform parent, out TMP_Text title, out TMP_Text status, out TMP_Text description,
        out TMP_Text empty, out RectTransform objectiveContent)
    {
        GameObject root = CreateUiObject("Quest Detail", parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-54f, 52f), new Vector2(600f, 430f), new Vector2(1f, 0f));
        Image frame = root.AddComponent<Image>();
        ApplySprite(frame, theme.PanelSprite, new Color(0.34f, 0.32f, 0.25f, 0.96f));

        title = CreateText("Title", root.transform, "任务标题", 31, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -28f), new Vector2(-150f, -8f), new Vector2(0f, 1f));
        status = CreateText("Status", root.transform, "进行中", 20, TextAlignmentOptions.TopRight, theme.ChineseFont);
        SetRect(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(280f, -30f), new Vector2(-28f, -8f), new Vector2(1f, 1f));
        description = CreateText("Description", root.transform, "剧情说明", 21, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        description.textWrappingMode = TextWrappingModes.Normal;
        SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -82f), new Vector2(-30f, -150f), new Vector2(0f, 1f));
        TMP_Text objectiveLabel = CreateText("Objective Label", root.transform, "目标", 22, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetRect(objectiveLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -160f), new Vector2(-30f, -132f), new Vector2(0f, 1f));

        objectiveContent = CreateUiObject("Objectives", root.transform).GetComponent<RectTransform>();
        SetRect(objectiveContent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -92f), new Vector2(-44f, -210f), new Vector2(0.5f, 0.5f));
        VerticalLayoutGroup layout = objectiveContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        empty = CreateText("Empty State", root.transform, "尚未接取任务", 25, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(empty.rectTransform, 44f);
        title.gameObject.SetActive(false);
        status.gameObject.SetActive(false);
        description.gameObject.SetActive(false);
        objectiveContent.gameObject.SetActive(false);
        return root;
    }

    // 创建标题飘带与居中文本。
    private static void CreateTitle(Transform parent)
    {
        Image ribbon = CreateImage("Title Ribbon", parent, theme.TitleSprite, Color.white);
        SetRect(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(390f, 74f), new Vector2(0.5f, 1f));
        TMP_Text title = CreateText("Title", ribbon.transform, "任务", 31, TextAlignmentOptions.Center, theme.DisplayFont);
        Stretch(title.rectTransform, 12f);
    }

    // 创建可用于筛选和关闭的绿色石质按钮。
    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, new Vector2(0.5f, 0.5f));
        Image image = root.AddComponent<Image>();
        ApplySprite(image, theme.PrimaryButtonSprite);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Text", root.transform, label, 21, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    // 创建覆盖在筛选按钮上的石质选中框。
    private static Image CreateSelection(string name, Transform parent)
    {
        Image selection = CreateImage(name, parent, null, new Color(0.79f, 0.62f, 0.15f, 0.22f));
        Stretch(selection.rectTransform, 1f);
        selection.raycastTarget = false;
        selection.transform.SetAsFirstSibling();
        return selection;
    }

    // 创建基础 UI 对象并继承父节点的局部坐标系。
    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    // 创建统一的 TMP 文本节点。
    private static TMP_Text CreateText(string name, Transform parent, string content, int fontSize, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        TextMeshProUGUI text = CreateUiObject(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.9f, 0.8f, 1f);
        text.raycastTarget = false;
        return text;
    }

    // 创建并按九宫格规则配置石质 Image。
    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = CreateUiObject(name, parent).AddComponent<Image>();
        ApplySprite(image, sprite, color);
        return image;
    }

    // 设置精灵与适合九宫格缩放的 Image 类型。
    private static void ApplySprite(Image image, Sprite sprite, Color? color = null)
    {
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color ?? (sprite != null ? Color.white : new Color(0.1f, 0.12f, 0.15f, 1f));
    }

    // 将矩形拉伸到父节点并保留内边距。
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    // 设置锚点、尺寸和局部位置。
    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 将编辑器引用写入私有序列化字段。
    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    // 将临时 UI 根节点保存为预制体并清理编辑器对象。
    private static void SavePrefab(GameObject root, string path)
    {
        string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            Directory.CreateDirectory(directory);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
