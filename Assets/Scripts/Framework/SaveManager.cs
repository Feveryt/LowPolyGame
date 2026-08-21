using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 单槽存档流程的运行时协调器。
/// 负责 JSON 与 PlayerPrefs 存储、玩家状态收集和跨场景恢复，不持有具体 UI 逻辑。
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class SaveManager : MonoBehaviour
{
    // PlayerPrefs 中唯一的单槽存档键。
    private const string SaveKey = "RPG.Save.SingleSlot";
    // 当前可识别的存档结构版本。
    private const int CurrentSchemaVersion = 1;
    // 运行时自动创建的唯一存档管理器。
    private static SaveManager instance;

    // 用于按物品 ID 还原 ItemDefinition 的集中目录。
    private ItemDefinitionCatalog itemCatalog;
    // 启动阶段已校验、等待应用到玩家的有效存档。
    private SaveData pendingSave;
    // 当前等待目标场景玩家初始化完成的读档协程。
    private Coroutine restoreRoutine;

    /// <summary>当前运行时唯一的存档管理器。</summary>
    public static SaveManager Instance => instance;
    /// <summary>是否存在已通过基础版本校验、可在玩家生成后恢复的存档。</summary>
    public bool HasSave => pendingSave != null;
    /// <summary>有效存档是否属于当前激活场景。</summary>
    public bool HasSaveForActiveScene => HasSave &&
        string.Equals(pendingSave.sceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal);

    // 在首个场景加载前自动创建持久化管理器，避免遗漏场景手动挂载。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntimeInstance()
    {
        if (instance != null)
            return;

        GameObject managerObject = new GameObject(nameof(SaveManager));
        managerObject.AddComponent<SaveManager>();
    }

    // 初始化单例、读取物品目录并预校验已有存档。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        itemCatalog = Resources.Load<ItemDefinitionCatalog>(nameof(ItemDefinitionCatalog));
        if (itemCatalog == null)
            Debug.LogError($"[{nameof(SaveManager)}] Missing Resources/{nameof(ItemDefinitionCatalog)} asset.", this);

        LoadPendingSave();
    }

    // 订阅场景加载事件，使从开始菜单进入目标场景时也能恢复存档。
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 取消场景事件订阅，避免重复实例残留回调。
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 仅在存档所属场景加载完成后等待玩家组件并恢复进度。
    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (pendingSave == null || !string.Equals(pendingSave.sceneName, loadedScene.name, StringComparison.Ordinal))
            return;

        if (restoreRoutine != null)
            StopCoroutine(restoreRoutine);

        restoreRoutine = StartCoroutine(RestoreAfterSceneLoad());
    }

    // 应用进入后台时保存可用的玩家进度。
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SaveGame();
    }

    // 应用退出前保存可用的玩家进度。
    private void OnApplicationQuit()
    {
        SaveGame();
    }

    /// <summary>收集当前玩家运行时状态并覆盖本地单槽存档。</summary>
    public bool SaveGame()
    {
        if (!TryFindPlayer(out PlayerStats playerStats, out PlayerInventory playerInventory))
            return false;

        SaveData saveData = CreateSaveData(playerStats, playerInventory);
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
        pendingSave = saveData;
        Debug.Log($"[{nameof(SaveManager)}] Game saved at {saveData.sceneName}.", this);
        return true;
    }

    /// <summary>将已校验的单槽存档恢复到当前场景中的玩家。</summary>
    public bool TryLoadGame()
    {
        if (pendingSave == null && !LoadPendingSave())
            return false;

        if (!TryFindPlayer(out PlayerStats playerStats, out PlayerInventory playerInventory))
            return false;

        if (!string.Equals(pendingSave.sceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal))
            return false;

        ApplyPlayerTransform(playerStats.transform, pendingSave);
        playerStats.RestoreFromSave(pendingSave.currentHealth, pendingSave.currentStamina, pendingSave.currentMana);
        if (pendingSave.inventorySlots.Length > playerInventory.Capacity)
            Debug.LogWarning($"[{nameof(SaveManager)}] Save has more inventory slots than the current capacity; extra slots will be skipped.", this);

        playerInventory.RestoreFromSave(pendingSave.gold, ResolveInventorySlots(pendingSave.inventorySlots));
        Debug.Log($"[{nameof(SaveManager)}] Game loaded from {pendingSave.sceneName}.", this);
        return true;
    }

    /// <summary>删除本地单槽存档并恢复新游戏默认初始化行为。</summary>
    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        pendingSave = null;
        Debug.Log($"[{nameof(SaveManager)}] Single-slot save cleared.", this);
    }

    // 读取 PlayerPrefs 中的 JSON 并执行不依赖场景对象的基础校验。
    private bool LoadPendingSave()
    {
        pendingSave = null;
        if (!PlayerPrefs.HasKey(SaveKey))
            return false;

        try
        {
            SaveData loadedData = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (!IsSaveDataValid(loadedData))
            {
                Debug.LogWarning($"[{nameof(SaveManager)}] Stored save is invalid and will be ignored.", this);
                return false;
            }

            pendingSave = loadedData;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{nameof(SaveManager)}] Failed to parse stored save: {exception.Message}", this);
            return false;
        }
    }

    // 等待当前场景玩家完成 Awake 初始化，再应用已校验的存档。
    private IEnumerator RestoreAfterSceneLoad()
    {
        const int maximumFrames = 120;
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            yield return null;
            if (TryLoadGame())
            {
                restoreRoutine = null;
                yield break;
            }
        }

        restoreRoutine = null;
        Debug.LogWarning($"[{nameof(SaveManager)}] Timed out waiting for player components while loading save.", this);
    }

    // 根据玩家组件构造当前版本的存档数据。
    private static SaveData CreateSaveData(PlayerStats playerStats, PlayerInventory playerInventory)
    {
        Transform playerTransform = playerStats.transform;
        Vector3 position = playerTransform.position;
        IReadOnlyList<InventorySlot> inventorySlots = playerInventory.Slots;
        InventorySlotSaveData[] savedSlots = new InventorySlotSaveData[inventorySlots.Count];

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlot slot = inventorySlots[i];
            savedSlots[i] = !slot.IsEmpty
                ? new InventorySlotSaveData(slot.Item.Id, slot.Quantity)
                : default;
        }

        return new SaveData
        {
            schemaVersion = CurrentSchemaVersion,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            sceneName = SceneManager.GetActiveScene().name,
            playerPositionX = position.x,
            playerPositionY = position.y,
            playerPositionZ = position.z,
            playerYaw = playerTransform.eulerAngles.y,
            currentHealth = playerStats.CurrentHealth,
            currentStamina = playerStats.CurrentStamina,
            currentMana = playerStats.CurrentMana,
            gold = playerInventory.Gold,
            inventorySlots = savedSlots,
        };
    }

    // 将物品 ID 存档数据解析为背包模型使用的配置引用。
    private IReadOnlyList<InventorySlot> ResolveInventorySlots(IReadOnlyList<InventorySlotSaveData> savedSlots)
    {
        List<InventorySlot> resolvedSlots = new List<InventorySlot>(savedSlots?.Count ?? 0);
        if (savedSlots == null)
            return resolvedSlots;

        for (int i = 0; i < savedSlots.Count; i++)
        {
            InventorySlotSaveData savedSlot = savedSlots[i];
            if (savedSlot.itemId <= 0 || savedSlot.quantity <= 0)
            {
                resolvedSlots.Add(default);
                continue;
            }

            if (itemCatalog == null || !itemCatalog.TryGetItem(savedSlot.itemId, out ItemDefinition item))
            {
                Debug.LogWarning($"[{nameof(SaveManager)}] Unknown item ID {savedSlot.itemId} in save slot {i}; skipped.", this);
                resolvedSlots.Add(default);
                continue;
            }

            int quantity = Mathf.Min(savedSlot.quantity, item.MaxStack);
            if (quantity != savedSlot.quantity)
                Debug.LogWarning($"[{nameof(SaveManager)}] Clamped item ID {savedSlot.itemId} quantity in save slot {i}.", this);

            resolvedSlots.Add(new InventorySlot(item, quantity));
        }

        return resolvedSlots;
    }

    // 恢复玩家位置并只写入纯水平朝向。
    private static void ApplyPlayerTransform(Transform playerTransform, SaveData saveData)
    {
        playerTransform.SetPositionAndRotation(
            saveData.PlayerPosition,
            Quaternion.Euler(0f, saveData.playerYaw, 0f));
    }

    // 检查存档版本、场景名和数组字段是否满足当前首版要求。
    private static bool IsSaveDataValid(SaveData saveData)
    {
        return saveData != null
            && saveData.schemaVersion == CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(saveData.sceneName)
            && saveData.inventorySlots != null;
    }

    // 查找同一名玩家对象上的数值与背包组件。
    private static bool TryFindPlayer(out PlayerStats playerStats, out PlayerInventory playerInventory)
    {
        playerStats = FindFirstObjectByType<PlayerStats>();
        playerInventory = playerStats != null ? playerStats.GetComponent<PlayerInventory>() : null;
        return playerStats != null && playerInventory != null;
    }

#if UNITY_EDITOR
    // 为开发阶段提供安全的单槽清档入口。
    [UnityEditor.MenuItem("RPG/Save/Clear Single Slot Save")]
    private static void ClearSingleSlotSaveFromMenu()
    {
        if (instance != null)
        {
            instance.ClearSave();
            return;
        }

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log($"[{nameof(SaveManager)}] Single-slot save cleared.");
    }
#endif
}
