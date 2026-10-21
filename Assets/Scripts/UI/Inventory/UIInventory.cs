using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包界面控制器，驱动 ScrollView 槽位、详情面板、UI 焦点、暂停和鼠标状态。
/// </summary>
public sealed class UIInventory : UIPanel
{
    // 玩家背包运行时组件。
    [Header("数据")]
    [SerializeField] private PlayerInventory playerInventory;
    // 玩家输入转发组件。
    [SerializeField] private InputManager inputManager;
    // 鼠标锁定状态管理组件。
    [SerializeField] private CursorManager cursorManager;

    // 背包面板根节点。
    [Header("界面引用")]
    [SerializeField] private GameObject panelRoot;
    // 面板可见性与射线交互控制组件。
    [SerializeField] private CanvasGroup panelCanvasGroup;
    // 背包列表的垂直滚动控件。
    [SerializeField] private ScrollRect inventoryScrollRect;
    // 槽位预制体的实例化父节点。
    [SerializeField] private RectTransform slotContent;
    // 背包和商店共用的基础槽位预制体。
    [SerializeField] private InventorySlotView slotPrefab;
    // 当前物品名称文本。
    [SerializeField] private Graphic itemNameText;
    // 当前物品类型文本。
    [SerializeField] private Graphic itemTypeText;
    // 当前物品描述文本。
    [SerializeField] private Graphic itemDescriptionText;
    // 当前物品数量文本。
    [SerializeField] private Graphic itemQuantityText;
    // 玩家金币文本。
    [SerializeField] private Graphic goldText;
    // 使用当前消耗品的按钮。
    [SerializeField] private Button useButton;
    // 丢弃当前物品的按钮。
    [SerializeField] private Button dropButton;
    // 滚动到不可见焦点时的插值速度。
    [SerializeField, Min(1f)] private float focusScrollSpeed = 12f;

    // —— 面板重排用的设计尺寸（参考分辨率 1920×1080 下的像素值）——
    // 原面板 1080×660 的内容紧贴石质边框（popup_bg 九宫格边界约 160），
    // 装备格与网格会压住标题花纹；这里放大面板并把标题 / 装备区 / 背包网格 / 详情区按安全区重新分区。
    private const float PanelWidth = 1080f;
    private const float PanelHeight = 850f;
    // 内容距面板左右边缘的安全距离（避开边框花纹）。
    private const float ContentInset = 96f;
    // 装备区（纸娃娃区块）。
    private const float EquipmentTopOffset = 92f;
    private const float EquipmentHeaderHeight = 22f;
    private const float EquipmentHeaderGap = 8f;
    private const float EquipmentHeaderRuleInset = 24f;
    private const float EquipmentSlotSize = 76f;
    private const float EquipmentSlotLabelHeight = 18f;
    private const float EquipmentRowGap = 6f;
    private const float EquipmentBlockBottomPadding = 12f;
    // 同一行相邻装备格的中心间距（槽位 76 + 间隔 48），保证标签不会互相重叠。
    private const float EquipmentColumnSpacing = 124f;
    // 装备区总高：表头 + 表头间距 + 两行（槽位 + 标签）+ 行间距 + 底部留白。
    private static readonly float EquipmentBlockHeight = EquipmentHeaderHeight + EquipmentHeaderGap
        + (EquipmentSlotSize + EquipmentSlotLabelHeight) * 2f + EquipmentRowGap + EquipmentBlockBottomPadding;
    // 装备区底边到网格区顶边的间距。
    private const float EquipmentToGridGap = 16f;
    // 背包网格与详情区。
    private static readonly float GridZoneTopOffset = EquipmentTopOffset + EquipmentBlockHeight + EquipmentToGridGap;
    private const float GridZoneHeight = 386f;
    private const float GridCellSize = 76f;
    private const float GridSpacing = 10f;
    private const float GridPadding = 10f;
    private const float GridDetailGap = 20f;
    private const int GridColumnCount = 5;
    // 内嵌容器的深色玻璃配色（替换素材包 popup_bg——其九宫格边界约 160，在小尺寸容器上会错位）。
    private static readonly Color InnerPanelFill = new Color(0.06f, 0.055f, 0.05f, 0.55f);
    // 装备区背板：刻意与网格/详情区取同一明度，否则槽位底图（item_frame 深色块）在浅背板上几乎不可见。
    private static readonly Color EquipmentFill = new Color(0.06f, 0.055f, 0.05f, 0.55f);
    // 装备格描边：暖色青铜，用来在深色装备区里勾出格子轮廓。
    private static readonly Color EquipmentSlotFrameColor = new Color(0.62f, 0.55f, 0.36f, 0.85f);

