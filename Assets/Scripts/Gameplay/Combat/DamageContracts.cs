using UnityEngine;

/// <summary>
/// 可接收伤害对象的统一接口。
/// 命中检测、普通攻击和技能只依赖此接口，不直接依赖玩家或敌人具体类型。
/// </summary>
public interface IDamageable
{
    /// <summary>目标当前是否仍存活。</summary>
    bool IsAlive { get; }

    /// <summary>结算一次伤害请求并返回结果。</summary>
    DamageResult TakeDamage(DamageRequest request);
}

/// <summary>
/// 角色可变化资源的类型。
/// </summary>
public enum ResourceType
{
    /// <summary>生命资源。</summary>
    Health,
    /// <summary>奔跑等动作消耗的体力资源。</summary>
    Stamina,
    /// <summary>技能消耗的蓝量资源。</summary>
    Mana,
}

/// <summary>
/// 一次伤害计算所需的攻击数值与倍率。
/// </summary>
public readonly struct DamageRequest
{
    /// <summary>攻击者在本次结算中提供的基础攻击数值。</summary>
    public float AttackPower { get; }

    /// <summary>攻击、重击或技能附带的伤害倍率。</summary>
    public float Multiplier { get; }

    /// <summary>本次伤害对应的攻击类别。</summary>
    public AttackType AttackType { get; }
    /// <summary>本次伤害对应的攻击配置 ID。</summary>
    public int AttackId { get; }
    /// <summary>造成伤害的来源对象。</summary>
    public object Source { get; }

    /// <summary>使用基础攻击力和可选倍率创建伤害请求。</summary>
    public DamageRequest(float attackPower, float multiplier = 1f)
        : this(attackPower, multiplier, AttackType.Skill, 0, null)
    {
    }

    /// <summary>创建包含攻击类别、配置 ID 和来源的完整伤害请求。</summary>
    public DamageRequest(float attackPower, float multiplier, AttackType attackType, int attackId, object source = null)
    {
        AttackPower = attackPower;
        Multiplier = multiplier;
        AttackType = attackType;
        AttackId = attackId;
        Source = source;
    }
}

/// <summary>
/// 一次伤害计算和应用后的结果。
/// </summary>
public readonly struct DamageResult
{
    /// <summary>防御减伤前的原始伤害值。</summary>
    public float RawDamage { get; }

    /// <summary>四舍五入并应用最低伤害限制后的最终伤害。</summary>
    public int FinalDamage { get; }

    /// <summary>本次请求是否造成了有效伤害。</summary>
    public bool WasApplied { get; }

    /// <summary>本次伤害是否使目标生命值降至零。</summary>
    public bool WasLethal { get; }

    /// <summary>表示无有效伤害的结果。</summary>
    public static DamageResult Ignored => new(0f, 0, false, false);

    /// <summary>使用计算出的伤害值创建结果。</summary>
    public DamageResult(float rawDamage, int finalDamage, bool wasApplied, bool wasLethal)
    {
        RawDamage = rawDamage;
        FinalDamage = finalDamage;
        WasApplied = wasApplied;
        WasLethal = wasLethal;
    }

    /// <summary>返回包含目标死亡状态的新结果。</summary>
    public DamageResult WithLethal(bool wasLethal)
    {
        return new DamageResult(RawDamage, FinalDamage, WasApplied, wasLethal);
    }
}

/// <summary>
/// 角色资源变化通知。
/// 由 CharacterStats 局部广播，HUD 和表现层可按资源类型刷新自身显示。
/// </summary>
public readonly struct ResourceChangedEvent
{
    /// <summary>发生资源变化的角色。</summary>
    public CharacterStats Source { get; }

    /// <summary>发生变化的资源类型。</summary>
    public ResourceType ResourceType { get; }

    /// <summary>资源变化后的当前值。</summary>
    public float Current { get; }

    /// <summary>资源对应的最大值。</summary>
    public float Maximum { get; }

    /// <summary>供进度条使用的零到一归一化值。</summary>
    public float NormalizedValue => Maximum <= 0f ? 0f : Current / Maximum;

    /// <summary>使用资源的来源、当前值与上限创建通知。</summary>
    public ResourceChangedEvent(CharacterStats source, ResourceType resourceType, float current, float maximum)
    {
        Source = source;
        ResourceType = resourceType;
        Current = current;
        Maximum = maximum;
    }
}
