using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 基础伤害公式的 EditMode 测试。
/// 覆盖无防御、常规防御、高防御与最低伤害边界，保证公式变更可被及时发现。
/// </summary>
public sealed class DamageSystemEditModeTests
{
    // 验证防御为零时只应用攻击与倍率。
    [Test]
    public void Calculate_WithZeroDefense_ReturnsRoundedRawDamage()
    {
        DamageResult result = DamageSystem.Calculate(new DamageRequest(20f, 1.5f), 0);

        Assert.That(result.RawDamage, Is.EqualTo(30f));
        Assert.That(result.FinalDamage, Is.EqualTo(30));
        Assert.That(result.WasApplied, Is.True);
    }

    // 验证防御等于固定系数时伤害减半。
    [Test]
    public void Calculate_WithHundredDefense_ReducesDamageByHalf()
    {
        DamageResult result = DamageSystem.Calculate(new DamageRequest(20f), 100);

        Assert.That(result.FinalDamage, Is.EqualTo(10));
    }

    // 验证极高防御仍保留至少一点有效伤害。
    [Test]
    public void Calculate_WithExtremeDefense_RespectsMinimumDamage()
    {
        DamageResult result = DamageSystem.Calculate(new DamageRequest(1f), 1_000_000);

        Assert.That(result.FinalDamage, Is.EqualTo(1));
    }

    // 验证非正攻击或倍率不会生成有效伤害。
    [Test]
    public void Calculate_WithNoRawDamage_ReturnsIgnoredResult()
    {
        DamageResult result = DamageSystem.Calculate(new DamageRequest(0f), 0);

        Assert.That(result.WasApplied, Is.False);
        Assert.That(result.FinalDamage, Is.EqualTo(0));
    }

    // 验证资源不足时拒绝完整消耗，且当前资源保持不变。
    [Test]
    public void PlayerStats_WhenResourceIsInsufficient_DoesNotSpendResource()
    {
        CharacterStatsDefinition definition = CreateDefinition();
        PlayerStats stats = CreatePlayerStats(definition, out GameObject gameObject);

        Assert.That(stats.TrySpendMana(40f), Is.True);
        float manaBeforeFailedSpend = stats.CurrentMana;

        Assert.That(stats.TrySpendMana(70f), Is.False);
        Assert.That(stats.CurrentMana, Is.EqualTo(manaBeforeFailedSpend));

        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(definition);
    }

    // 验证恢复操作受最大资源值限制，防止资源溢出。
    [Test]
    public void PlayerStats_WhenRestoringResource_ClampsToMaximum()
    {
        CharacterStatsDefinition definition = CreateDefinition();
        PlayerStats stats = CreatePlayerStats(definition, out GameObject gameObject);

        Assert.That(stats.TrySpendStamina(30f), Is.True);
        stats.RestoreStamina(100f);

        Assert.That(stats.CurrentStamina, Is.EqualTo(stats.MaxStamina));

        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(definition);
    }

    // 创建供资源接口测试使用的临时配置资产。
    private static CharacterStatsDefinition CreateDefinition()
    {
        var definition = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
        var serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("maxHealth").intValue = 100;
        serializedDefinition.FindProperty("maxStamina").floatValue = 100f;
        serializedDefinition.FindProperty("maxMana").floatValue = 100f;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    // 创建已绑定临时配置并完成初始化的玩家属性组件。
    private static PlayerStats CreatePlayerStats(CharacterStatsDefinition definition, out GameObject gameObject)
    {
        gameObject = new GameObject("PlayerStatsTest");
        gameObject.SetActive(false);
        PlayerStats stats = gameObject.AddComponent<PlayerStats>();
        var serializedStats = new SerializedObject(stats);
        serializedStats.FindProperty("definition").objectReferenceValue = definition;
        serializedStats.ApplyModifiedPropertiesWithoutUndo();
        gameObject.SetActive(true);
        stats.ResetRuntimeStats();
        return stats;
    }
}
