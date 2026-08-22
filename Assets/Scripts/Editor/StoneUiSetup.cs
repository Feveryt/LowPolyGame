using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一次性将项目可运行界面替换为石质主题并完成 TMP、预制体和场景引用绑定的编辑器工具。
/// </summary>
public static class StoneUiSetup
{
    // 石质资源包根目录。
    private const string ThemeRoot = "Assets/ArtRes/UI Res/GUI Kit The Stone";
    // 运行时主题资源路径。
    private const string ThemeAssetPath = "Assets/Resources/UI/StoneUiTheme.asset";
    // 游戏开始场景路径。
    private const string GameStartScenePath = "Assets/Scenes/GameScene/GameStart.unity";
    // 测试游戏场景路径。
    private const string TestActScenePath = "Assets/Scenes/GameScene/TestAct.unity";
    // 背包槽位预制体路径。
    private const string InventorySlotPath = "Assets/Prefabs/UI/InventorySlot.prefab";
    // 背包面板预制体路径。
    private const string InventoryPanelPath = "Assets/Resource/Inventory/Inventory Panel.prefab";
    // 设置面板预制体路径。
    private const string SettingsCanvasPath = "Assets/Prefabs/UI/SettingsCanvas.prefab";
    // 对话选项按钮预制体路径。
    private const string DialogueChoicePath = "Assets/Prefabs/UI/DialogueChoiceButton.prefab";
    // 项目统一使用的中文 TMP 字体资产路径。
    private const string UnifiedFontAssetPath = "Assets/ArtRes/Fonts/FZYTK SDF.asset";

    // 当前构建使用的主题资源。
    private static StoneUiTheme theme;

