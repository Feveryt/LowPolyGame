using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 创建首版 SoundLibrary，并把 AudioManager 和石头守卫音效协调器安装到可运行场景。
/// </summary>
public static class AudioSetup
{
    private const string LibraryPath = "Assets/Resources/Audio/SoundLibrary.asset";
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/GameScene/GameStart.unity",
        "Assets/Scenes/GameScene/TestAct.unity",
        "Assets/Scenes/GameScene/Demo 1.unity",
    };

    /// <summary>从菜单创建并绑定首版核心音效资产与场景组件。</summary>
    // 使用中文根菜单以适配当前中文 Unity 编辑器；英文菜单不会自动映射到“工具”。
    [MenuItem("工具/音频/安装核心音效")]
    [MenuItem("工具/Audio/Install Core Audio")]
    public static void InstallCoreAudio()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string originalScenePath = SceneManager.GetActiveScene().path;
        SoundLibrary library = GetOrCreateLibrary();
        ConfigureLibrary(library);
        AssetDatabase.SaveAssets();

        for (int index = 0; index < ScenePaths.Length; index++)
            InstallSceneComponents(ScenePaths[index]);

        if (!string.IsNullOrWhiteSpace(originalScenePath) && File.Exists(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AudioSetup] Core audio library and scene components installed.");
    }

    // 创建或加载 Resources 内的集中音效配置资产。
    private static SoundLibrary GetOrCreateLibrary()
    {
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library != null)
            return library;

        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
        library = ScriptableObject.CreateInstance<SoundLibrary>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        return library;
    }

    // 以选定的首版素材和播放规则填充全部语义音效定义。
    private static void ConfigureLibrary(SoundLibrary library)
    {
        SerializedObject serializedLibrary = new SerializedObject(library);
        Configure(serializedLibrary, "uiConfirm", new[]
        {
            "Assets/Audio/UI/按钮类1－mcx20070509.wav",
            "Assets/Audio/UI/按钮类2－mcx20070509.wav",
            "Assets/Audio/UI/按钮类3－mcx20070509.wav",
        }, 0.38f, 0.48f, 0.98f, 1.03f, 0.04f, 2, false, 1f, 20f);
        Configure(serializedLibrary, "uiOpen", new[] { "Assets/Audio/UI/打开菜单－xh20070511.wav" }, 0.45f, 0.5f, 0.98f, 1.02f, 0.02f, 3, false, 1f, 20f);
        Configure(serializedLibrary, "uiClose", new[] { "Assets/Audio/UI/关闭菜单－xh20070511.wav" }, 0.42f, 0.48f, 0.98f, 1.02f, 0.02f, 3, false, 1f, 20f);
        Configure(serializedLibrary, "uiDialogueAdvance", new[]
        {
            "Assets/Audio/UI/翻页1－xh20070419.wav",
            "Assets/Audio/UI/翻页2－xh20070419.wav",
            "Assets/Audio/UI/翻页3－xh20070419.wav",
        }, 0.34f, 0.42f, 0.97f, 1.04f, 0.06f, 1, false, 1f, 20f);
        Configure(serializedLibrary, "uiPickupRecordPage", new[] { "Assets/Audio/UI/捡起羊皮纸－xh20070419.wav" }, 0.55f, 0.62f, 0.98f, 1.02f, 0f, 1, false, 1f, 20f);
        Configure(serializedLibrary, "uiPickupInscription", new[] { "Assets/Audio/UI/捡起宝石－xh20070419.wav" }, 0.55f, 0.62f, 0.98f, 1.02f, 0f, 1, false, 1f, 20f);
        Configure(serializedLibrary, "uiQuestUpdated", new[] { "Assets/Audio/UI/信息提示1-xys20070413.wav" }, 0.48f, 0.55f, 1f, 1f, 0.1f, 1, false, 1f, 20f);
        Configure(serializedLibrary, "uiQuestCompleted", new[] { "Assets/Audio/UI/胜利完成－xh20070419.wav" }, 0.56f, 0.64f, 1f, 1f, 0f, 1, false, 1f, 20f);

        Configure(serializedLibrary, "playerLightSwing", new[]
        {
            "Assets/Audio/Player/Combat/起音优化/刀剑挥舞1-起音优化.wav",
            "Assets/Audio/Player/Combat/起音优化/刀剑挥舞2-起音优化.wav",
            "Assets/Audio/Player/Combat/起音优化/刀剑挥舞3-起音优化.wav",
        }, 0.38f, 0.48f, 0.96f, 1.05f, 0.05f, 2, false, 1f, 20f);
        Configure(serializedLibrary, "playerHeavySwing", new[]
        {
            "Assets/Audio/Player/Combat/大力挥动-武器1-mcx20070511.wav",
            "Assets/Audio/Player/Combat/大力挥动-武器2-mcx20070511.wav",
        }, 0.48f, 0.58f, 0.94f, 1.02f, 0.08f, 1, false, 1f, 20f);
        Configure(serializedLibrary, "playerRoll", new[] { "Assets/Audio/Player/Combat/跳跃落地-带衣服声-LTT20070511.wav" }, 0.42f, 0.5f, 0.97f, 1.03f, 0f, 3, false, 1f, 20f);
        Configure(serializedLibrary, "playerStoneHit", new[]
        {
            "Assets/Audio/Player/Combat/击中-土石1-ltt20070411.wav",
            "Assets/Audio/Player/Combat/击中-土石2-ltt20070411.wav",
            "Assets/Audio/Player/Combat/击中-土石3-ltt20070411.wav",
        }, 0.52f, 0.62f, 0.95f, 1.04f, 0.05f, 2, false, 1f, 20f);

        Configure(serializedLibrary, "stoneGolemAttackWindup", new[]
        {
            "Assets/Audio/Enemy/StoneGolem/攻击1.wav",
            "Assets/Audio/Enemy/StoneGolem/攻击2.wav",
            "Assets/Audio/Enemy/StoneGolem/攻击3.wav",
            "Assets/Audio/Enemy/StoneGolem/攻击4.wav",
        }, 0.55f, 0.68f, 0.94f, 1.04f, 0.12f, 1, true, 3f, 28f);
        Configure(serializedLibrary, "stoneGolemImpact", new[] { "Assets/Audio/Enemy/StoneGolem/重型脚步声1－xh20070417.wav" }, 0.68f, 0.78f, 0.94f, 1.02f, 0.08f, 1, true, 3f, 32f);
        Configure(serializedLibrary, "stoneGolemHurt", new[]
        {
            "Assets/Audio/Enemy/StoneGolem/受伤1.wav",
            "Assets/Audio/Enemy/StoneGolem/受伤2.wav",
            "Assets/Audio/Enemy/StoneGolem/受伤3.wav",
            "Assets/Audio/Enemy/StoneGolem/受伤4.wav",
        }, 0.52f, 0.65f, 0.95f, 1.04f, 0.12f, 1, true, 2.5f, 25f);
        Configure(serializedLibrary, "stoneGolemDeath", new[] { "Assets/Audio/Enemy/StoneGolem/死亡1.wav" }, 0.7f, 0.8f, 1f, 1f, 0f, 1, true, 3f, 35f);
        Configure(serializedLibrary, "stoneGolemCollapse", new[] { "Assets/Audio/Enemy/StoneGolem/倒塌带滚石声-WQ20070429.wav" }, 0.72f, 0.85f, 0.96f, 1.02f, 0f, 1, true, 3f, 38f);

        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
    }

    // 写入一个定义的片段变体、随机范围、冷却、并发和空间参数。
    private static void Configure(SerializedObject library, string propertyName, IReadOnlyList<string> paths,
        float minVolume, float maxVolume, float minPitch, float maxPitch, float cooldown, int maxSimultaneous,
        bool spatial, float minDistance, float maxDistance)
    {
        SerializedProperty definition = library.FindProperty(propertyName);
        if (definition == null)
            return;

        SerializedProperty clips = definition.FindPropertyRelative("clips");
        clips.arraySize = paths.Count;
        for (int index = 0; index < paths.Count; index++)
            clips.GetArrayElementAtIndex(index).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[index]);

        definition.FindPropertyRelative("minVolume").floatValue = minVolume;
        definition.FindPropertyRelative("maxVolume").floatValue = maxVolume;
        definition.FindPropertyRelative("minPitch").floatValue = minPitch;
        definition.FindPropertyRelative("maxPitch").floatValue = maxPitch;
        definition.FindPropertyRelative("cooldown").floatValue = cooldown;
        definition.FindPropertyRelative("maxSimultaneous").intValue = maxSimultaneous;
        definition.FindPropertyRelative("spatial").boolValue = spatial;
        definition.FindPropertyRelative("minDistance").floatValue = minDistance;
        definition.FindPropertyRelative("maxDistance").floatValue = maxDistance;
    }

    // 为每个可运行场景安装手动挂载的管理器，并为守卫附加事件协调器。
    private static void InstallSceneComponents(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogWarning($"[AudioSetup] Scene was not found: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        AudioManager manager = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            GameObject managerObject = new GameObject("AudioManager");
            manager = managerObject.AddComponent<AudioManager>();
        }

        StoneGolem[] golems = Object.FindObjectsByType<StoneGolem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < golems.Length; index++)
            if (golems[index].GetComponent<StoneGolemAudio>() == null)
                golems[index].gameObject.AddComponent<StoneGolemAudio>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
