using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 最小角色数值 HUD。
/// 订阅 PlayerStats 的局部资源事件，显示生命、体力和蓝量；后续完整 HUD 可在此基础上扩展。
/// </summary>
public class UIHUD : UIPanel
{
    // HUD 绑定的场景玩家运行时属性组件。
    [SerializeField] private PlayerStats playerStats;
    // HUD 绑定的玩家背包组件，用于显示金币。
    [SerializeField] private PlayerInventory playerInventory;
    // 显示当前生命比例的进度条。
    [SerializeField] private Slider healthSlider;
    // 显示当前体力比例的进度条。
    [SerializeField] private Slider staminaSlider;
    // 显示当前蓝量比例的进度条。
    [SerializeField] private Slider manaSlider;
    // 显示当前生命和上限数值的文本。
    [SerializeField] private Graphic healthValueText;
    // 显示当前体力和上限数值的文本。
    [SerializeField] private Graphic staminaValueText;
    // 显示当前蓝量和上限数值的文本。
    [SerializeField] private Graphic manaValueText;
    // 显示当前金币数量的文本。
    [SerializeField] private Graphic goldValueText;

    // 确认 Inspector 引用属于当前场景，否则绑定当前场景中唯一的玩家属性组件。
    private void Awake()
    {
        if (playerStats == null || !playerStats.gameObject.scene.IsValid())
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerInventory == null || !playerInventory.gameObject.scene.IsValid())
            playerInventory = FindFirstObjectByType<PlayerInventory>();
    }

    // 启用时订阅资源变化并刷新初始显示。
    private void OnEnable()
    {
        SubscribeToPlayerStats();
        SubscribeToPlayerInventory();
        RefreshAll();
    }

    // 禁用时取消订阅，避免重复注册和无效回调。
    private void OnDisable()
    {
        UnsubscribeFromPlayerStats();
        UnsubscribeFromPlayerInventory();
    }

    // 注册玩家资源变化事件。
    private void SubscribeToPlayerStats()
    {
        if (playerStats != null)
            playerStats.ResourceChanged += OnResourceChanged;
    }

    // 取消注册玩家资源变化事件。
    private void UnsubscribeFromPlayerStats()
    {
        if (playerStats != null)
            playerStats.ResourceChanged -= OnResourceChanged;
    }

    // 注册玩家金币变化事件。
    private void SubscribeToPlayerInventory()
    {
        if (playerInventory != null)
            playerInventory.GoldChanged += OnGoldChanged;
    }

    // 取消注册玩家金币变化事件。
    private void UnsubscribeFromPlayerInventory()
    {
        if (playerInventory != null)
            playerInventory.GoldChanged -= OnGoldChanged;
    }

    // 根据变更资源类型刷新对应进度条。
    private void OnResourceChanged(ResourceChangedEvent changeEvent)
    {
        if (changeEvent.Source != playerStats)
            return;

        switch (changeEvent.ResourceType)
        {
            case ResourceType.Health:
                SetResourceView(healthSlider, healthValueText, changeEvent.Current, changeEvent.Maximum);
                break;
            case ResourceType.Stamina:
                SetResourceView(staminaSlider, staminaValueText, changeEvent.Current, changeEvent.Maximum);
                break;
            case ResourceType.Mana:
                SetResourceView(manaSlider, manaValueText, changeEvent.Current, changeEvent.Maximum);
                break;
        }
    }

    // 刷新三个资源条的完整初始状态。
    private void RefreshAll()
    {
        if (playerStats == null)
            return;

        SetResourceView(healthSlider, healthValueText, playerStats.CurrentHealth, playerStats.MaxHealth);
        SetResourceView(staminaSlider, staminaValueText, playerStats.CurrentStamina, playerStats.MaxStamina);
        SetResourceView(manaSlider, manaValueText, playerStats.CurrentMana, playerStats.MaxMana);
        RefreshGold();
    }

    // 接收背包金币变化并刷新 HUD 显示。
    private void OnGoldChanged(int gold)
    {
        if (goldValueText != null)
            SetText(goldValueText, gold.ToString());
    }

    // 刷新当前玩家的初始金币显示。
    private void RefreshGold()
    {
        if (playerInventory != null)
            OnGoldChanged(playerInventory.Gold);
    }

    // 同步资源 Slider 的归一化进度与当前值/上限文本。
    private static void SetResourceView(Slider slider, Graphic valueText, float current, float maximum)
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = maximum <= 0f ? 0f : current / maximum;
        }

        SetText(valueText, $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}");
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
