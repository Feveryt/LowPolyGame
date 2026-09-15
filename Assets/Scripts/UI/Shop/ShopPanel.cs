using System.Collections.Generic;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>商店运行时界面，负责以项目石质主题展示商品、数量选择和交易反馈。</summary>
[DisallowMultipleComponent]
public sealed class ShopPanel : MonoBehaviour
{
    // 商品行预制体在 Resources 下的固定路径，供 Inspector 引用缺失时兜底加载。
    private const string RowPrefabPath = "Prefabs/UI/ShopItemRow";

    private static ShopPanel instance;

    // 已创建的商品行，用于刷新背包持有数量和销毁旧商店内容。
    private readonly List<ShopItemRow> rows = new List<ShopItemRow>();
    // 当前交易规则和玩家背包的数据入口。
    private ShopService shopService;
    private PlayerInventory playerInventory;
    // 商店打开期间控制输入和光标状态的场景组件。
    private InputManager inputManager;
    private CursorManager cursorManager;
    // 项目统一石质主题，提供窗体、按钮、图标和中文字体。
    private StoneUiTheme stoneUiTheme;
    // 覆盖层的可见性和交互控制组件。
    [SerializeField] private CanvasGroup canvasGroup;
    // 商品行的垂直布局根节点。
    [SerializeField] private RectTransform contentRoot;
    // 商店抬头、金币和交易反馈文本。
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text messageText;
    // 静态预制体中的关闭按钮，实例创建时绑定运行时关闭回调。
    [SerializeField] private Button closeButton;
    // 单条商品行的预制体，商店打开时按商品数量实例化。
    [SerializeField] private ShopItemRow rowPrefab;
    // 商店是否正占用游戏输入。
    private bool isOpen;
    // 打开前 CursorManager 的启用状态。
    private bool cursorWasEnabled;

