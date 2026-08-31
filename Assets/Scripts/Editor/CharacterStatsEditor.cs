using UnityEditor;
using UnityEngine;

/// <summary>
/// CharacterStats 的运行时调试 Inspector。
/// 通过公开的安全接口修改当前生命值，避免绕过资源事件、边界限制与死亡状态更新。
/// </summary>
[CustomEditor(typeof(CharacterStats), true)]
public sealed class CharacterStatsEditor : Editor
{
    // 绘制除原始生命字段外的默认 Inspector，并补充运行时安全生命值输入框。
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "currentHealth");
        serializedObject.ApplyModifiedProperties();

        CharacterStats stats = (CharacterStats)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("运行时调试", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUI.BeginChangeCheck();
            float health = EditorGUILayout.FloatField("当前生命值", stats.CurrentHealth);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(stats, "修改当前生命值");
                stats.SetHealthForDebug(health);
                EditorUtility.SetDirty(stats);
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("当前生命值仅可在 Play Mode 中修改；角色初始化时会按数值配置重置资源。", MessageType.Info);
    }
}