    /// <summary>
    /// 装备区布局表：按身体部位自上而下、由左至右摆放，与人物各部位一一对应。
    /// column 取值 0/1/2 表示左 / 中 / 右三列（中列为人体中轴）；row 0 为上身（头、配饰），row 1 为下身（双手、躯干）。
    /// </summary>
    private static readonly (EquipmentSlotType type, string name, string part, int column, int row)[] EquipmentLayout =
    {
        (EquipmentSlotType.Helmet, "头盔", "头部", 1, 0),
        (EquipmentSlotType.Accessory, "饰品", "配饰", 2, 0),
        (EquipmentSlotType.Weapon, "武器", "右手", 0, 1),
        (EquipmentSlotType.Armor, "护甲", "躯干", 1, 1),
        (EquipmentSlotType.Shield, "盾牌", "左手", 2, 1),
    };

    // 已按背包容量创建的槽位视图缓存。
    private readonly List<InventorySlotView> slotViews = new List<InventorySlotView>();
    // 装备格视图缓存（按槽位类型）。
    private readonly Dictionary<EquipmentSlotType, EquipmentSlotView> equipmentViews =
        new Dictionary<EquipmentSlotType, EquipmentSlotView>();
    // 拖影图标（跟随指针的临时 Image）。
    private Image dragGhost;
    // 当前选中的背包槽位索引。
    private int selectedIndex = -1;
    // 正在平滑靠近的滚动归一化位置。
    private float targetVerticalPosition = 1f;
    // 背包是否已经打开。
    private bool isOpen;
    // 打开背包前光标管理器的启用状态。
    private bool cursorManagerWasEnabled;

    /// <summary>玩家换装组件（装备/卸下的执行者）。</summary>
    public PlayerOutfit PlayerOutfit { get; private set; }

    /// <summary>背包是否处于打开状态，供光标管理器避免重新锁定鼠标。</summary>
    public bool IsOpen => isOpen;

    // 解析场景依赖并初始化静态 UI 引用。
    private void Awake()
    {
        playerInventory = playerInventory != null ? playerInventory : FindFirstObjectByType<PlayerInventory>();
        inputManager = inputManager != null ? inputManager : FindFirstObjectByType<InputManager>();
        cursorManager = cursorManager != null ? cursorManager : FindFirstObjectByType<CursorManager>();
        panelRoot = panelRoot != null ? panelRoot : gameObject;
        panelCanvasGroup = panelCanvasGroup != null ? panelCanvasGroup : panelRoot.GetComponent<CanvasGroup>();
        panelCanvasGroup = panelCanvasGroup != null ? panelCanvasGroup : panelRoot.AddComponent<CanvasGroup>();
        SetPanelVisible(false);
        WireButtons();
    }

