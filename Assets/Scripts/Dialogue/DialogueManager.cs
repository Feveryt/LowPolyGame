using QFramework;
using UnityEngine;

/// <summary>
/// 对话运行时流程的唯一入口。
/// 负责节点推进、分支选择、事件广播、输入状态与完成状态的切换。
/// </summary>
public sealed class DialogueManager : MonoBehaviour
{
    // Resources 中全局玩家展示资料的加载路径。
    private const string PresentationSettingsPath = "Dialogue/DialoguePresentationSettings";
    // 懒加载的全局运行时实例。
    private static DialogueManager instance;

    // 当前展示的对话资产。
    private DialogueAsset activeDialogue;
    // 当前正在显示的台词节点。
    private DialogueNode currentNode;
    // 玩家回答显示完毕后要进入的目标节点。
    private int pendingTargetNodeId = -1;
    // 项目共享的玩家头像与名称设置。
    private DialoguePresentationSettings presentationSettings;
    // 运行时自动创建的对话 UGUI。
    private DialoguePanel panel;
    // 负责切换 Player/UI 输入映射的玩家输入组件。
    private InputManager inputManager;
    // 对话期间暂时禁用的光标管理器。
    private CursorManager cursorManager;
    // 光标管理器在打开对话前的启用状态。
    private bool cursorManagerWasEnabled;

    /// <summary>全局对话管理器实例。</summary>
    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
            {
                var root = new GameObject(nameof(DialogueManager));
                instance = root.AddComponent<DialogueManager>();
            }