    /// <summary>生成石质主题、预制体并更新 GameStart 和 TestAct 场景。</summary>
    [MenuItem("RPG/Setup/Build Stone UI")]
    public static void BuildStoneUi()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            theme = CreateOrUpdateTheme();
            CreateInventorySlotPrefab();
            CreateInventoryPanelPrefab();
            CreateSettingsCanvasPrefab();
            CreateDialogueChoicePrefab();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        SetupGameStartScene();
        SetupTestActScene();
        AssignItemIcons();
        AssetDatabase.SaveAssets();
        Debug.Log("[StoneUiSetup] Stone UI setup completed.");
    }

    // 创建可由运行时加载器读取的统一主题配置。
    private static StoneUiTheme CreateOrUpdateTheme()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");
        TMP_FontAsset unifiedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnifiedFontAssetPath);
        unifiedFont ??= TMP_Settings.defaultFontAsset;

        StoneUiTheme result = AssetDatabase.LoadAssetAtPath<StoneUiTheme>(ThemeAssetPath);
        if (result == null)
        {
            result = ScriptableObject.CreateInstance<StoneUiTheme>();
            AssetDatabase.CreateAsset(result, ThemeAssetPath);
        }

        SerializedObject serializedTheme = new SerializedObject(result);
        SetObject(serializedTheme, "chineseFont", unifiedFont);
        SetObject(serializedTheme, "displayFont", unifiedFont);
        SetObject(serializedTheme, "panelSprite", Load<Sprite>("ResourceData/Sprites/Popup/popup_bg.png"));
        SetObject(serializedTheme, "titleSprite", Load<Sprite>("ResourceData/Sprites/_Common/title_ribbon_brown.png"));
        SetObject(serializedTheme, "primaryButtonSprite", Load<Sprite>("ResourceData/Sprites/_Common/_Buttons/btn_color_green.png"));
        SetObject(serializedTheme, "dangerButtonSprite", Load<Sprite>("ResourceData/Sprites/_Common/_Buttons/btn_color_red.png"));
        SetObject(serializedTheme, "itemSlotSprite", Load<Sprite>("ResourceData/Sprites/Item/item_frame.png"));
        SetObject(serializedTheme, "selectionSprite", Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/slider_skill_frame_yellow.png"));
        SetObject(serializedTheme, "resourceBackgroundSprite", Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/user_info_slider_bg.png"));
        SetObject(serializedTheme, "healthFillSprite", Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/slider_skill_fill_red.png"));
        SetObject(serializedTheme, "staminaFillSprite", Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/slider_skill_fill_green.png"));
        SetObject(serializedTheme, "manaFillSprite", Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/slider_skill_fill_purple.png"));
        SetObject(serializedTheme, "coinSprite", Load<Sprite>("ResourceData/Sprites/_Common/_Status/status_icon_coin.png"));
        serializedTheme.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(result);
        return result;
    }

    // 创建复用的石质物品槽位预制体。
    private static void CreateInventorySlotPrefab()
    {
        GameObject root = CreateUiObject("InventorySlot", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(96f, 96f);
        Image background = root.AddComponent<Image>();
        ApplySprite(background, theme.ItemSlotSprite);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        Image icon = CreateImage("Icon", root.transform, null, Color.white);
        Stretch(icon.rectTransform, 12f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Image selected = CreateImage("Selected Frame", root.transform, theme.SelectionSprite, new Color(1f, 0.82f, 0.24f, 1f));
        Stretch(selected.rectTransform, 2f);
        selected.raycastTarget = false;

        TMP_Text quantity = CreateText("Quantity", root.transform, string.Empty, 20, TextAlignmentOptions.BottomRight, theme.DisplayFont);
        Stretch(quantity.rectTransform, 9f);
        quantity.raycastTarget = false;
        quantity.fontStyle = FontStyles.Bold;

        InventorySlotView view = root.AddComponent<InventorySlotView>();
        SerializedObject serializedView = new SerializedObject(view);
        SetObject(serializedView, "button", button);
        SetObject(serializedView, "icon", icon);
        SetObject(serializedView, "quantityText", quantity);
        SetObject(serializedView, "selectionImage", selected);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, InventorySlotPath);
    }

    // 创建石质背包 ScrollView、详情与操作按钮预制体。
    private static void CreateInventoryPanelPrefab()
    {
        InventorySlotView slotPrefab = AssetDatabase.LoadAssetAtPath<InventorySlotView>(InventorySlotPath);
        GameObject root = CreateUiObject("Inventory Panel", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetCentered(rootRect, new Vector2(1080f, 660f));
        Image panelImage = root.AddComponent<Image>();
        ApplySprite(panelImage, theme.PanelSprite);
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        UIInventory inventory = root.AddComponent<UIInventory>();

        CreateTitle("Title", root.transform, "背包", 42);
        CreateCoinLabel(root.transform, new Vector2(-40f, -42f), new Vector2(220f, 42f), TextAlignmentOptions.Right, out TMP_Text goldText);

        GameObject scrollRoot = CreateUiObject("Scroll View", root.transform);
        RectTransform scrollRect = scrollRoot.GetComponent<RectTransform>();
        SetAnchoredRect(scrollRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(570f, 500f), new Vector2(0f, 0.5f));
        Image scrollBackground = scrollRoot.AddComponent<Image>();
        ApplySprite(scrollBackground, theme.PanelSprite, new Color(1f, 1f, 1f, 0.68f));
        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 18f;

        GameObject viewportObject = CreateUiObject("Viewport", scrollRoot.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, 16f);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        GridLayoutGroup grid = contentObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.cellSize = new Vector2(92f, 92f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        ContentSizeFitter contentSize = contentObject.AddComponent<ContentSizeFitter>();
        contentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;

        GameObject detail = CreateUiObject("Item Detail Panel", root.transform);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        SetAnchoredRect(detailRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(390f, 500f), new Vector2(1f, 0.5f));
        Image detailImage = detail.AddComponent<Image>();
        ApplySprite(detailImage, theme.PanelSprite, new Color(1f, 1f, 1f, 0.78f));
        TMP_Text itemName = CreateText("Item Name", detail.transform, "未选择物品", 30, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetAnchoredRect(itemName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -26f), new Vector2(-26f, 42f), new Vector2(0f, 1f));
        TMP_Text itemType = CreateText("Item Type", detail.transform, string.Empty, 20, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetAnchoredRect(itemType.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -76f), new Vector2(-26f, 28f), new Vector2(0f, 1f));
        TMP_Text description = CreateText("Item Description", detail.transform, "请选择一个物品查看详情", 20, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        description.textWrappingMode = TextWrappingModes.Normal;
        SetAnchoredRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -122f), new Vector2(-26f, 172f), new Vector2(0f, 1f));
        TMP_Text quantityText = CreateText("Item Quantity", detail.transform, string.Empty, 20, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetAnchoredRect(quantityText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 110f), new Vector2(-26f, 30f), new Vector2(0f, 0f));
        Button useButton = CreateButton("Use Button", detail.transform, "使用", theme.PrimaryButtonSprite, new Vector2(26f, 24f), new Vector2(160f, 48f));
        Button dropButton = CreateButton("Drop Button", detail.transform, "丢弃", theme.DangerButtonSprite, new Vector2(204f, 24f), new Vector2(160f, 48f));

        SerializedObject serializedInventory = new SerializedObject(inventory);
        SetObject(serializedInventory, "panelRoot", root);
        SetObject(serializedInventory, "panelCanvasGroup", canvasGroup);
        SetObject(serializedInventory, "inventoryScrollRect", scroll);
        SetObject(serializedInventory, "slotContent", content);
        SetObject(serializedInventory, "slotPrefab", slotPrefab);
        SetObject(serializedInventory, "itemNameText", itemName);
        SetObject(serializedInventory, "itemTypeText", itemType);
        SetObject(serializedInventory, "itemDescriptionText", description);
        SetObject(serializedInventory, "itemQuantityText", quantityText);
        SetObject(serializedInventory, "goldText", goldText);
        SetObject(serializedInventory, "useButton", useButton);
        SetObject(serializedInventory, "dropButton", dropButton);
        serializedInventory.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, InventoryPanelPath);
    }

    // 创建跨场景复用的石质设置画布预制体。
    private static void CreateSettingsCanvasPrefab()
    {
        GameObject root = CreateUiObject("SettingsCanvas", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        AddCanvasScaler(root);
        root.AddComponent<GraphicRaycaster>();
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        UISettings settings = root.AddComponent<UISettings>();

        Image dim = CreateImage("Dim", root.transform, null, new Color(0.02f, 0.03f, 0.04f, 0.72f));
        Stretch(dim.rectTransform, 0f);
        GameObject panel = CreateUiObject("Settings Panel", root.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetCentered(panelRect, new Vector2(550f, 480f));
        Image panelImage = panel.AddComponent<Image>();
        ApplySprite(panelImage, theme.PanelSprite);
        CreateTitle("Title", panel.transform, "设置", 38);
        GameObject gameplayButtons = CreateUiObject("Gameplay Buttons", panel.transform);
        Stretch(gameplayButtons.GetComponent<RectTransform>(), 0f);
        Button continueButton = CreateButton("Continue Button", gameplayButtons.transform, "继续游戏", theme.PrimaryButtonSprite, new Vector2(125f, 244f), new Vector2(300f, 54f));
        Button saveButton = CreateButton("Save Button", gameplayButtons.transform, "保存游戏", theme.PrimaryButtonSprite, new Vector2(125f, 166f), new Vector2(300f, 54f));
        Button returnMenuButton = CreateButton("Return To Menu Button", gameplayButtons.transform, "回到菜单", theme.DangerButtonSprite, new Vector2(125f, 88f), new Vector2(300f, 54f));
        Button returnButton = CreateButton("Return Button", panel.transform, "返回", theme.PrimaryButtonSprite, new Vector2(125f, 166f), new Vector2(300f, 54f));

        SerializedObject serializedSettings = new SerializedObject(settings);
        SetObject(serializedSettings, "panelCanvasGroup", canvasGroup);
        SetObject(serializedSettings, "gameplayButtonsRoot", gameplayButtons);
        SetObject(serializedSettings, "continueButton", continueButton);
        SetObject(serializedSettings, "saveButton", saveButton);
        SetObject(serializedSettings, "returnToMenuButton", returnMenuButton);
        SetObject(serializedSettings, "returnButton", returnButton);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, SettingsCanvasPath);
    }

    // 创建对话中动态实例化的石质选项按钮预制体。
    private static void CreateDialogueChoicePrefab()
    {
        GameObject root = CreateUiObject("Dialogue Choice Button", null);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(680f, 54f);
        Image image = root.AddComponent<Image>();
        ApplySprite(image, theme.PrimaryButtonSprite);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateText("Label", root.transform, "选项", 23, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(label.rectTransform, 12f);
        label.raycastTarget = false;
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 54f;
        layout.preferredHeight = 58f;
        SavePrefab(root, DialogueChoicePath);
    }

    // 重建开始菜单场景中已有 Canvas 的石质视觉与按钮引用。
    private static void SetupGameStartScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameStartScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            GameObject canvasObject = CreateUiObject("Canvas", null);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddCanvasScaler(canvasObject);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        ClearChildren(canvas.transform);
        StartMenuController controller = canvas.GetComponent<StartMenuController>();
        if (controller == null)
            controller = canvas.gameObject.AddComponent<StartMenuController>();
        GameObject menu = CreateUiObject("Start Menu Panel", canvas.transform);
        Stretch(menu.GetComponent<RectTransform>(), 0f);
        CanvasGroup menuGroup = menu.AddComponent<CanvasGroup>();
        Image background = menu.AddComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.045f, 1f);

        GameObject titlePanel = CreateUiObject("Title Panel", menu.transform);
        RectTransform titlePanelRect = titlePanel.GetComponent<RectTransform>();
        SetAnchoredRect(titlePanelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(700f, 130f), new Vector2(0.5f, 0.5f));
        Image titleImage = titlePanel.AddComponent<Image>();
        ApplySprite(titleImage, theme.TitleSprite);
        TMP_Text title = CreateText("Title", titlePanel.transform, "RPG DEMO", 52, TextAlignmentOptions.Center, theme.DisplayFont);
        Stretch(title.rectTransform, 18f);
        Button startButton = CreateCenteredButton("Start Game Button", menu.transform, "开始游戏", theme.PrimaryButtonSprite, 72f);
        Button settingsButton = CreateCenteredButton("Settings Button", menu.transform, "设置", theme.PrimaryButtonSprite, 0f);
        Button quitButton = CreateCenteredButton("Quit Game Button", menu.transform, "退出游戏", theme.DangerButtonSprite, -72f);

        SerializedObject serializedController = new SerializedObject(controller);
        SetObject(serializedController, "menuCanvasGroup", menuGroup);
        SetObject(serializedController, "startButton", startButton);
        SetObject(serializedController, "settingsButton", settingsButton);
        SetObject(serializedController, "quitButton", quitButton);
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EnsureSettingsInstance(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 替换 TestAct 的 HUD、背包层级和静态对话界面。
    private static void SetupTestActScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TestActScenePath, OpenSceneMode.Single);
        Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (hudCanvas == null)
        {
            GameObject canvasObject = CreateUiObject("Canvas", null);
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddCanvasScaler(canvasObject);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        UIHUD hud = Object.FindFirstObjectByType<UIHUD>(FindObjectsInactive.Include);
        if (hud == null)
            hud = hudCanvas.gameObject.AddComponent<UIHUD>();
        ClearChildren(hud.transform);
        BuildHud(hud);

        UIInventory inventory = Object.FindFirstObjectByType<UIInventory>(FindObjectsInactive.Include);
        if (inventory != null)
            inventory.transform.SetParent(hudCanvas.transform, false);

        CreateDialogueCanvas();
        CreateDialoguePromptCanvas();
        EnsureSettingsInstance(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 搭建带头像框、资源条和金币显示的左上角色状态卡。
    private static void BuildHud(UIHUD hud)
    {
        RectTransform root = hud.transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        GameObject card = CreateUiObject("Player Status Card", hud.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        SetAnchoredRect(cardRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(500f, 190f), new Vector2(0f, 1f));
        Image cardImage = card.AddComponent<Image>();
        ApplySprite(cardImage, theme.PanelSprite, new Color(1f, 1f, 1f, 0.84f));
        Image portrait = CreateImage("Portrait Frame", card.transform, Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/user_info_profile_bg.png"), Color.white);
        SetAnchoredRect(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(116f, 116f), new Vector2(0f, 1f));
        Image portraitInner = CreateImage("Portrait", portrait.transform, Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/user_info_profile_default.png"), Color.white);
        Stretch(portraitInner.rectTransform, 9f);
        portraitInner.preserveAspect = true;

        Slider hp = CreateResourceBar(card.transform, "HP", new Vector2(132f, -28f), theme.HealthFillSprite, "生命", out TMP_Text hpText);
        Slider stamina = CreateResourceBar(card.transform, "Stamina", new Vector2(132f, -76f), theme.StaminaFillSprite, "体力", out TMP_Text staminaText);
        Slider mana = CreateResourceBar(card.transform, "Mana", new Vector2(132f, -124f), theme.ManaFillSprite, "蓝量", out TMP_Text manaText);
        CreateCoinLabel(card.transform, new Vector2(132f, 18f), new Vector2(230f, 34f), TextAlignmentOptions.Left, out TMP_Text goldText);

        SerializedObject serializedHud = new SerializedObject(hud);
        SetObject(serializedHud, "healthSlider", hp);
        SetObject(serializedHud, "staminaSlider", stamina);
        SetObject(serializedHud, "manaSlider", mana);
        SetObject(serializedHud, "healthValueText", hpText);
        SetObject(serializedHud, "staminaValueText", staminaText);
        SetObject(serializedHud, "manaValueText", manaText);
        SetObject(serializedHud, "goldValueText", goldText);
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    // 创建静态石质对话画布，并绑定 DialoguePanel 所需的所有 Inspector 引用。
    private static void CreateDialogueCanvas()
    {
        DialoguePanel oldPanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);

        GameObject root = CreateUiObject("Dialogue Canvas", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        AddCanvasScaler(root);
        root.AddComponent<GraphicRaycaster>();
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        DialoguePanel panel = root.AddComponent<DialoguePanel>();

        GameObject box = CreateUiObject("Dialogue Box", root.transform);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        SetAnchoredRect(boxRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1180f, 250f), new Vector2(0.5f, 0f));
        Image boxImage = box.AddComponent<Image>();
        ApplySprite(boxImage, theme.PanelSprite);
        CreateTitle("Title", box.transform, "对话", 28);

        GameObject content = CreateUiObject("Dialogue Content", box.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        Stretch(contentRect, 32f);
        contentRect.offsetMin = new Vector2(170f, 28f);
        contentRect.offsetMax = new Vector2(-170f, -42f);
        TMP_Text speaker = CreateText("Speaker Name", content.transform, string.Empty, 26, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        SetAnchoredRect(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 34f), new Vector2(0f, 1f));
        TMP_Text dialogue = CreateText("Dialogue Text", content.transform, string.Empty, 24, TextAlignmentOptions.TopLeft, theme.ChineseFont);
        dialogue.textWrappingMode = TextWrappingModes.Normal;
        SetAnchoredRect(dialogue.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -40f), new Vector2(0.5f, 0.5f));
        Button continueButton = CreateButton("Continue Button", box.transform, "继续", theme.PrimaryButtonSprite, new Vector2(960f, 18f), new Vector2(160f, 44f));

        GameObject options = CreateUiObject("Options", box.transform);
        RectTransform optionsRect = options.GetComponent<RectTransform>();
        SetAnchoredRect(optionsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(700f, 190f), new Vector2(0.5f, 0.5f));
        VerticalLayoutGroup layout = options.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = options.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject leftFrame = CreatePortraitFrame("Left Portrait Frame", root.transform, new Vector2(0.07f, 0f), new Vector2(1f, 0f), out Image leftPortrait);
        GameObject rightFrame = CreatePortraitFrame("Right Portrait Frame", root.transform, new Vector2(0.93f, 0f), new Vector2(0f, 0f), out Image rightPortrait);
        Button choicePrefab = AssetDatabase.LoadAssetAtPath<Button>(DialogueChoicePath);
        SerializedObject serializedPanel = new SerializedObject(panel);
        SetObject(serializedPanel, "canvasGroup", canvasGroup);
        SetObject(serializedPanel, "dialogueContentRoot", contentRect);
        SetObject(serializedPanel, "speakerNameText", speaker);
        SetObject(serializedPanel, "dialogueText", dialogue);
        SetObject(serializedPanel, "leftPortraitFrame", leftFrame);
        SetObject(serializedPanel, "rightPortraitFrame", rightFrame);
        SetObject(serializedPanel, "leftPortrait", leftPortrait);
        SetObject(serializedPanel, "rightPortrait", rightPortrait);
        SetObject(serializedPanel, "continueButton", continueButton);
        SetObject(serializedPanel, "optionsRoot", optionsRect);
        SetObject(serializedPanel, "choiceButtonPrefab", choicePrefab);
        serializedPanel.ApplyModifiedPropertiesWithoutUndo();
    }

    // 创建静态石质 NPC 交互提示画布。
    private static void CreateDialoguePromptCanvas()
    {
        DialoguePromptUI oldPrompt = Object.FindFirstObjectByType<DialoguePromptUI>(FindObjectsInactive.Include);
        if (oldPrompt != null)
            Object.DestroyImmediate(oldPrompt.gameObject);

        GameObject root = CreateUiObject("Dialogue Prompt Canvas", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        AddCanvasScaler(root);
        root.AddComponent<GraphicRaycaster>();
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        DialoguePromptUI prompt = root.AddComponent<DialoguePromptUI>();
        GameObject panel = CreateUiObject("Prompt Panel", root.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetAnchoredRect(panelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(420f, 52f), new Vector2(0.5f, 0.5f));
        Image panelImage = panel.AddComponent<Image>();
        ApplySprite(panelImage, theme.PanelSprite);
        TMP_Text text = CreateText("Prompt", panel.transform, string.Empty, 23, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(text.rectTransform, 12f);
        SerializedObject serializedPrompt = new SerializedObject(prompt);
        SetObject(serializedPrompt, "promptText", text);
        SetObject(serializedPrompt, "canvasGroup", canvasGroup);
        serializedPrompt.ApplyModifiedPropertiesWithoutUndo();
    }

    // 在需要测试的场景中放置设置预制体，供 SingletonPanel 保留首个运行时实例。
    private static void EnsureSettingsInstance(Scene scene)
    {
        UISettings existing = Object.FindFirstObjectByType<UISettings>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsCanvasPath);
        if (prefab != null)
            PrefabUtility.InstantiatePrefab(prefab, scene);
    }

    // 为四个测试物品绑定资源包中对应的石质图标。
    private static void AssignItemIcons()
    {
        AssignItemIcon("Assets/GameData/Definitions/Items/HealthPotion.asset", "ResourceData/Sprites/Item/item_img_potion_red.png");
        AssignItemIcon("Assets/GameData/Definitions/Items/ManaPotion.asset", "ResourceData/Sprites/Icons/No Shadow/128x128/icon_poiton_purple.png");
        AssignItemIcon("Assets/GameData/Definitions/Items/Herb.asset", "ResourceData/Sprites/Item/item_img_clover.png");
        AssignItemIcon("Assets/GameData/Definitions/Items/IronSword.asset", "ResourceData/Sprites/Icons/No Shadow/128x128/icon_sword_A.png");
    }

    // 设置单个物品定义资产的图标引用。
    private static void AssignItemIcon(string itemPath, string iconPath)
    {
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        if (item == null)
            return;

        SerializedObject serializedItem = new SerializedObject(item);
        SetObject(serializedItem, "icon", Load<Sprite>(iconPath));
        serializedItem.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    // 创建石质资源条并返回 Slider 与数值文本引用。
    private static Slider CreateResourceBar(Transform parent, string name, Vector2 position, Sprite fillSprite, string label, out TMP_Text valueText)
    {
        GameObject root = CreateUiObject(name + " Row", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetAnchoredRect(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(330f, 32f), new Vector2(0f, 1f));
        TMP_Text labelText = CreateText("Label", root.transform, label, 18, TextAlignmentOptions.Left, theme.ChineseFont);
        SetAnchoredRect(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(42f, 30f), new Vector2(0f, 0.5f));
        GameObject sliderObject = CreateUiObject("Resource Bar", root.transform);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        SetAnchoredRect(sliderRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(48f, 0f), new Vector2(0f, 20f), new Vector2(0f, 0.5f));
        Image background = sliderObject.AddComponent<Image>();
        ApplySprite(background, theme.ResourceBackgroundSprite);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.interactable = false;
        GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 4f);
        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        Image fillImage = fill.AddComponent<Image>();
        ApplySprite(fillImage, fillSprite);
        Stretch(fill.GetComponent<RectTransform>(), 0f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = background;
        valueText = CreateText("Value", sliderObject.transform, "100 / 100", 15, TextAlignmentOptions.Center, theme.DisplayFont);
        Stretch(valueText.rectTransform, 4f);
        valueText.raycastTarget = false;
        return slider;
    }

    // 创建带金币图标的数值标签。
    private static void CreateCoinLabel(Transform parent, Vector2 position, Vector2 size, TextAlignmentOptions alignment, out TMP_Text goldText)
    {
        GameObject root = CreateUiObject("Gold", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetAnchoredRect(rootRect, new Vector2(0f, 0f), new Vector2(0f, 0f), position, size, new Vector2(0f, 0f));
        Image coin = CreateImage("Coin Icon", root.transform, theme.CoinSprite, Color.white);
        SetAnchoredRect(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(0f, 0.5f));
        goldText = CreateText("Gold Text", root.transform, "0", 22, alignment, theme.DisplayFont);
        SetAnchoredRect(goldText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(36f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f));
    }

    // 创建对话头像框与内部图像。
    private static GameObject CreatePortraitFrame(string name, Transform parent, Vector2 anchor, Vector2 pivot, out Image portrait)
    {
        GameObject frame = CreateUiObject(name, parent);
        RectTransform rect = frame.GetComponent<RectTransform>();
        SetAnchoredRect(rect, anchor, anchor, new Vector2(0f, 135f), new Vector2(210f, 210f), pivot);
        Image frameImage = frame.AddComponent<Image>();
        ApplySprite(frameImage, Load<Sprite>("ResourceData/Sprites/_Common/_SliderBar/user_info_profile_bg.png"));
        portrait = CreateImage("Portrait", frame.transform, null, Color.white);
        Stretch(portrait.rectTransform, 14f);
        portrait.preserveAspect = true;
        return frame;
    }

    // 创建统一标题飘带和 TMP 标题文本。
    private static void CreateTitle(string name, Transform parent, string title, int size)
    {
        GameObject root = CreateUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchoredRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(360f, 70f), new Vector2(0.5f, 1f));
        Image image = root.AddComponent<Image>();
        ApplySprite(image, theme.TitleSprite);
        TMP_Text text = CreateText("Text", root.transform, title, size, TextAlignmentOptions.Center, theme.DisplayFont);
        Stretch(text.rectTransform, 12f);
    }

    // 创建固定于父节点左下角的石质文字按钮。
    private static Button CreateButton(string name, Transform parent, string label, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject root = CreateUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchoredRect(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), position, size, new Vector2(0f, 0f));
        Image image = root.AddComponent<Image>();
        ApplySprite(image, sprite);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Text", root.transform, label, 23, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(text.rectTransform, 10f);
        text.raycastTarget = false;
        return button;
    }

    // 创建居中的开始菜单按钮。
    private static Button CreateCenteredButton(string name, Transform parent, string label, Sprite sprite, float y)
    {
        GameObject root = CreateUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(310f, 56f), new Vector2(0.5f, 0.5f));
        Image image = root.AddComponent<Image>();
        ApplySprite(image, sprite);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Text", root.transform, label, 25, TextAlignmentOptions.Center, theme.ChineseFont);
        Stretch(text.rectTransform, 12f);
        text.raycastTarget = false;
        return button;
    }

    // 创建带 RectTransform 的 UI 节点。
    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    // 创建统一石质 Image。
    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = CreateUiObject(name, parent).AddComponent<Image>();
        ApplySprite(image, sprite, color);
        return image;
    }

    // 创建统一 TMP 文本。
    private static TMP_Text CreateText(string name, Transform parent, string content, int size, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        TextMeshProUGUI text = CreateUiObject(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        return text;
    }

    // 应用精灵并在支持时启用九宫格缩放。
    private static void ApplySprite(Image image, Sprite sprite, Color? color = null)
    {
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color ?? (sprite != null ? Color.white : new Color(0.1f, 0.12f, 0.15f, 1f));
    }

    // 为画布设置统一适配分辨率。
    private static void AddCanvasScaler(GameObject target)
    {
        CanvasScaler scaler = target.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    // 将 RectTransform 拉伸至父节点并保留统一内边距。
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    // 设置以指定锚点为基准的 RectTransform 尺寸和位置。
    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 设置居中 UI 节点的稳定尺寸。
    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
    }

    // 删除旧 UI 子节点，避免遗留旧版 Text 和默认皮肤。
    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    // 保存临时构建根节点为指定预制体并清理临时对象。
    private static void SavePrefab(GameObject root, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // 设置序列化对象的 ObjectReference 字段。
    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    // 从石质资源包中加载单个资源。
    private static T Load<T>(string relativePath) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>($"{ThemeRoot}/{relativePath}");
    }

    // 确保 AssetDatabase 使用的目录已经存在。
    private static void EnsureFolder(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !AssetDatabase.IsValidFolder(path))
            Directory.CreateDirectory(path);
    }
}
