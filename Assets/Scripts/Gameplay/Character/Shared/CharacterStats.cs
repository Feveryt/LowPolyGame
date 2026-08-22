using System;
using UnityEngine;

/// <summary>
/// 玩家和敌人共用的运行时属性基类。
/// 它从 CharacterStatsDefinition 初始化资源，并通过局部 C# 事件向 HUD 和表现层发布变化。
/// </summary>
public abstract class CharacterStats : MonoBehaviour, IDamageable
{
    // 用于初始化本角色数值的静态配置资产。
    [SerializeField] private CharacterStatsDefinition definition;

    // 当前生命值，仅在运行时变化。
    private float currentHealth;
    // 当前体力值，仅在运行时变化。
    private float currentStamina;
    // 当前蓝量，仅在运行时变化。
    private float currentMana;

    /// <summary>任一资源变化时触发，供本角色关联的 HUD 或特效订阅。</summary>
    public event Action<ResourceChangedEvent> ResourceChanged;

    /// <summary>本角色受到有效伤害时触发。</summary>
    public event Action<DamageResult> DamageReceived;

    /// <summary>生命值首次降至零时触发。</summary>
    public event Action<CharacterStats> Died;

    /// <summary>当前使用的静态数值配置。</summary>
    public CharacterStatsDefinition Definition => definition;

    /// <summary>角色的稳定配置 ID；未配置时返回零。</summary>
    public int Id => definition != null ? definition.Id : 0;

    /// <summary>角色基础攻击力；未配置时返回零。</summary>
    public int Attack => definition != null ? definition.Attack : 0;

    /// <summary>角色基础防御力；未配置时返回零。</summary>
    public int Defense => definition != null ? definition.Defense : 0;

    /// <summary>生命值上限；未配置时返回零。</summary>
    public float MaxHealth => definition != null ? definition.MaxHealth : 0f;

    /// <summary>体力上限；未配置时返回零。</summary>
    public float MaxStamina => definition != null ? definition.MaxStamina : 0f;

    /// <summary>蓝量上限；未配置时返回零。</summary>
    public float MaxMana => definition != null ? definition.MaxMana : 0f;

    /// <summary>当前生命值。</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>当前体力值。</summary>
    public float CurrentStamina => currentStamina;

    /// <summary>当前蓝量。</summary>
    public float CurrentMana => currentMana;

    /// <summary>当前是否拥有有效生命值。</summary>
    public bool IsAlive { get; private set; }

    // 根据静态配置初始化运行时资源。
    protected virtual void Awake()
    {
        InitializeRuntimeStats();
    }

    /// <summary>将生命、体力和蓝量恢复到配置上限。</summary>
    public virtual void ResetRuntimeStats()
    {
        InitializeRuntimeStats();
    }

    /// <summary>从存档恢复当前资源，并限制在配置上限内后通知订阅者。</summary>
    public void RestoreFromSave(float health, float stamina, float mana)
    {
        currentHealth = Mathf.Clamp(Mathf.Max(1f, health), 0f, MaxHealth);
        currentStamina = Mathf.Clamp(stamina, 0f, MaxStamina);
        currentMana = Mathf.Clamp(mana, 0f, MaxMana);
        IsAlive = currentHealth > 0f;
        NotifyResourceChanged(ResourceType.Health);
        NotifyResourceChanged(ResourceType.Stamina);
        NotifyResourceChanged(ResourceType.Mana);
    }

    /// <summary>尝试足额消耗体力；资源不足时不修改当前值。</summary>
    public bool TrySpendStamina(float amount)
    {
        return TrySpendResource(ResourceType.Stamina, amount);
    }

    /// <summary>尝试足额消耗蓝量；资源不足时不修改当前值。</summary>
    public bool TrySpendMana(float amount)
    {
        return TrySpendResource(ResourceType.Mana, amount);
    }

    /// <summary>恢复生命值，并限制在生命上限内。</summary>
    public void RestoreHealth(float amount)
    {
        RestoreResource(ResourceType.Health, amount);
    }

    /// <summary>恢复体力，并限制在体力上限内。</summary>
    public void RestoreStamina(float amount)
    {
        RestoreResource(ResourceType.Stamina, amount);
    }

