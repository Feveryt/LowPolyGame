using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最小角色数值 HUD。
/// 订阅 PlayerStats 的局部资源事件，显示生命、体力和蓝量；后续完整 HUD 可在此基础上扩展。
/// </summary>
public class UIHUD : UIPanel
{
    // HUD 绑定的场景玩家运行时属性组件。
    [SerializeField] private PlayerStats playerStats;
    // 显示当前生命比例的进度条。
    [SerializeField] private Slider healthSlider;
    // 显示当前体力比例的进度条。
    [SerializeField] private Slider staminaSlider;
    // 显示当前蓝量比例的进度条。
    [SerializeField] private Slider manaSlider;
    // 显示当前生命和上限数值的文本。
    [SerializeField] private Text healthValueText;
    // 显示当前体力和上限数值的文本。
    [SerializeField] private Text staminaValueText;
    // 显示当前蓝量和上限数值的文本。
    [SerializeField] private Text manaValueText;

    // 确认 Inspector 引用属于当前场景，否则绑定当前场景中唯一的玩家属性组件。
    private void Awake()
    {
        if (playerStats == null || !playerStats.gameObject.scene.IsValid())
            playerStats = FindFirstObjectByType<PlayerStats>();
    }

    // 启用时订阅资源变化并刷新初始显示。
    private void OnEnable()
    {
        SubscribeToPlayerStats();
        RefreshAll();
    }

    // 禁用时取消订阅，避免重复注册和无效回调。
    private void OnDisable()
    {
        UnsubscribeFromPlayerStats();
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
    }

    // 同步资源 Slider 的归一化进度与当前值/上限文本。
    private static void SetResourceView(Slider slider, Text valueText, float current, float maximum)
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = maximum <= 0f ? 0f : current / maximum;
        }

        if (valueText != null)
            valueText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}";
    }
}
