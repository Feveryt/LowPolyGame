using System.Collections.Generic;
using System.Linq;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>石质任务面板，浏览只读任务进度并管理暂停、光标、输入和 UI 焦点。</summary>
[DisallowMultipleComponent]
public sealed class QuestPanel : MonoBehaviour
{
    // 面板整体的可见性和射线拦截控制。
    [SerializeField] private CanvasGroup canvasGroup;
    // 左侧任务列表滚动区域。
    [SerializeField] private ScrollRect listScrollRect;
    // 动态任务条目的父节点。
    [SerializeField] private RectTransform listContent;
    // 单条任务的石质列表预制体。
    [SerializeField] private QuestListItem listItemPrefab;
    // 右侧顺序目标的父节点。
    [SerializeField] private RectTransform objectiveContent;
    // 单个目标的石质视图预制体。
    [SerializeField] private QuestObjectiveView objectivePrefab;
    // 进行中任务筛选按钮。
    [SerializeField] private Button activeFilterButton;
    // 已完成任务筛选按钮。
    [SerializeField] private Button completedFilterButton;
    // 关闭面板按钮。
    [SerializeField] private Button closeButton;
    // 当前任务标题。
    [SerializeField] private TMP_Text titleText;
    // 当前任务状态。
    [SerializeField] private TMP_Text statusText;
    // 当前任务剧情说明。
    [SerializeField] private TMP_Text descriptionText;
    // 没有可显示任务时的提示文本。
    [SerializeField] private TMP_Text emptyStateText;
    // 筛选按钮的选中强调底图。
    [SerializeField] private Image activeFilterSelection;
    // 已完成筛选按钮的选中强调底图。
    [SerializeField] private Image completedFilterSelection;

    // 当前面板筛选页签。
    private bool showingCompleted;
    // 当前列表中被选中的任务 ID。
    private string selectedQuestId;
    // 面板是否正在显示。
    private bool isOpen;
    // 面板打开前光标管理器的启用状态。
    private bool cursorManagerWasEnabled;
    // 当前场景的输入转发器。
    private InputManager inputManager;
    // 当前场景的光标管理器。
    private CursorManager cursorManager;
    // 动态创建的任务条目。
    private readonly List<QuestListItem> listItems = new List<QuestListItem>();
    // 动态创建的目标条目。
    private readonly List<QuestObjectiveView> objectiveViews = new List<QuestObjectiveView>();
    // 最新只读任务快照缓存。
    private IReadOnlyList<QuestSnapshot> snapshots = new List<QuestSnapshot>();

    /// <summary>任务面板当前是否处于打开状态。</summary>
    public bool IsOpen => isOpen;

    // 初始化面板为隐藏状态并绑定静态按钮。
    private void Awake()
    {
        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
        canvasGroup = canvasGroup != null ? canvasGroup : gameObject.AddComponent<CanvasGroup>();
        SetPanelVisible(false);
        WireButtons();
        AttachInputManager();
        QuestService.Instance.QuestChanged += OnQuestChanged;
    }

    // 确保运行时禁用对象不会保留输入、服务订阅或暂停状态。
    private void OnDestroy()
    {
        DetachInputManager();
        if (QuestService.Instance != null)
            QuestService.Instance.QuestChanged -= OnQuestChanged;
        if (isOpen)
            CloseQuestPanel();
    }