    /// <summary>恢复蓝量，并限制在蓝量上限内。</summary>
    public void RestoreMana(float amount)
    {
        RestoreResource(ResourceType.Mana, amount);
    }

    /// <summary>按统一伤害公式结算请求，并更新当前生命值。</summary>
    public DamageResult TakeDamage(DamageRequest request)
    {
        if (!IsAlive)
            return DamageResult.Ignored;

        DamageResult result = DamageSystem.Calculate(request, Defense);
        if (!result.WasApplied)
            return result;

        SetResource(ResourceType.Health, currentHealth - result.FinalDamage);
        result = result.WithLethal(!IsAlive);
        DamageReceived?.Invoke(result);

        if (result.WasLethal)
            Died?.Invoke(this);

        return result;
    }

    // 供子类处理持续消耗时按当前剩余资源尽可能扣除。
    protected float SpendResourceUpTo(ResourceType resourceType, float amount)
    {
        if (!IsAlive || amount <= 0f)
            return 0f;

        float current = GetCurrentResource(resourceType);
        float spent = Mathf.Min(current, amount);
        if (spent > 0f)
            SetResource(resourceType, current - spent);

        return spent;
    }

    // 使用配置资产将所有运行时资源重置为满值。
    private void InitializeRuntimeStats()
    {
        if (definition == null)
        {
            currentHealth = 0f;
            currentStamina = 0f;
            currentMana = 0f;
            IsAlive = false;
            Debug.LogError($"[{nameof(CharacterStats)}] Missing {nameof(CharacterStatsDefinition)} on {name}.", this);
            return;
        }

        currentHealth = MaxHealth;
        currentStamina = MaxStamina;
        currentMana = MaxMana;
        IsAlive = currentHealth > 0f;
        NotifyResourceChanged(ResourceType.Health);
        NotifyResourceChanged(ResourceType.Stamina);
        NotifyResourceChanged(ResourceType.Mana);
    }

    // 检查指定资源是否足够支付完整消耗。
    private bool TrySpendResource(ResourceType resourceType, float amount)
    {
        if (!IsAlive || amount < 0f)
            return false;

        if (amount <= 0f)
            return true;

        float current = GetCurrentResource(resourceType);
        if (current + Mathf.Epsilon < amount)
            return false;

        SetResource(resourceType, current - amount);
        return true;
    }

    // 向指定资源增加有效数值。
    private void RestoreResource(ResourceType resourceType, float amount)
    {
        if (amount <= 0f)
            return;

        SetResource(resourceType, GetCurrentResource(resourceType) + amount);
    }

    // 返回指定资源的当前运行时数值。
    private float GetCurrentResource(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Health => currentHealth,
            ResourceType.Stamina => currentStamina,
            ResourceType.Mana => currentMana,
            _ => 0f,
        };
    }

    // 更新指定资源并向监听者广播变化。
    private void SetResource(ResourceType resourceType, float value)
    {
        float maximum = GetMaximumResource(resourceType);
        float current = GetCurrentResource(resourceType);
        float clampedValue = Mathf.Clamp(value, 0f, maximum);
        if (Mathf.Approximately(current, clampedValue))
            return;

        switch (resourceType)
        {
            case ResourceType.Health:
                currentHealth = clampedValue;
                IsAlive = currentHealth > 0f;
                break;
            case ResourceType.Stamina:
                currentStamina = clampedValue;
                break;
            case ResourceType.Mana:
                currentMana = clampedValue;
                break;
        }

        NotifyResourceChanged(resourceType);
    }

    // 返回指定资源的配置上限。
    private float GetMaximumResource(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Health => MaxHealth,
            ResourceType.Stamina => MaxStamina,
            ResourceType.Mana => MaxMana,
            _ => 0f,
        };
    }

    // 将指定资源的当前值和上限通知给本角色的监听者。
    private void NotifyResourceChanged(ResourceType resourceType)
    {
        ResourceChanged?.Invoke(new ResourceChangedEvent(
            this,
            resourceType,
            GetCurrentResource(resourceType),
            GetMaximumResource(resourceType)));
    }
}
