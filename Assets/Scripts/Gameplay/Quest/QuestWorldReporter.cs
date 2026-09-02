using UnityEngine;

/// <summary>把场景交互物与敌人死亡转换为任务服务可监听的稳定事件。</summary>
public sealed class QuestWorldReporter : MonoBehaviour
{
    [SerializeField] private string targetId;
    [SerializeField] private QuestObjectiveType eventType;
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField] private Transform player;
    private InputManager inputManager;
    private CharacterStats characterStats;
    private bool consumed;

    /// <summary>目标事件是否已在本次场景生命周期内上报。</summary>
    public bool Consumed => consumed;

    // 订阅敌人死亡；交互物的输入引用在 Update 中按需解析。
    private void Awake()
    {
        characterStats = GetComponent<CharacterStats>();
        if (eventType == QuestObjectiveType.EnemyKilled && characterStats != null)
            characterStats.Died += OnDied;
    }

    // 防止对象销毁后保留死亡订阅。
    private void OnDestroy()
    {
        if (characterStats != null)
            characterStats.Died -= OnDied;
        if (inputManager != null)
            inputManager.InteractPressed -= TryInteract;
    }

    // 为交互物连接输入，并检查玩家距离。
    private void Update()
    {
        if (eventType != QuestObjectiveType.Interaction || consumed)
            return;
        AttachInput();
    }

    // 仅在目标为当前活跃交互距离内时提交事件。
    private void TryInteract()
    {
        if (consumed || !TryGetPlayer(out Transform playerTransform))
            return;
        Vector3 offset = transform.position - playerTransform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > interactionRange * interactionRange)
            return;
        if (!QuestService.Instance.NotifyInteraction(targetId))
            return;
        consumed = true;
        gameObject.SetActive(false);
    }

    // 将敌人死亡事件上报一次。
    private void OnDied(CharacterStats _)
    {
        if (consumed)
            return;
        consumed = QuestService.Instance.NotifyEnemyKilled(targetId);
    }

    // 解析并订阅当前场景的输入管理器。
    private void AttachInput()
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

    // 解析 Inspector 指定或场景中的玩家角色。
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
}