    // 保持打开期间的光标释放状态，并适配场景中稍后创建的输入组件。
    private void Update()
    {
        if (inputManager == null)
            AttachInputManager();

        if (!isOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>切换任务面板显示状态。</summary>
    public void Toggle()
    {
        if (isOpen)
            CloseQuestPanel();
        else
            OpenQuestPanel();
    }

    /// <summary>打开任务面板，暂停世界并默认显示进行中的第一条任务。</summary>
    public void OpenQuestPanel()
    {
        if (isOpen)
            return;

        AttachInputManager();
        if (inputManager == null || inputManager.IsUiInputEnabled)
            return;

        isOpen = true;
        AudioManager.Instance?.PlayUiOpen();
        inputManager.SetUiInputEnabled(true);
        inputManager.SetLookInputEnabled(false);
        SetGameState(GameState.Paused);
        SetCursorForPanel(true);
        SetPanelVisible(true);
        SelectInitialQuest();
    }

    /// <summary>关闭任务面板，恢复世界时间、光标和输入。</summary>
    public void CloseQuestPanel()
    {
        if (!isOpen)
            return;

        isOpen = false;
        AudioManager.Instance?.PlayUiClose();
        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetLookInputEnabled(true);
        SetPanelVisible(false);
        SetGameState(GameState.Playing);
        SetCursorForPanel(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    // 绑定筛选和关闭按钮，避免预制体依赖 Inspector 中的持久化事件。
    private void WireButtons()
    {
        activeFilterButton.onClick.AddListener(() => SelectFilter(false));
        completedFilterButton.onClick.AddListener(() => SelectFilter(true));
        closeButton.onClick.AddListener(CloseQuestPanel);
    }

    // 绑定当前场景的输入组件，并在场景重载时解除旧订阅。
    private void AttachInputManager()
    {
        InputManager found = FindFirstObjectByType<InputManager>();
        if (found == inputManager)
            return;

        DetachInputManager();
        inputManager = found;
        cursorManager = FindFirstObjectByType<CursorManager>();
        if (inputManager != null)
        {
            inputManager.QuestPressed += Toggle;
            inputManager.UiCancelPressed += CloseQuestPanel;
        }
    }

    // 解除当前场景的输入事件，避免跨场景重复响应。
    private void DetachInputManager()
    {
        if (inputManager == null)
            return;

        inputManager.QuestPressed -= Toggle;
        inputManager.UiCancelPressed -= CloseQuestPanel;
        inputManager = null;
    }

    // 任务服务变更后仅在打开期间实时重绘数据，保留仍可见的选择。
    private void OnQuestChanged(string questId)
    {
        if (isOpen)
            RefreshTasks(false);
    }

    // 在打开时优先进行中任务，无进行中时回退至已完成任务。
    private void SelectInitialQuest()
    {
        snapshots = QuestService.Instance.GetVisibleQuests();
        bool hasActive = snapshots.Any(snapshot => snapshot.State == QuestState.Active || snapshot.State == QuestState.ReadyToTurnIn);
        bool hasCompleted = snapshots.Any(snapshot => snapshot.State == QuestState.Completed);
        showingCompleted = !hasActive && hasCompleted;
        selectedQuestId = null;
        RefreshTasks(true);
    }

    // 切换任务状态页签，并将焦点放在该页签第一条任务。
    private void SelectFilter(bool completed)
    {
        if (showingCompleted == completed)
            return;

        showingCompleted = completed;
        selectedQuestId = null;
        RefreshTasks(true);
    }

    // 根据最新快照创建筛选列表，并保留仍在当前页签的选择。
    private void RefreshTasks(bool focusFirst)
    {
        snapshots = QuestService.Instance.GetVisibleQuests();
        List<QuestSnapshot> filtered = snapshots.Where(IsVisibleInCurrentFilter).ToList();
        if (filtered.All(snapshot => snapshot.QuestId != selectedQuestId))
            selectedQuestId = filtered.Count > 0 ? filtered[0].QuestId : null;

        ClearViews(listItems);
        for (int index = 0; index < filtered.Count; index++)
        {
            QuestSnapshot snapshot = filtered[index];
            QuestListItem item = Instantiate(listItemPrefab, listContent);
            item.name = "Quest " + (index + 1) + " - " + snapshot.QuestId;
            item.Configure(snapshot, snapshot.QuestId == selectedQuestId, SelectQuest);
            listItems.Add(item);
        }

        ConfigureNavigation();
        RefreshDetail();
        RefreshFilterVisuals();
        if (focusFirst)
            FocusCurrentSelection();
    }

    // 点击或焦点导航至任务时，刷新右侧详情与列表高亮。
    private void SelectQuest(string questId)
    {
        if (!isOpen || selectedQuestId == questId)
            return;

        selectedQuestId = questId;
        foreach (QuestListItem item in listItems)
            item.SetSelected(item.QuestId == selectedQuestId);
        RefreshDetail();
    }

    // 填充任务详情与目标列表，空列表时展示恰当的发现状态。
    private void RefreshDetail()
    {
        QuestSnapshot selected = snapshots.FirstOrDefault(snapshot => snapshot.QuestId == selectedQuestId && IsVisibleInCurrentFilter(snapshot));
        bool hasAnyQuest = snapshots.Count > 0;
        bool hasSelected = selected != null;
        emptyStateText.gameObject.SetActive(!hasSelected);
        emptyStateText.text = hasAnyQuest ? "该分类暂无任务" : "尚未接取任务";

        titleText.gameObject.SetActive(hasSelected);
        statusText.gameObject.SetActive(hasSelected);
        descriptionText.gameObject.SetActive(hasSelected);
        objectiveContent.gameObject.SetActive(hasSelected);
        ClearViews(objectiveViews);
        if (!hasSelected)
            return;

        titleText.text = selected.Title;
        statusText.text = GetStatusText(selected.State);
        statusText.color = selected.State == QuestState.ReadyToTurnIn
            ? new Color(0.61f, 0.86f, 0.48f, 1f)
            : selected.State == QuestState.Completed
                ? new Color(0.61f, 0.66f, 0.62f, 1f)
                : new Color(1f, 0.86f, 0.47f, 1f);
        descriptionText.text = selected.Description;
        foreach (QuestObjectiveSnapshot objective in selected.Objectives)
        {
            QuestObjectiveView view = Instantiate(objectivePrefab, objectiveContent);
            view.Configure(objective);
            objectiveViews.Add(view);
        }
    }

    // 为页签、列表和关闭按钮建立手柄或键盘可预测的焦点关系。
    private void ConfigureNavigation()
    {
        SetNavigation(activeFilterButton, completedFilterButton, listItems.Count > 0 ? listItems[0].Button : closeButton, completedFilterButton);
        SetNavigation(completedFilterButton, activeFilterButton, listItems.Count > 0 ? listItems[0].Button : closeButton, activeFilterButton);

        for (int index = 0; index < listItems.Count; index++)
        {
            Button up = index > 0 ? listItems[index - 1].Button : showingCompleted ? completedFilterButton : activeFilterButton;
            Button down = index < listItems.Count - 1 ? listItems[index + 1].Button : closeButton;
            SetNavigation(listItems[index].Button, showingCompleted ? completedFilterButton : activeFilterButton, down, up);
        }

        SetNavigation(closeButton, listItems.Count > 0 ? listItems[listItems.Count - 1].Button : activeFilterButton,
            showingCompleted ? completedFilterButton : activeFilterButton, showingCompleted ? completedFilterButton : activeFilterButton);
    }

    // 设置单个 Selectable 的显式上下左右焦点。
    private static void SetNavigation(Button button, Selectable left, Selectable down, Selectable up)
    {
        Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
        navigation.selectOnLeft = left;
        navigation.selectOnRight = left;
        navigation.selectOnDown = down;
        navigation.selectOnUp = up;
        button.navigation = navigation;
    }

    // 将 UI 焦点与滚动位置对齐到当前选中条目。
    private void FocusCurrentSelection()
    {
        QuestListItem selected = listItems.FirstOrDefault(item => item.QuestId == selectedQuestId);
        if (selected != null)
        {
            EventSystem.current?.SetSelectedGameObject(selected.gameObject);
            listScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        EventSystem.current?.SetSelectedGameObject(showingCompleted ? completedFilterButton.gameObject : activeFilterButton.gameObject);
    }

    // 当前页签是否应显示给定快照。
    private bool IsVisibleInCurrentFilter(QuestSnapshot snapshot)
    {
        return showingCompleted ? snapshot.State == QuestState.Completed
            : snapshot.State == QuestState.Active || snapshot.State == QuestState.ReadyToTurnIn;
    }

    // 更新两个页签的选中底图。
    private void RefreshFilterVisuals()
    {
        activeFilterSelection.enabled = !showingCompleted;
        completedFilterSelection.enabled = showingCompleted;
    }

    // 清理动态视图，以最新只读快照重新构建。
    private static void ClearViews<T>(List<T> views) where T : Component
    {
        foreach (T view in views)
            if (view != null)
                Destroy(view.gameObject);
        views.Clear();
    }

    // 控制整个模态面板的视觉和交互状态。
    private void SetPanelVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // 设置任务面板打开或关闭后的系统光标状态。
    private void SetCursorForPanel(bool active)
    {
        if (active)
        {
            if (cursorManager != null)
            {
                cursorManagerWasEnabled = cursorManager.enabled;
                cursorManager.enabled = false;
                cursorManager.UnlockCursor();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        if (cursorManager != null)
        {
            cursorManager.enabled = cursorManagerWasEnabled;
            if (cursorManagerWasEnabled)
                cursorManager.LockCursor();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 通过项目状态机与时间缩放统一冻结或恢复世界。
    private static void SetGameState(GameState state)
    {
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(state));
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
    }

    // 将状态转换为详情页使用的文字标签。
    private static string GetStatusText(QuestState state)
    {
        return state == QuestState.ReadyToTurnIn ? "可交付"
            : state == QuestState.Completed ? "已完成"
            : "进行中";
    }
}
