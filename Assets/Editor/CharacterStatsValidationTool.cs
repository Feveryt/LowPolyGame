using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 角色数值配置校验工具。
/// 扫描 CharacterStatsDefinition 资产，报告重复 ID、无效属性和不匹配的体力配置。
/// </summary>
public static class CharacterStatsValidationTool
{
    // 角色数值配置资产的唯一存放目录。
    private const string DefinitionsFolder = "Assets/GameData/Definitions/Characters";

    /// <summary>从 Unity 菜单执行所有角色数值配置校验。</summary>
    [MenuItem("RPG/Validation/Validate Character Stats")]
    public static void ValidateDefinitions()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:CharacterStatsDefinition", new[] { DefinitionsFolder });
        var definitionsById = new Dictionary<int, CharacterStatsDefinition>();
        int errorCount = 0;

        foreach (string assetGuid in assetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuid);
            CharacterStatsDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterStatsDefinition>(path);
            if (definition == null)
                continue;

            errorCount += ValidateDefinition(definition, definitionsById);
        }

        if (errorCount == 0)
            Debug.Log($"[CharacterStatsValidation] Validated {assetGuids.Length} character stat definitions with no errors.");
        else
            Debug.LogError($"[CharacterStatsValidation] Found {errorCount} error(s) in {assetGuids.Length} character stat definitions.");
    }

    // 校验单个资产并记录已出现的 ID。
    private static int ValidateDefinition(
        CharacterStatsDefinition definition,
        IDictionary<int, CharacterStatsDefinition> definitionsById)
    {
        int errorCount = 0;

        if (definition.Id <= 0)
            errorCount += LogError(definition, "ID must be greater than zero.");

        if (definitionsById.TryGetValue(definition.Id, out CharacterStatsDefinition duplicate))
        {
            errorCount += LogError(
                definition,
                $"Duplicate ID {definition.Id}; it is already used by '{duplicate.name}'.");
        }
        else
        {
            definitionsById.Add(definition.Id, definition);
        }

        if (definition.MaxHealth <= 0f)
            errorCount += LogError(definition, "Max Health must be greater than zero.");
        if (definition.Attack < 0 || definition.Defense < 0)
            errorCount += LogError(definition, "Attack and Defense cannot be negative.");
        if (definition.MaxStamina < 0f || definition.MaxMana < 0f)
            errorCount += LogError(definition, "Maximum resources cannot be negative.");

        bool hasStaminaRules = definition.StaminaDrainPerSecond > 0f
            || definition.StaminaRecoveryPerSecond > 0f
            || definition.StaminaRecoveryDelay > 0f
            || definition.StaminaResumeThreshold > 0f;
        if (definition.MaxStamina <= 0f && hasStaminaRules)
            errorCount += LogError(definition, "Stamina rules are configured while Max Stamina is zero.");
        if (definition.MaxStamina > 0f && definition.StaminaResumeThreshold > definition.MaxStamina)
            errorCount += LogError(definition, "Stamina resume threshold cannot exceed Max Stamina.");

        return errorCount;
    }

    // 输出带有可定位资产引用的校验错误。
    private static int LogError(CharacterStatsDefinition definition, string message)
    {
        Debug.LogError($"[CharacterStatsValidation] {definition.name}: {message}", definition);
        return 1;
    }
}