            return instance;
        }
    }

    /// <summary>当前是否有对话正在占用 UI 与玩家输入。</summary>
    public bool IsOpen => activeDialogue != null;

    // 维护单例并加载全局展示配置。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        presentationSettings = Resources.Load<DialoguePresentationSettings>(PresentationSettingsPath);
    }

    // 销毁时解除输入订阅，避免常驻管理器遗留回调。
    private void OnDestroy()
    {
        if (inputManager != null)
            inputManager.UiCancelPressed -= CancelDialogue;

        if (instance == this)
            instance = null;
    }

    /// <summary>从场景 NPC 开始一段对话，已完成资产则播放结束栏。</summary>
    public void Begin(DialogueAsset dialogue)
    {
        if (IsOpen || dialogue == null)
            return;

        dialogue.EnsureDialogueId();
        if (DialogueProgressStore.IsCompleted(dialogue.DialogueId))
        {
            Open(dialogue);
            ShowCompletionText();
            return;
        }

        DialogueNode entry = dialogue.GetNode(dialogue.EntryNodeId);
        if (entry == null)
        {
            Debug.LogWarning($"[DialogueManager] 对话 {dialogue.name} 缺少有效入口节点。", dialogue);
            return;
        }

        Open(dialogue);
        ShowNode(entry);
    }

    /// <summary>主动关闭当前对话并恢复玩家输入。</summary>
    public void CancelDialogue()
    {
        if (!IsOpen)
            return;

        panel?.Hide();
        activeDialogue = null;
        currentNode = null;
        pendingTargetNodeId = -1;
        RestoreGameplay();
    }

    // 打开 UGUI 并切换为不暂停世界的对话输入状态。
    private void Open(DialogueAsset dialogue)
    {
        activeDialogue = dialogue;
        EnsurePanel();
        AttachInputManager();
        inputManager?.SetPlayerInputEnabled(false);
        inputManager?.SetUiInputEnabled(true);
        inputManager?.SetLookInputEnabled(false);
        SetCursorForDialogue(true);
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(GameState.Cutscene));
    }

    // 显示普通台词节点，并在进入节点后广播配置事件。
    private void ShowNode(DialogueNode node)
    {
        currentNode = node;
        pendingTargetNodeId = -1;
        ResolveSpeaker(node.Speaker, out string speakerName, out Sprite portrait, out DialoguePortraitSide portraitSide);
        panel.ShowLine(speakerName, node.Text, portrait, portraitSide, AdvanceCurrentNode);
        SendNodeEvent(node);

        if (node.Speaker == DialogueSpeaker.Npc && node.Choices.Count > 0)
            panel.ShowChoices(node.Choices, SelectChoice);
    }

    // 显示已完成对话的结束栏，不会重复发送完成事件。
    private void ShowCompletionText()
    {
        ResolveSpeaker(DialogueSpeaker.Npc, out string speakerName, out Sprite portrait, out DialoguePortraitSide portraitSide);
        string text = string.IsNullOrWhiteSpace(activeDialogue.CompletionText)
            ? "我已经没有更多要说的了。"
            : activeDialogue.CompletionText;
        panel.ShowLine(speakerName, text, portrait, portraitSide, CancelDialogue);
    }

    // 推进没有选项的普通节点，叶子节点在确认后才标记完成。
    private void AdvanceCurrentNode()
    {
        if (currentNode == null)
        {
            CancelDialogue();
            return;
        }

        if (currentNode.NextNodeId < 0)
        {
            CompleteDialogue();
            return;
        }

        ShowTargetNode(currentNode.NextNodeId);
    }

    // 先显示玩家选择的回答，再在确认后进入其配置的目标节点。
    private void SelectChoice(int choiceIndex)
    {
        if (currentNode == null || choiceIndex < 0 || choiceIndex >= currentNode.Choices.Count)
            return;

        DialogueChoice choice = currentNode.Choices[choiceIndex];
        pendingTargetNodeId = choice.TargetNodeId;
        ResolveSpeaker(DialogueSpeaker.Player, out string speakerName, out Sprite portrait, out DialoguePortraitSide portraitSide);
        panel.ShowLine(speakerName, choice.Text, portrait, portraitSide, AdvanceAfterChoice);
    }

    // 根据选项目标继续显示节点，或在选项直接结束时标记完成。
    private void AdvanceAfterChoice()
    {
        if (pendingTargetNodeId < 0)
        {
            CompleteDialogue();
            return;
        }

        ShowTargetNode(pendingTargetNodeId);
    }

    // 验证跳转目标并展示对应节点。
    private void ShowTargetNode(int nodeId)
    {
        DialogueNode target = activeDialogue != null ? activeDialogue.GetNode(nodeId) : null;
        if (target == null)
        {
            Debug.LogWarning($"[DialogueManager] 对话 {activeDialogue?.name} 指向了不存在的节点 {nodeId}。", activeDialogue);
            CancelDialogue();
            return;
        }

        ShowNode(target);
    }

    // 首次完成时记录进度，发送一次完成事件并结束此次交互。
    private void CompleteDialogue()
    {
        if (activeDialogue != null && !DialogueProgressStore.IsCompleted(activeDialogue.DialogueId))
        {
            DialogueProgressStore.MarkCompleted(activeDialogue.DialogueId);
            if (!string.IsNullOrWhiteSpace(activeDialogue.CompletionEventId))
            {
                GameArchitecture.Interface.SendEvent(new DialogueNodeEvent(
                    activeDialogue.CompletionEventId,
                    activeDialogue.DialogueId,
                    -1));
            }
        }

        CancelDialogue();
    }

    // 从资产和全局设置中解析当前说话者的展示资料。
    private void ResolveSpeaker(DialogueSpeaker speaker, out string speakerName, out Sprite portrait, out DialoguePortraitSide portraitSide)
    {
        if (speaker == DialogueSpeaker.Npc)
        {
            speakerName = string.IsNullOrWhiteSpace(activeDialogue.NpcName) ? "NPC" : activeDialogue.NpcName;
            portrait = activeDialogue.NpcPortrait;
            portraitSide = activeDialogue.NpcPortraitSide;
            return;
        }

        speakerName = presentationSettings != null && !string.IsNullOrWhiteSpace(presentationSettings.PlayerName)
            ? presentationSettings.PlayerName
            : "Player";
        portrait = presentationSettings != null ? presentationSettings.PlayerPortrait : null;
        portraitSide = presentationSettings != null ? presentationSettings.PlayerPortraitSide : DialoguePortraitSide.Right;
    }

    // 向 QFramework 广播节点进入事件，空事件 ID 不产生通知。
    private void SendNodeEvent(DialogueNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.EventId))
            GameArchitecture.Interface.SendEvent(new DialogueNodeEvent(node.EventId, activeDialogue.DialogueId, node.NodeId));
    }

    // 创建或查找默认对话面板。
    private void EnsurePanel()
    {
        if (panel == null)
            panel = FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);

        if (panel == null)
        {
            var root = new GameObject(nameof(DialoguePanel));
            panel = root.AddComponent<DialoguePanel>();
        }
    }

    // 绑定当前玩家的输入管理器，并监听 UI 取消动作。
    private void AttachInputManager()
    {
        InputManager found = FindFirstObjectByType<InputManager>();
        if (found == inputManager)
            return;

        if (inputManager != null)
            inputManager.UiCancelPressed -= CancelDialogue;

        inputManager = found;
        if (inputManager != null)
            inputManager.UiCancelPressed += CancelDialogue;
    }

    // 对话结束后恢复 Player 映射、光标和游戏状态。
    private void RestoreGameplay()
    {
        inputManager?.SetUiInputEnabled(false);
        inputManager?.SetPlayerInputEnabled(true);
        inputManager?.SetLookInputEnabled(true);
        SetCursorForDialogue(false);
        GameArchitecture.Interface.SendCommand(new ChangeGameStateCommand(GameState.Playing));
    }

    // 处理对话期间鼠标可点击 UI 的锁定状态。
    private void SetCursorForDialogue(bool active)
    {
        if (active)
        {
            cursorManager = FindFirstObjectByType<CursorManager>();
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
}
