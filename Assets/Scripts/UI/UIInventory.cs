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

    // 已按背包容量创建的槽位视图缓存。
    private readonly List<InventorySlotView> slotViews = new List<InventorySlotView>();
    // 当前选中的背包槽位索引。
    private int selectedIndex = -1;
    // 正在平滑靠近的滚动归一化位置。
    private float targetVerticalPosition = 1f;
    // 背包是否已经打开。
    private bool isOpen;
    // 打开背包前光标管理器的启用状态。
    private bool cursorManagerWasEnabled;

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

        EnsureSlotViews();
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
            slotViews.Add(view);
        }

        for (int i = capacity; i < slotViews.Count; i++)
            slotViews[i].gameObject.SetActive(false);

        ConfigureSlotNavigation(capacity);
    }

    // 为五列槽位显式设置相邻导航，阻止焦点跳入详情按钮。
    private void ConfigureSlotNavigation(int capacity)
    {
        const int columnCount = 5;
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
            SetText(itemTypeText, hasItem ? slot.Item.Category.ToString() : string.Empty);
        if (itemDescriptionText != null)
            SetText(itemDescriptionText, hasItem ? slot.Item.Description : "请选择一个物品查看详情");
        if (itemQuantityText != null)
            SetText(itemQuantityText, hasItem ? $"数量: {slot.Quantity}" : string.Empty);

        bool usable = hasItem && slot.Item.Category == ItemCategory.Consumable;
        if (useButton != null)
            useButton.interactable = usable;
        if (dropButton != null)
            dropButton.interactable = hasItem;
    }

    // 使用当前选中的物品。
    private void UseSelected()
    {
        if (playerInventory != null && selectedIndex >= 0)
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
}