    // 订阅输入与背包数据变化事件。
    private void OnEnable()
    {
        if (inputManager != null)
        {
            inputManager.InventoryPressed += Toggle;
            inputManager.UiCancelPressed += CloseInventory;
        }

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged += OnInventoryChanged;
            playerInventory.GoldChanged += OnGoldChanged;
        }
    }

    // 取消事件订阅并恢复暂停状态。
    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.InventoryPressed -= Toggle;
            inputManager.UiCancelPressed -= CloseInventory;
            inputManager.SetUiInputEnabled(false);
        }

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= OnInventoryChanged;
            playerInventory.GoldChanged -= OnGoldChanged;
        }

        if (isOpen)
            CloseInventory();
    }

    // 维持打开背包期间的鼠标可见状态，并在暂停时平滑滚动。
    private void Update()
    {
        if (!isOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (inventoryScrollRect != null)
        {
            inventoryScrollRect.verticalNormalizedPosition = Mathf.MoveTowards(
                inventoryScrollRect.verticalNormalizedPosition,
                targetVerticalPosition,
                focusScrollSpeed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>切换背包面板显示状态。</summary>
    public void Toggle()
    {
        if (isOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    /// <summary>打开背包、启用 UI 输入并选择首个可用槽位。</summary>
    public void OpenInventory()
    {
        if (isOpen)
            return;

        isOpen = true;
        AudioManager.Instance?.PlayUiOpen();
        SetPanelVisible(true);
        inputManager?.SetUiInputEnabled(true);
        inputManager?.SetLookInputEnabled(false);
        SetGameState(GameState.Paused);

        if (cursorManager != null)
        {
            cursorManagerWasEnabled = cursorManager.enabled;
            cursorManager.UnlockCursor();
            cursorManager.enabled = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        PlayerOutfit = PlayerOutfit != null ? PlayerOutfit : FindFirstObjectByType<PlayerOutfit>();
        // 先完成面板重排（分辨率可能在运行期间变化），再生成槽位，避免用旧尺寸计算布局。
        ApplyPanelLayout();
        EnsureSlotViews();
        EnsureEquipmentSlots();
        selectedIndex = GetInitialSelectedIndex();
        RefreshAll();
        SelectSlotObject(selectedIndex);
    }

    /// <summary>关闭背包、禁用 UI 输入并恢复世界运行。</summary>
    public void CloseInventory()
    {
        if (!isOpen)
            return;

        isOpen = false;
        AudioManager.Instance?.PlayUiClose();
        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetLookInputEnabled(true);
        SetPanelVisible(false);
        SetGameState(GameState.Playing);

        if (cursorManager != null)
        {
            cursorManager.enabled = cursorManagerWasEnabled;
            cursorManager.LockCursor();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // 按背包容量实例化槽位预制体，并配置固定网格导航。
    private void EnsureSlotViews()
    {
        if (playerInventory == null || slotPrefab == null || slotContent == null)
            return;

        int capacity = playerInventory.Capacity;
        while (slotViews.Count < capacity)
        {
            int index = slotViews.Count;
            InventorySlotView view = Instantiate(slotPrefab, slotContent);
            view.name = $"Inventory Slot {index + 1}";
            view.Initialize(index, SelectSlot, OnSlotSelected);
            view.InitializeDrag(OnSlotBeginDrag, OnSlotDrag, OnSlotEndDrag, OnSlotDrop, OnSlotDoubleClick);
            slotViews.Add(view);
        }

        for (int i = capacity; i < slotViews.Count; i++)
            slotViews[i].gameObject.SetActive(false);

        ConfigureSlotNavigation(capacity);
    }

    // 在标题下方构建装备区：按身体部位（头 / 躯干 / 双手 / 配饰）分区摆放，与人物各部位一一对应。
    private void EnsureEquipmentSlots()
    {
        if (equipmentViews.Count > 0 || panelRoot == null)
            return;

        StoneUiTheme theme = Resources.Load<StoneUiTheme>("UI/StoneUiTheme");

        // 装备区宿主：标题下方的独立圆角区块，左右与背包网格、详情区取同一内边距。
        RectTransform hostRect = UIRuntimeGraphics.CreateRoundedBackdrop(
            panelRoot.transform as RectTransform, "Equipment Panel", EquipmentFill);
        hostRect.anchorMin = new Vector2(0f, 1f);
        hostRect.anchorMax = new Vector2(1f, 1f);
        hostRect.pivot = new Vector2(0.5f, 1f);
        hostRect.sizeDelta = new Vector2(ContentInset * -2f, EquipmentBlockHeight);
        hostRect.anchoredPosition = new Vector2(0f, -EquipmentTopOffset);

        BuildEquipmentHeader(hostRect, theme);

        // 行的纵向节拍：槽位高度 + 槽位标签 + 行间距。
        float rowPitch = EquipmentSlotSize + EquipmentSlotLabelHeight + EquipmentRowGap;

        for (int i = 0; i < EquipmentLayout.Length; i++)
        {
            var spec = EquipmentLayout[i];
            float slotTop = EquipmentHeaderHeight + EquipmentHeaderGap + spec.row * rowPitch;
            float columnOffset = (spec.column - 1) * EquipmentColumnSpacing;

            CreateEquipmentSlot(hostRect, theme, spec.type, spec.name, spec.part, columnOffset, slotTop);
        }

        RefreshEquipmentViews();
    }

    // 装备区分组标题：居中文字 + 左右细分隔线，与声音卡片的分组标题语言保持一致。
    private void BuildEquipmentHeader(RectTransform hostRect, StoneUiTheme theme)
    {
        const float textWidth = 96f;
        const float ruleMargin = 16f;
        float centerY = -(EquipmentHeaderHeight * 0.5f);

        GameObject titleObject = new GameObject("Equipment Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.SetParent(hostRect, false);
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(textWidth, EquipmentHeaderHeight);
        titleRect.anchoredPosition = Vector2.zero;
        TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = "装 备";
        titleText.fontSize = 20f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.95f, 0.88f, 0.68f);
        titleText.enableWordWrapping = false;
        titleText.raycastTarget = false;
        if (theme != null && theme.ChineseFont != null)
            titleText.font = theme.ChineseFont;

        CreateHeaderRule(hostRect, "Rule Left", new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            -(textWidth * 0.5f + ruleMargin), centerY);
        CreateHeaderRule(hostRect, "Rule Right", new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            textWidth * 0.5f + ruleMargin, centerY);
    }

    // 创建装备区分组标题两侧的细分隔线。
    // innerOffsetFromMiddle：分隔线朝向中线的一端相对中线的偏移，左半段为负、右半段为正。
    private static void CreateHeaderRule(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        float innerOffsetFromMiddle, float centerY)
    {
        GameObject rule = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = rule.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;

        if (anchorMin.x < 0.5f)
        {
            // 左半段：从内容左边界延伸到中线左侧。
            rect.offsetMin = new Vector2(EquipmentHeaderRuleInset, centerY - 1f);
            rect.offsetMax = new Vector2(innerOffsetFromMiddle, centerY + 1f);
        }
        else
        {
            // 右半段：从中线右侧延伸到内容右边界。
            rect.offsetMin = new Vector2(innerOffsetFromMiddle, centerY - 1f);
            rect.offsetMax = new Vector2(-EquipmentHeaderRuleInset, centerY + 1f);
        }

        Image image = rule.GetComponent<Image>();
        image.color = new Color(0.78f, 0.72f, 0.60f, 0.28f);
        image.raycastTarget = false;
    }

    // 创建一个装备格（含槽位名 + 身体部位标签），并注册到装备格字典。
    private void CreateEquipmentSlot(RectTransform hostRect, StoneUiTheme theme, EquipmentSlotType type,
        string slotName, string partName, float columnOffset, float slotTop)
    {
        GameObject slotObject = Instantiate(slotPrefab.gameObject, hostRect);
        slotObject.name = $"Equipment Slot {type}";
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.localScale = Vector3.one;
        slotRect.anchorMin = new Vector2(0.5f, 1f);
        slotRect.anchorMax = new Vector2(0.5f, 1f);
        slotRect.pivot = new Vector2(0.5f, 1f);
        slotRect.sizeDelta = new Vector2(EquipmentSlotSize, EquipmentSlotSize);
        slotRect.anchoredPosition = new Vector2(columnOffset, -slotTop);

        // 槽位预制体自带 InventorySlotView 与"选中高亮"子物体：
        // 前者是背包格专用（回调未注入且会重复响应拖放），后者会在装备格上常亮并盖住物品图标，这里一并清掉。
        InventorySlotView legacyView = slotObject.GetComponent<InventorySlotView>();
        if (legacyView != null)
            Destroy(legacyView);

        Transform selectedFrame = slotObject.transform.Find("Selected Frame");
        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(false);

        Transform quantity = slotObject.transform.Find("Quantity");
        if (quantity != null)
            quantity.gameObject.SetActive(false);

        // 槽位描边：插到子物体首位，绘制在槽位底图之上、物品图标之下，勾出格子轮廓。
        RectTransform frameRect = UIRuntimeGraphics.CreateRoundedElement(
            slotRect, "Slot Frame", UIRuntimeGraphics.RoundedFrame, EquipmentSlotFrameColor);
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        frameRect.SetAsFirstSibling();

        // 槽位标签：槽位名 + 身体部位，位于槽位正下方。
        GameObject labelObject = new GameObject($"Slot Label {type}", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(slotRect, false);
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(EquipmentColumnSpacing - 8f, EquipmentSlotLabelHeight);
        labelRect.anchoredPosition = Vector2.zero;
        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = $"{slotName} · {partName}";
        labelText.fontSize = 13f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(0.85f, 0.82f, 0.75f);
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Overflow;
        labelText.raycastTarget = false;
        if (theme != null && theme.ChineseFont != null)
            labelText.font = theme.ChineseFont;

        EquipmentSlotView view = slotObject.AddComponent<EquipmentSlotView>();
        view.BindLabel(labelText);
        view.Configure(type, $"{slotName} · {partName}");
        view.Initialize(OnEquipmentDrop, OnEquipmentClick);

        // 绑定槽位预制体自带的 Icon 子物体（运行时组件的序列化引用为空）。
        Transform iconTransform = slotObject.transform.Find("Icon");
        if (iconTransform != null)
            view.BindIcon(iconTransform.GetComponent<Image>());

        equipmentViews[type] = view;

        // 尚未开放穿戴的部位：禁用交互并整体降低亮度，标签一并变暗以示区分。
        if (!IsSlotUnlocked(type))
        {
            view.SetEquipped(null);
            Button slotButton = slotObject.GetComponent<Button>();
            if (slotButton != null)
                slotButton.interactable = false;

            CanvasGroup slotGroup = slotObject.GetComponent<CanvasGroup>();
            if (slotGroup == null)
                slotGroup = slotObject.AddComponent<CanvasGroup>();
            slotGroup.alpha = 0.45f;
        }
    }

    // 目前只有武器与盾牌支持穿戴，其余部位为预留槽位。
    private static bool IsSlotUnlocked(EquipmentSlotType type)
    {
        return type == EquipmentSlotType.Weapon || type == EquipmentSlotType.Shield;
    }

    /// <summary>
    /// 重排背包面板：放大面板、把装备区/网格/详情区按安全区分块，
    /// 同步调整网格单元尺寸与滚动区高度，最后做画布等比自适应。
    /// </summary>
    private void ApplyPanelLayout()
    {
        RectTransform panel = panelRoot != null ? panelRoot.transform as RectTransform : null;
        if (panel == null)
            return;

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.localScale = Vector3.one;

        // 金币移到右上角，与标题飘带同排，给底部网格留出完整的安全区。
        if (goldText != null)
        {
            Transform goldRoot = goldText.transform.parent;
            RectTransform goldRect = goldRoot as RectTransform;
            if (goldRect != null)
            {
                goldRect.anchorMin = new Vector2(1f, 1f);
                goldRect.anchorMax = new Vector2(1f, 1f);
                goldRect.pivot = new Vector2(1f, 1f);
                goldRect.sizeDelta = new Vector2(240f, 44f);
                goldRect.anchoredPosition = new Vector2(-ContentInset, -18f);
            }
        }

        // 背包网格：宽度按"列数 × 单元 + 间距 + 内边距"精确推导，保证网格不被滚动区裁切。
        float contentWidth = GridColumnCount * GridCellSize + (GridColumnCount - 1) * GridSpacing + GridPadding * 2f;
        // 滚动区比内容多出 32 像素（Viewport 的内缩），否则网格右侧会被裁掉。
        float scrollWidth = contentWidth + 32f;
        float detailWidth = PanelWidth - ContentInset * 2f - scrollWidth - GridDetailGap;

        if (inventoryScrollRect != null)
        {
            RectTransform scrollRect = inventoryScrollRect.transform as RectTransform;
            if (scrollRect != null)
            {
                scrollRect.anchorMin = new Vector2(0f, 1f);
                scrollRect.anchorMax = new Vector2(0f, 1f);
                scrollRect.pivot = new Vector2(0f, 1f);
                scrollRect.sizeDelta = new Vector2(scrollWidth, GridZoneHeight);
                scrollRect.anchoredPosition = new Vector2(ContentInset, -GridZoneTopOffset);

                // 素材包 popup_bg 的九宫格边界约 160 像素，在 472 宽的滚动区上边角会挤在一起，
                // 这里统一换成程序生成的圆角背板，和装备区、声音卡片保持同一套容器语言。
                ApplyInnerPanelStyle(inventoryScrollRect.GetComponent<Image>(), InnerPanelFill);
            }
        }

        // 物品详情区：与滚动区顶部对齐、等高，右边缘对齐内容安全区。
        Transform detailRoot = itemNameText != null ? itemNameText.transform.parent : null;
        RectTransform detailRect = detailRoot as RectTransform;
        if (detailRect != null)
        {
            detailRect.anchorMin = new Vector2(1f, 1f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.pivot = new Vector2(1f, 1f);
            detailRect.sizeDelta = new Vector2(detailWidth, GridZoneHeight);
            detailRect.anchoredPosition = new Vector2(-ContentInset, -GridZoneTopOffset);

            ApplyInnerPanelStyle(detailRect.GetComponent<Image>(), InnerPanelFill);
        }

        ApplyGridLayout();

        UIRuntimeGraphics.FitPanelToCanvas(panel, new Vector2(PanelWidth, PanelHeight));
    }

    // 同步网格布局参数，使 5 列 × 4 行刚好落在滚动区可视范围内。
    private void ApplyGridLayout()
    {
        if (slotContent == null)
            return;

        GridLayoutGroup grid = slotContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        grid.cellSize = new Vector2(GridCellSize, GridCellSize);
        grid.spacing = new Vector2(GridSpacing, GridSpacing);
        grid.padding = new RectOffset((int)GridPadding, (int)GridPadding, (int)GridPadding, (int)GridPadding);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridColumnCount;
    }

    // 把内嵌容器的背景换成程序生成的圆角背板（保留原有可点击区域，仅替换观感）。
    private static void ApplyInnerPanelStyle(Image image, Color fill)
    {
        if (image == null)
            return;

        image.sprite = UIRuntimeGraphics.RoundedPanel;
        image.type = Image.Type.Sliced;
        image.color = fill;
    }

    // 刷新全部装备格显示（统一从 PlayerOutfit 读取，覆盖双击/拖拽/读档三种装备来源）。
    private void RefreshEquipmentViews()
    {
        if (PlayerOutfit == null)
            return;

        foreach (var pair in equipmentViews)
        {
            ItemDefinition equipped = pair.Key switch
            {
                EquipmentSlotType.Weapon => PlayerOutfit.EquippedWeaponItem,
                EquipmentSlotType.Shield => PlayerOutfit.EquippedShieldItem,
                _ => null,
            };
            pair.Value.SetEquipped(equipped);
        }
    }

    // 装备格拖拽落点：校验槽位匹配后从背包移除并穿戴。
    private void OnEquipmentDrop(EquipmentSlotView slot)
    {
        if (playerInventory == null || PlayerOutfit == null || !DragPayload.IsDragging)
            return;

        if (DragPayload.SourceSlotIndex < 0)
            return;

        if (playerInventory.TryGetItem(DragPayload.SourceSlotIndex, out InventorySlot source) &&
            !source.IsEmpty &&
            source.Item.IsEquippable &&
            source.Item.EquipmentSlot == slot.SlotType &&
            EquipAndReturnOld(source.Item, DragPayload.SourceSlotIndex))
        {
            AudioManager.Instance?.PlayUiConfirm();
        }

        DragPayload.IsDragging = false;
        DragPayload.SourceSlotIndex = -1;
        RefreshAll();
        RefreshEquipmentViews();
    }

    // 点击装备格：卸下当前装备并放回背包。
    private void OnEquipmentClick(EquipmentSlotView slot)
    {
        if (playerInventory == null || PlayerOutfit == null)
            return;

        ItemDefinition equipped = slot.EquippedItem;
        if (equipped == null || !playerInventory.CanAddItem(equipped, 1))
        {
            AudioManager.Instance?.PlayUiClose();
            return;
        }

        ItemDefinition removed = PlayerOutfit.Unequip(slot.SlotType);
        if (removed != null)
            playerInventory.TryAddItem(removed, 1);

        AudioManager.Instance?.PlayUiConfirm();
        RefreshAll();
        RefreshEquipmentViews();
    }

    // 拖拽开始：记录来源槽位并创建跟随指针的拖影。
    private void OnSlotBeginDrag(InventorySlotView view)
    {
        if (playerInventory == null || !playerInventory.TryGetItem(view.GetSlotIndexForDrag(), out InventorySlot slot) || slot.IsEmpty)
            return;

        DragPayload.IsDragging = true;
        DragPayload.SourceSlotIndex = view.GetSlotIndexForDrag();

        if (slot.Item.Icon == null)
            return;

        dragGhost = CreateDragGhost(slot.Item.Icon);
    }

    // 拖拽中：拖影跟随指针。
    private void OnSlotDrag(InventorySlotView view, Vector2 pointerPosition)
    {
        if (dragGhost == null)
            return;

        Camera canvasCamera = GetCanvasCamera();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panelRoot.transform as RectTransform, pointerPosition, canvasCamera, out Vector2 localPoint))
            dragGhost.rectTransform.anchoredPosition = localPoint;
    }

    // 拖拽结束：清理拖影（落点结果由 OnSlotDrop/OnEquipmentDrop 处理）。
    private void OnSlotEndDrag(InventorySlotView view)
    {
        DragPayload.IsDragging = false;
        DragPayload.SourceSlotIndex = -1;

        if (dragGhost != null)
        {
            Destroy(dragGhost.gameObject);
            dragGhost = null;
        }

        RefreshAll();
        RefreshEquipmentViews();
    }

    // 拖拽落到另一背包格：交换或堆叠合并。
    private void OnSlotDrop(int targetIndex, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex == targetIndex)
            return;

        playerInventory?.TryMoveItem(sourceIndex, targetIndex);
    }

    // 双击背包格：装备类物品直接穿戴（被替换下来的装备退回背包）。
    private void OnSlotDoubleClick(int index)
    {
        if (playerInventory == null || !playerInventory.TryGetItem(index, out InventorySlot slot) || slot.IsEmpty)
            return;

        if (!slot.Item.IsEquippable)
            return;

        PlayerOutfit = PlayerOutfit != null ? PlayerOutfit : FindFirstObjectByType<PlayerOutfit>();
        if (PlayerOutfit == null || !EquipAndReturnOld(slot.Item, index))
            return;

        AudioManager.Instance?.PlayUiConfirm();
        RefreshAll();
        RefreshEquipmentViews();
    }

    // 把背包第 sourceIndex 格里的物品装备上，并把被替换下来的旧装备退回背包。
    // 先移除新物品以腾出格子，再放回旧装备，避免背包恰好放满时旧装备被直接吞掉。
    private bool EquipAndReturnOld(ItemDefinition item, int sourceIndex)
    {
        if (playerInventory == null || PlayerOutfit == null || item == null)
            return false;

        ItemDefinition replacedItem = GetEquippedItem(item.EquipmentSlot);

        if (sourceIndex >= 0)
            playerInventory.TryRemoveItem(sourceIndex, 1);

        if (!PlayerOutfit.Equip(item))
        {
            // 装备失败：把新物品放回背包，保持换装前的状态。
            if (sourceIndex >= 0)
                playerInventory.TryAddItem(item, 1);
            return false;
        }

        if (replacedItem != null && !playerInventory.TryAddItem(replacedItem, 1))
            Debug.LogWarning($"[Inventory] 背包已满，被替换下来的「{replacedItem.DisplayName}」未能退回背包。", this);

        return true;
    }

    // 读取指定槽位当前装备的物品定义。
    private ItemDefinition GetEquippedItem(EquipmentSlotType slot)
    {
        if (PlayerOutfit == null)
            return null;

        switch (slot)
        {
            case EquipmentSlotType.Weapon:
                return PlayerOutfit.EquippedWeaponItem;
            case EquipmentSlotType.Shield:
                return PlayerOutfit.EquippedShieldItem;
            default:
                return null;
        }
    }

    // 创建跟随指针的拖影图标（挂在背包面板画布顶层）。
    private Image CreateDragGhost(Sprite sprite)
    {
        GameObject ghost = new GameObject("Drag Ghost", typeof(RectTransform), typeof(Image));
        RectTransform ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.SetParent(panelRoot.transform, true);
        ghostRect.SetAsLastSibling();
        ghostRect.sizeDelta = new Vector2(GridCellSize - 4f, GridCellSize - 4f);

        Image image = ghost.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.8f);
        return image;
    }

    // 获取画布渲染用的相机（Screen Space Overlay 时为 null）。
    private Camera GetCanvasCamera()
    {
        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    // 为固定列数的槽位显式设置相邻导航，阻止焦点跳入详情按钮。
    private void ConfigureSlotNavigation(int capacity)
    {
        const int columnCount = GridColumnCount;
        for (int index = 0; index < capacity; index++)
        {
            Button button = slotViews[index].Button;
            if (button == null)
                continue;

            Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnLeft = index % columnCount > 0 ? slotViews[index - 1].Button : null;
            navigation.selectOnRight = index % columnCount < columnCount - 1 && index + 1 < capacity ? slotViews[index + 1].Button : null;
            navigation.selectOnUp = index - columnCount >= 0 ? slotViews[index - columnCount].Button : null;
            navigation.selectOnDown = index + columnCount < capacity ? slotViews[index + columnCount].Button : null;
            button.navigation = navigation;
        }
    }

    // 刷新所有槽位、详情和金币显示。
    private void RefreshAll()
    {
        if (playerInventory == null)
            return;

        IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
        for (int i = 0; i < slotViews.Count; i++)
        {
            InventorySlot slot = i < slots.Count ? slots[i] : default;
            slotViews[i].SetSlot(slot);
            slotViews[i].SetSelected(i == selectedIndex);
        }

        OnGoldChanged(playerInventory.Gold);
        RefreshDetails();
    }

    // 接收背包模型变化并刷新显示。
    private void OnInventoryChanged(InventoryChangedEvent changeEvent)
    {
        RefreshAll();
    }

    // 接收金币变化并刷新金币文本。
    private void OnGoldChanged(int value)
    {
        if (goldText != null)
            SetText(goldText, $"金币: {value}");
    }

    // 处理鼠标点击或提交选择的槽位。
    private void SelectSlot(int index)
    {
        if (index < 0 || index >= slotViews.Count)
            return;

        selectedIndex = index;
        RefreshAll();
        SelectSlotObject(index);
    }

    // 响应 EventSystem 焦点变化并同步详情与滚动位置。
    private void OnSlotSelected(int index)
    {
        if (!isOpen || index == selectedIndex)
            return;

        selectedIndex = index;
        RefreshAll();
        SetScrollTarget(index);
    }

    // 将 EventSystem 当前焦点设为目标槽位。
    private void SelectSlotObject(int index)
    {
        if (index < 0 || index >= slotViews.Count)
            return;

        EventSystem.current?.SetSelectedGameObject(slotViews[index].gameObject);
        SetScrollTarget(index);
    }

    // 找到打开背包时优先显示的第一个非空槽位。
    private int GetInitialSelectedIndex()
    {
        if (playerInventory == null)
            return 0;

        IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                return i;
        }

        return 0;
    }

    // 计算焦点槽位位于视口外时所需的滚动目标。
    private void SetScrollTarget(int index)
    {
        if (inventoryScrollRect == null || inventoryScrollRect.viewport == null || index < 0 || index >= slotViews.Count)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform viewport = inventoryScrollRect.viewport;
        Bounds viewportBounds = new Bounds(viewport.rect.center, viewport.rect.size);
        Bounds slotBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, slotViews[index].RectTransform);
        float offset = 0f;

        if (slotBounds.max.y > viewportBounds.max.y)
            offset = slotBounds.max.y - viewportBounds.max.y;
        else if (slotBounds.min.y < viewportBounds.min.y)
            offset = slotBounds.min.y - viewportBounds.min.y;

        float scrollableHeight = Mathf.Max(0f, slotContent.rect.height - viewport.rect.height);
        targetVerticalPosition = scrollableHeight > 0f
            ? Mathf.Clamp01(inventoryScrollRect.verticalNormalizedPosition + offset / scrollableHeight)
            : 1f;
    }

    // 刷新选中物品的详情与操作按钮状态。
    private void RefreshDetails()
    {
        InventorySlot slot = default;
        bool hasItem = playerInventory != null && selectedIndex >= 0 && selectedIndex < playerInventory.Slots.Count && (slot = playerInventory.Slots[selectedIndex]).IsEmpty == false;
        if (itemNameText != null)
            SetText(itemNameText, hasItem ? slot.Item.DisplayName : "未选择物品");
        if (itemTypeText != null)
            SetText(itemTypeText, hasItem ? GetItemTypeLabel(slot.Item) : string.Empty);
        if (itemDescriptionText != null)
            SetText(itemDescriptionText, hasItem ? slot.Item.Description : "请选择一个物品查看详情");
        if (itemQuantityText != null)
            SetText(itemQuantityText, hasItem ? $"数量: {slot.Quantity}" : string.Empty);

        // 消耗品显示"使用"、装备显示"装备"，其他类型不可用。
        bool isConsumable = hasItem && slot.Item.Category == ItemCategory.Consumable;
        bool isEquippable = hasItem && slot.Item.IsEquippable;
        if (useButton != null)
        {
            useButton.interactable = isConsumable || isEquippable;
            SetButtonText(useButton, isEquippable ? "装备" : "使用");
        }
        if (dropButton != null)
            dropButton.interactable = hasItem;
    }

    // 使用或装备当前选中的物品（按物品类型分流）。
    private void UseSelected()
    {
        if (playerInventory == null || selectedIndex < 0)
            return;

        if (!playerInventory.TryGetItem(selectedIndex, out InventorySlot slot) || slot.IsEmpty)
            return;

        if (slot.Item.IsEquippable)
            OnSlotDoubleClick(selectedIndex);
        else
            playerInventory.TryUseItem(selectedIndex);
    }

    // 丢弃当前选中槽位的全部物品。
    private void DropSelected()
    {
        if (playerInventory != null && selectedIndex >= 0)
            playerInventory.TryDropItem(selectedIndex);
    }

    // 绑定使用和丢弃按钮的点击事件。
    private void WireButtons()
    {
        if (useButton != null)
        {
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(UseSelected);
        }

        if (dropButton != null)
        {
            dropButton.onClick.RemoveAllListeners();
            dropButton.onClick.AddListener(DropSelected);
        }
    }

    // 通过 CanvasGroup 控制背包面板的可见性与交互性。
    private void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    // 通过项目统一命令切换暂停或运行状态。
    private void SetGameState(GameState state)
    {
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }

    // 同时兼容旧 UGUI Text 与石质主题使用的 TMP 文本。
    private static void SetText(Graphic textGraphic, string content)
    {
        if (textGraphic is TMP_Text tmpText)
            tmpText.text = content;
        else if (textGraphic is Text legacyText)
            legacyText.text = content;
    }

    // 获取物品的类型显示文案。
    private static string GetItemTypeLabel(ItemDefinition item)
    {
        return item.EquipmentSlot switch
        {
            EquipmentSlotType.Weapon => "装备 - 武器",
            EquipmentSlotType.Shield => "装备 - 盾牌",
            EquipmentSlotType.Helmet => "装备 - 头盔",
            EquipmentSlotType.Armor => "装备 - 护甲",
            EquipmentSlotType.Accessory => "装备 - 配饰",
            _ => item.Category.ToString(),
        };
    }

    // 修改按钮显示文本（兼容 TMP）。
    private static void SetButtonText(Button button, string content)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = content;
    }
}
