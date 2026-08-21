using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 绑定在 NPC 场景对象上的对话交互组件。
/// 仅距离玩家最近且位于交互范围内的 NPC 会显示提示并响应交互键。
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcDialogueInteractor : MonoBehaviour
{
    // 已启用的 NPC 对话组件，用于每帧选出最近目标。
    private static readonly List<NpcDialogueInteractor> ActiveInteractors = new List<NpcDialogueInteractor>();
    // 当前帧距离玩家最近的有效 NPC。
    private static NpcDialogueInteractor closestInteractor;
    // 防止同一帧由多个 NPC 重复计算最近目标。
    private static int lastSelectionFrame = -1;

    // 此 NPC 使用的独立对话资产。
    [SerializeField] private DialogueAsset dialogue;
    // 玩家可开始交互的水平距离，单位米。
    [SerializeField, Min(0.1f)] private float interactionRange = 1f;
    // 可选的玩家 Transform，空时自动查找 PlayerController。
    [SerializeField] private Transform player;

    // 玩家输入组件缓存，用于订阅交互动作。
    private InputManager inputManager;

    /// <summary>绑定到 NPC 的对话资产。</summary>
    public DialogueAsset Dialogue => dialogue;

    // 注册场景交互对象。
    private void OnEnable()
    {
        if (!ActiveInteractors.Contains(this))
            ActiveInteractors.Add(this);
    }

    // 取消注册并在必要时隐藏遗留提示。
    private void OnDisable()
    {
        ActiveInteractors.Remove(this);
        if (closestInteractor == this)
        {
            closestInteractor = null;
            DialoguePromptUI.Instance.Hide();
        }

        if (inputManager != null)
            inputManager.InteractPressed -= TryInteract;
        inputManager = null;
    }

    // 刷新输入引用、最近目标和按键提示。
    private void Update()
    {
        AttachInputManager();
        RefreshClosestInteractor();

        if (lastSelectionFrame == Time.frameCount)
        {
            if (DialogueManager.Instance.IsOpen || closestInteractor == null)
                DialoguePromptUI.Instance.Hide();
            else
                DialoguePromptUI.Instance.Show($"按 E 与 {closestInteractor.GetNpcName()} 对话");
        }
    }

    // 在本帧所有已启用组件中选出范围内距离最近的 NPC。
    private static void RefreshClosestInteractor()
    {
        if (lastSelectionFrame == Time.frameCount)
            return;

        lastSelectionFrame = Time.frameCount;
        closestInteractor = null;
        float closestDistance = float.MaxValue;
        foreach (NpcDialogueInteractor interactor in ActiveInteractors)
        {
            if (interactor == null || !interactor.TryGetPlayer(out Transform playerTransform) || interactor.dialogue == null)
                continue;

            Vector3 offset = interactor.transform.position - playerTransform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            float range = interactor.interactionRange;
            if (distance <= range * range && distance < closestDistance)
            {
                closestDistance = distance;
                closestInteractor = interactor;
            }
        }
    }

    // 监听玩家输入组件在运行时生成或切换后的交互事件。
    private void AttachInputManager()
    {
        InputManager found = FindFirstObjectByType<InputManager>();
        if (found == inputManager)
            return;

        if (inputManager != null)
            inputManager.InteractPressed -= TryInteract;

        inputManager = found;
        if (inputManager != null)
            inputManager.InteractPressed += TryInteract;
    }

    // 仅最近有效 NPC 响应 E 或手柄南键。
    private void TryInteract()
    {
        if (closestInteractor == this && dialogue != null && !DialogueManager.Instance.IsOpen)
            DialogueManager.Instance.Begin(dialogue);
    }

    // 解析 Inspector 绑定或场景玩家控制器的 Transform。
    private bool TryGetPlayer(out Transform playerTransform)
    {
        if (player == null)
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            if (controller != null)
                player = controller.transform;
        }

        playerTransform = player;
        return playerTransform != null;
    }

    // 返回用于按键提示的 NPC 名称。
    private string GetNpcName()
    {
        return dialogue != null && !string.IsNullOrWhiteSpace(dialogue.NpcName) ? dialogue.NpcName : name;
    }

    // 在场景视图中标示配置的交互范围。
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
