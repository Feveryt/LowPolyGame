using UnityEngine;

/// <summary>
/// 角色静态数值配置。
/// 该资产只保存初始值、上限与体力规则；当前资源值由 CharacterStats 在运行时维护。
/// </summary>
[CreateAssetMenu(fileName = "CharacterStats_", menuName = "RPG/Stats/Character Stats Definition")]
public sealed class CharacterStatsDefinition : ScriptableObject
{
    [Header("Identity")]
    // 用于配置校验、存档和后续生成器查询的稳定角色 ID。
    [SerializeField, Min(1)] private int id = 1;
    // Inspector 与后续 UI 显示使用的角色名称。
    [SerializeField] private string displayName = "New Character";

    [Header("Base Attributes")]
    // 角色满生命值上限。
    [SerializeField, Min(1)] private int maxHealth = 100;
    // 参与攻击伤害结算的基础攻击力。
    [SerializeField, Min(0)] private int attack = 20;
    // 参与伤害减免结算的基础防御力。
    [SerializeField, Min(0)] private int defense = 10;
    // 角色体力资源上限；设为零表示该角色不使用体力。
    [SerializeField, Min(0f)] private float maxStamina = 100f;
    // 角色蓝量资源上限；设为零表示该角色不使用蓝量。
    [SerializeField, Min(0f)] private float maxMana = 100f;

    [Header("Stamina")]
    // 实际奔跑时每秒扣除的体力值。
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 20f;
    // 停止消耗后每秒恢复的体力值。
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 25f;
    // 停止消耗体力到开始恢复之间的等待时间，单位为秒。
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1f;
    // 体力耗尽后重新允许奔跑所需的最低体力值。
    [SerializeField, Min(0f)] private float staminaResumeThreshold = 10f;

    /// <summary>配置资产的稳定角色 ID。</summary>
    public int Id => id;

    /// <summary>配置资产的显示名称。</summary>
    public string DisplayName => displayName;

    /// <summary>角色满生命值上限。</summary>
    public int MaxHealth => maxHealth;

    /// <summary>角色基础攻击力。</summary>
    public int Attack => attack;

    /// <summary>角色基础防御力。</summary>
    public int Defense => defense;

    /// <summary>角色体力资源上限。</summary>
    public float MaxStamina => maxStamina;

    /// <summary>角色蓝量资源上限。</summary>
    public float MaxMana => maxMana;

    /// <summary>奔跑时每秒消耗的体力。</summary>
    public float StaminaDrainPerSecond => staminaDrainPerSecond;

    /// <summary>停止消耗后每秒恢复的体力。</summary>
    public float StaminaRecoveryPerSecond => staminaRecoveryPerSecond;

    /// <summary>体力恢复开始前的等待时间。</summary>
    public float StaminaRecoveryDelay => staminaRecoveryDelay;

    /// <summary>体力耗尽后重新允许奔跑的阈值。</summary>
    public float StaminaResumeThreshold => staminaResumeThreshold;

    // 在 Inspector 修改后将阈值限制在对应资源上限内。
    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        attack = Mathf.Max(0, attack);
        defense = Mathf.Max(0, defense);
        maxStamina = Mathf.Max(0f, maxStamina);
        maxMana = Mathf.Max(0f, maxMana);
        staminaDrainPerSecond = Mathf.Max(0f, staminaDrainPerSecond);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
        staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
        staminaResumeThreshold = Mathf.Clamp(staminaResumeThreshold, 0f, maxStamina);
    }
}