    /// <summary>当前场景中的商店面板实例。</summary>
    public static ShopPanel Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
            return instance;
        }
    }

    // 初始化场景依赖并预创建隐藏的石质商店界面。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        inputManager = FindFirstObjectByType<InputManager>();
        playerInventory = FindFirstObjectByType<PlayerInventory>();
        cursorManager = FindFirstObjectByType<CursorManager>();
        stoneUiTheme = Resources.Load<StoneUiTheme>("UI/StoneUiTheme");
        EnsureUi();
        BindStaticControls();
        SetVisible(false);
    }

    // 注册取消输入，以便键盘和手柄可关闭商店。
    private void OnEnable()
    {
        AttachInput();
    }

    // 清理输入和数据订阅，避免运行时销毁后遗留回调。
    private void OnDisable()
    {
        if (inputManager != null)
            inputManager.UiCancelPressed -= CloseShop;
        UnsubscribeData();
    }

    // 商店打开期间持续维持可点击的光标状态。
    private void Update()
    {
        if (!isOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>打开指定商店、暂停世界并刷新商品数据。</summary>
    public void OpenShop(ShopDefinition shop, ShopService service)
    {
        if (shop == null)
            return;

        EnsureUi();
        shopService = service != null ? service : ShopService.Instance;
        playerInventory = playerInventory != null ? playerInventory : FindFirstObjectByType<PlayerInventory>();
        inputManager = inputManager != null ? inputManager : FindFirstObjectByType<InputManager>();
        cursorManager = cursorManager != null ? cursorManager : FindFirstObjectByType<CursorManager>();
        AttachInput();
        SubscribeData();

        isOpen = true;
        SetVisible(true);
        inputManager?.SetUiInputEnabled(true);
        inputManager?.SetPlayerInputEnabled(false);
        inputManager?.SetLookInputEnabled(false);
        SetGameState(GameState.Paused);

        if (cursorManager != null)
        {
            cursorWasEnabled = cursorManager.enabled;
            cursorManager.UnlockCursor();
            cursorManager.enabled = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        titleText.text = shop.DisplayName;
        messageText.text = string.Empty;
        RebuildRows(shop);
        Refresh();
        if (rows.Count > 0)
            EventSystem.current?.SetSelectedGameObject(rows[0].BuyButton.gameObject);
    }

    /// <summary>关闭商店并恢复玩家输入、光标和游戏状态。</summary>
    public void CloseShop()
    {
        if (!isOpen)
            return;

        isOpen = false;
        UnsubscribeData();
        SetVisible(false);
        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetPlayerInputEnabled(true);
        inputManager?.SetLookInputEnabled(true);
        SetGameState(GameState.Playing);

        if (cursorManager != null)
        {
            cursorManager.enabled = cursorWasEnabled;
            if (cursorWasEnabled)
                cursorManager.LockCursor();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        shopService?.CloseShop();
    }

    // 绑定 UI 取消事件到当前场景的输入管理器。
    private void AttachInput()
    {
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputManager>();
        if (inputManager == null)
            return;

        inputManager.UiCancelPressed -= CloseShop;
        inputManager.UiCancelPressed += CloseShop;
    }

    // 订阅交易、背包和金币变化，确保商品行始终反映实时状态。
    private void SubscribeData()
    {
        if (shopService != null)
            shopService.Changed -= Refresh;
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= OnInventoryChanged;
            playerInventory.GoldChanged -= OnGoldChanged;
            playerInventory.InventoryChanged += OnInventoryChanged;
            playerInventory.GoldChanged += OnGoldChanged;
        }
        if (shopService != null)
            shopService.Changed += Refresh;
    }

    // 取消本轮商店打开期间的所有数据订阅。
    private void UnsubscribeData()
    {
        if (shopService != null)
            shopService.Changed -= Refresh;
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= OnInventoryChanged;
            playerInventory.GoldChanged -= OnGoldChanged;
        }
    }

    // 按当前商店资料生成商品行，并移除上次打开遗留的行对象。
    private void RebuildRows(ShopDefinition shop)
    {
        for (int i = 0; i < rows.Count; i++)
            Destroy(rows[i].gameObject);
        rows.Clear();

        ShopItemRow prefab = ResolveRowPrefab();
        if (prefab == null)
            return;

        foreach (ShopItemEntry entry in shop.Items)
        {
            if (entry != null && entry.Item != null)
                rows.Add(CreateRow(prefab, entry));
        }
    }

    // 解析商品行预制体：优先使用 Inspector 引用，缺失时按固定路径兜底加载。
    private ShopItemRow ResolveRowPrefab()
    {
        if (rowPrefab != null)
            return rowPrefab;

        rowPrefab = Resources.Load<ShopItemRow>(RowPrefabPath);
        if (rowPrefab != null)
        {
            Debug.LogWarning($"[{nameof(ShopPanel)}] 未绑定商品行预制体，已回退加载 Resources/{RowPrefabPath}.prefab。", this);
            return rowPrefab;
        }

        Debug.LogError($"[{nameof(ShopPanel)}] 未配置商品行预制体，本次不会显示任何商品行。", this);
        return null;
    }

    // 实例化商品行预制体并写入该商品的运行时资料。
    private ShopItemRow CreateRow(ShopItemRow prefab, ShopItemEntry entry)
    {
        ShopItemRow row = Instantiate(prefab, contentRoot);
        row.name = entry.Item.DisplayName;
        row.Configure(entry, this);
        return row;
    }

    // 刷新金币、拥有数量和按钮可用状态。
    private void Refresh()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();

        OnGoldChanged(playerInventory != null ? playerInventory.Gold : 0);
        for (int i = 0; i < rows.Count; i++)
            rows[i].Refresh(playerInventory);
    }

    // 背包内容变更后刷新商品拥有数量。
    private void OnInventoryChanged(InventoryChangedEvent changeEvent)
    {
        Refresh();
    }

    // 金币变更后同步商店抬头中的余额。
    private void OnGoldChanged(int value)
    {
        if (goldText != null)
            goldText.text = value.ToString();
    }

    // 处理商品行发起的购买操作并显示交易结果。
    internal void Buy(ShopItemRow row)
    {
        ShopTransactionResult result = shopService != null
            ? shopService.TryBuy(row.Entry, row.Amount)
            : ShopTransactionResult.Failed("商店服务不可用");
        ShowTransactionResult(result);
        Refresh();
    }

    // 处理商品行发起的出售操作并显示交易结果。
    internal void Sell(ShopItemRow row)
    {
        ShopTransactionResult result = shopService != null
            ? shopService.TrySell(row.Entry, row.Amount)
            : ShopTransactionResult.Failed("商店服务不可用");
        ShowTransactionResult(result);
        Refresh();
    }

    // 以主题色显示交易成功或失败的简短反馈。
    private void ShowTransactionResult(ShopTransactionResult result)
    {
        messageText.text = result.Message;
        messageText.color = result.Succeeded
            ? new Color(0.72f, 0.9f, 0.42f, 1f)
            : new Color(0.96f, 0.5f, 0.38f, 1f);
    }

    // 创建全屏遮罩、石质窗体、标题、商品列表和关闭控件。
    private void EnsureUi()
    {
        if (canvasGroup != null && contentRoot != null && titleText != null && goldText != null && messageText != null && closeButton != null)
            return;

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect == null)
            panelRect = gameObject.AddComponent<RectTransform>();

        if (Application.isPlaying && GetComponentInParent<Canvas>() == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Shop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            transform.SetParent(canvas.transform, false);
        }

        Stretch(panelRect, 0f);
        transform.SetAsLastSibling();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image dim = CreateImage(transform, null, new Color(0.015f, 0.02f, 0.025f, 0.78f), -1f);
        dim.name = "Dim";
        Stretch(dim.rectTransform, 0f);
        dim.color = new Color(0.015f, 0.02f, 0.025f, 0.78f);

        GameObject window = CreateUiObject("Shop Window", transform);
        SetCentered(window.GetComponent<RectTransform>(), new Vector2(1120f, 680f));
        Image windowImage = window.AddComponent<Image>();
        ApplyThemeSprite(windowImage, stoneUiTheme != null ? stoneUiTheme.PanelSprite : null, Color.white);

        Image ribbon = CreateImage(window.transform, stoneUiTheme != null ? stoneUiTheme.TitleSprite : null, Color.white, 410f);
        SetAnchoredRect(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(410f, 70f), new Vector2(0.5f, 1f));
        titleText = CreateText(ribbon.transform, "商店", 34, TextAlignmentOptions.Center, -1f);
        Stretch(titleText.rectTransform, 12f);
        titleText.font = stoneUiTheme != null ? stoneUiTheme.DisplayFont : TMP_Settings.defaultFontAsset;

        GameObject goldRoot = CreateUiObject("Gold", window.transform);
        SetAnchoredRect(goldRoot.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-42f, -46f), new Vector2(180f, 36f), new Vector2(1f, 1f));
        HorizontalLayoutGroup goldLayout = goldRoot.AddComponent<HorizontalLayoutGroup>();
        goldLayout.spacing = 7f;
        goldLayout.childAlignment = TextAnchor.MiddleRight;
        goldLayout.childControlWidth = false;
        goldLayout.childControlHeight = true;
        CreateImage(goldRoot.transform, stoneUiTheme != null ? stoneUiTheme.CoinSprite : null, Color.white, 28f);
        goldText = CreateText(goldRoot.transform, "0", 22, TextAlignmentOptions.MidlineRight, 112f);

        closeButton = CreateButton(window.transform, "关闭", 112f, stoneUiTheme != null ? stoneUiTheme.DangerButtonSprite : null, Color.white, 19f);
        SetAnchoredRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-42f, -96f), new Vector2(112f, 42f), new Vector2(1f, 1f));

        GameObject itemsFrame = CreateUiObject("Items Frame", window.transform);
        RectTransform itemsFrameRect = itemsFrame.GetComponent<RectTransform>();
        itemsFrameRect.anchorMin = Vector2.zero;
        itemsFrameRect.anchorMax = Vector2.one;
        itemsFrameRect.offsetMin = new Vector2(38f, 94f);
        itemsFrameRect.offsetMax = new Vector2(-38f, -140f);
        Image itemsImage = itemsFrame.AddComponent<Image>();
        ApplyThemeSprite(itemsImage, stoneUiTheme != null ? stoneUiTheme.PanelSprite : null, new Color(0.22f, 0.22f, 0.18f, 0.84f));
        contentRoot = CreateUiObject("Items", itemsFrame.transform).GetComponent<RectTransform>();
        Stretch(contentRoot, 14f);
        VerticalLayoutGroup contentLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        messageText = CreateText(window.transform, string.Empty, 18, TextAlignmentOptions.Center, -1f);
        messageText.rectTransform.anchorMin = new Vector2(0f, 0f);
        messageText.rectTransform.anchorMax = new Vector2(1f, 0f);
        messageText.rectTransform.offsetMin = new Vector2(48f, 38f);
        messageText.rectTransform.offsetMax = new Vector2(-48f, 68f);

    }

    // 为预制体和运行时回退布局统一注册关闭按钮的实例回调。
    private void BindStaticControls()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveListener(CloseShop);
        closeButton.onClick.AddListener(CloseShop);
    }

    // 控制覆盖层的可见性和射线拦截。
    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // 创建遵循 UGUI 层级约定的 RectTransform 对象。
    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    // 创建主题字体文本，并为布局控件可选地指定固定宽度。
    private TMP_Text CreateText(Transform parent, string value, float size, TextAlignmentOptions alignment, float width, float height = -1f)
    {
        TextMeshProUGUI text = CreateUiObject("Text", parent).AddComponent<TextMeshProUGUI>();
        text.font = stoneUiTheme != null && stoneUiTheme.ChineseFont != null ? stoneUiTheme.ChineseFont : TMP_Settings.defaultFontAsset;
        if (stoneUiTheme != null && stoneUiTheme.ChineseFont != null)
            text.fontSharedMaterial = stoneUiTheme.ChineseFont.material;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.9f, 0.8f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        if (width > 0f || height > 0f)
        {
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }
            if (height > 0f)
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }
        }
        return text;
    }

    // 创建主题图片，尺寸为负值时由父布局控制。
    private Image CreateImage(Transform parent, Sprite sprite, Color color, float size)
    {
        Image image = CreateUiObject("Image", parent).AddComponent<Image>();
        ApplyThemeSprite(image, sprite, color);
        image.raycastTarget = false;
        if (size > 0f)
        {
            LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = size;
            element.preferredWidth = size;
            element.minHeight = size;
            element.preferredHeight = size;
        }
        return image;
    }

    // 创建带统一字体和主题精灵的操作按钮。
    private Button CreateButton(Transform parent, string label, float width, Sprite sprite, Color color, float fontSize = 18f)
    {
        GameObject buttonObject = CreateUiObject(label, parent);
        Image image = buttonObject.AddComponent<Image>();
        ApplyThemeSprite(image, sprite, color);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = 42f;
        layout.preferredHeight = 42f;
        TMP_Text text = CreateText(buttonObject.transform, label, fontSize, TextAlignmentOptions.Center, -1f);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    // 为九宫格精灵设置 Sliced 类型，并保留无主题时的可读回退颜色。
    private static void ApplyThemeSprite(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? color : new Color(0.14f, 0.16f, 0.2f, 1f);
    }

    // 将矩形拉伸到父节点并保留统一内边距。
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    // 配置中心窗口的固定尺寸。
    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    // 配置固定锚点、位置、尺寸和枢轴。
    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 通过项目统一架构同步暂停状态和时间缩放。
    private static void SetGameState(GameState state)
    {
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }

}
