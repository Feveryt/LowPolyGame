using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证音量通道的钳制与持久化，以及背景音乐按场景解析曲目的规则。
/// </summary>
public sealed class AudioVolumeSettingsEditModeTests
{
    private bool hadSfxKey;
    private bool hadBgmKey;
    private float originalSfxVolume;
    private float originalBgmVolume;

    // 记录本机原有音量设置并把测试起点归一，避免相互影响。
    [SetUp]
    public void SetUp()
    {
        hadSfxKey = PlayerPrefs.HasKey(AudioVolumeSettings.SfxVolumeKey);
        hadBgmKey = PlayerPrefs.HasKey(AudioVolumeSettings.BgmVolumeKey);
        originalSfxVolume = PlayerPrefs.GetFloat(AudioVolumeSettings.SfxVolumeKey, AudioVolumeSettings.DefaultSfxVolume);
        originalBgmVolume = PlayerPrefs.GetFloat(AudioVolumeSettings.BgmVolumeKey, AudioVolumeSettings.DefaultBgmVolume);

        AudioVolumeSettings.ResetToDefaults();
    }

    // 还原测试前的持久化数值与内存缓存，不污染编辑器会话。
    [TearDown]
    public void TearDown()
    {
        RestoreKey(AudioVolumeSettings.SfxVolumeKey, hadSfxKey, originalSfxVolume);
        RestoreKey(AudioVolumeSettings.BgmVolumeKey, hadBgmKey, originalBgmVolume);
        AudioVolumeSettings.Reload();
    }

    /// <summary>超出范围的音量必须被钳制到 0 到 1。</summary>
    [Test]
    public void SetSfxVolume_ClampsToValidRange()
    {
        AudioVolumeSettings.SetSfxVolume(-3f);
        Assert.That(AudioVolumeSettings.SfxVolume, Is.EqualTo(0f));
        Assert.That(PlayerPrefs.GetFloat(AudioVolumeSettings.SfxVolumeKey), Is.EqualTo(0f));

        AudioVolumeSettings.SetSfxVolume(4f);
        Assert.That(AudioVolumeSettings.SfxVolume, Is.EqualTo(1f));
        Assert.That(PlayerPrefs.GetFloat(AudioVolumeSettings.SfxVolumeKey), Is.EqualTo(1f));
    }

    /// <summary>音乐通道音量必须写入 PlayerPrefs，供下次启动恢复。</summary>
    [Test]
    public void SetBgmVolume_PersistsToPlayerPrefs()
    {
        AudioVolumeSettings.SetBgmVolume(0.35f);

        Assert.That(AudioVolumeSettings.BgmVolume, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(PlayerPrefs.GetFloat(AudioVolumeSettings.BgmVolumeKey), Is.EqualTo(0.35f).Within(0.0001f));
    }

    /// <summary>音量真正变化时触发事件，重复设置相同数值不再触发。</summary>
    [Test]
    public void SetVolume_RaisesChangedEventOnlyOnChange()
    {
        int changeCount = 0;
        System.Action handler = () => changeCount++;
        AudioVolumeSettings.Changed += handler;

        try
        {
            AudioVolumeSettings.SetBgmVolume(1f);
            int baseline = changeCount;

            AudioVolumeSettings.SetBgmVolume(0.42f);
            Assert.That(changeCount, Is.EqualTo(baseline + 1));

            AudioVolumeSettings.SetBgmVolume(0.42f);
            Assert.That(changeCount, Is.EqualTo(baseline + 1), "重复设置相同数值不应再次触发事件");

            AudioVolumeSettings.SetSfxVolume(0.2f);
            Assert.That(changeCount, Is.EqualTo(baseline + 2), "音效通道变化也应触发同一事件");
        }
        finally
        {
            AudioVolumeSettings.Changed -= handler;
        }
    }

    /// <summary>恢复默认值时应清除两条通道的持久化键。</summary>
    [Test]
    public void ResetToDefaults_ClearsPersistedKeys()
    {
        AudioVolumeSettings.SetSfxVolume(0.13f);
        AudioVolumeSettings.SetBgmVolume(0.13f);
        Assert.That(PlayerPrefs.HasKey(AudioVolumeSettings.SfxVolumeKey), Is.True);
        Assert.That(PlayerPrefs.HasKey(AudioVolumeSettings.BgmVolumeKey), Is.True);

        AudioVolumeSettings.ResetToDefaults();

        Assert.That(PlayerPrefs.HasKey(AudioVolumeSettings.SfxVolumeKey), Is.False);
        Assert.That(PlayerPrefs.HasKey(AudioVolumeSettings.BgmVolumeKey), Is.False);
        Assert.That(AudioVolumeSettings.SfxVolume, Is.EqualTo(AudioVolumeSettings.DefaultSfxVolume));
        Assert.That(AudioVolumeSettings.BgmVolume, Is.EqualTo(AudioVolumeSettings.DefaultBgmVolume));
    }

    /// <summary>配置了场景覆盖时必须优先使用覆盖曲目。</summary>
    [Test]
    public void TryResolveTrack_WithSceneOverride_PrefersOverride()
    {
        AudioClip defaultClip = AudioClip.Create("DefaultClip", 64, 1, 44100, false);
        AudioClip overrideClip = AudioClip.Create("OverrideClip", 64, 1, 44100, false);
        BgmLibrary library = CreateLibraryWithDefaultClip(defaultClip);

        SerializedObject serialized = new SerializedObject(library);
        SerializedProperty sceneTracks = serialized.FindProperty("sceneTracks");
        sceneTracks.arraySize = 1;
        SerializedProperty entry = sceneTracks.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("scenePath").stringValue = "Assets/Scenes/GameScene/Demo 1.unity";
        entry.FindPropertyRelative("track").FindPropertyRelative("clip").objectReferenceValue = overrideClip;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(library.TryResolveTrack("Assets/Scenes/GameScene/Demo 1.unity", out BgmTrack track), Is.True);
        Assert.That(track.Clip, Is.EqualTo(overrideClip));

        Object.DestroyImmediate(defaultClip);
        Object.DestroyImmediate(overrideClip);
        Object.DestroyImmediate(library);
    }

    /// <summary>场景没有覆盖配置时回退到默认曲目。</summary>
    [Test]
    public void TryResolveTrack_WithoutOverride_FallsBackToDefault()
    {
        AudioClip defaultClip = AudioClip.Create("DefaultClip", 64, 1, 44100, false);
        BgmLibrary library = CreateLibraryWithDefaultClip(defaultClip);

        Assert.That(library.TryResolveTrack("Assets/Scenes/GameScene/TestAct.unity", out BgmTrack track), Is.True);
        Assert.That(track.Clip, Is.EqualTo(defaultClip));

        Object.DestroyImmediate(defaultClip);
        Object.DestroyImmediate(library);
    }

    /// <summary>只写场景文件名时也应匹配完整路径。</summary>
    [Test]
    public void TryResolveTrack_WithSceneNameOnly_MatchesFullPath()
    {
        AudioClip defaultClip = AudioClip.Create("DefaultClip", 64, 1, 44100, false);
        AudioClip overrideClip = AudioClip.Create("OverrideClip", 64, 1, 44100, false);
        BgmLibrary library = CreateLibraryWithDefaultClip(defaultClip);

        SerializedObject serialized = new SerializedObject(library);
        SerializedProperty sceneTracks = serialized.FindProperty("sceneTracks");
        sceneTracks.arraySize = 1;
        SerializedProperty entry = sceneTracks.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("scenePath").stringValue = "Demo 1";
        entry.FindPropertyRelative("track").FindPropertyRelative("clip").objectReferenceValue = overrideClip;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(library.TryResolveTrack("Assets/Scenes/GameScene/Demo 1.unity", out BgmTrack track), Is.True);
        Assert.That(track.Clip, Is.EqualTo(overrideClip));

        Object.DestroyImmediate(defaultClip);
        Object.DestroyImmediate(overrideClip);
        Object.DestroyImmediate(library);
    }

    /// <summary>片段缺失时必须返回 false，避免播放空音乐。</summary>
    [Test]
    public void TryResolveTrack_WithMissingClip_ReturnsFalse()
    {
        BgmLibrary library = ScriptableObject.CreateInstance<BgmLibrary>();

        Assert.That(library.TryResolveTrack("Assets/Scenes/GameScene/TestAct.unity", out BgmTrack track), Is.False);
        Assert.That(track.IsValid, Is.False);

        Object.DestroyImmediate(library);
    }

    // 创建只配置默认曲目的音乐库，供解析规则测试复用。
    private static BgmLibrary CreateLibraryWithDefaultClip(AudioClip clip)
    {
        BgmLibrary library = ScriptableObject.CreateInstance<BgmLibrary>();
        SerializedObject serialized = new SerializedObject(library);
        serialized.FindProperty("defaultTrack").FindPropertyRelative("clip").objectReferenceValue = clip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return library;
    }

    // 按原始存在状态还原单个音量键，原本不存在时保持删除状态。
    private static void RestoreKey(string key, bool hadKey, float value)
    {
        if (hadKey)
            PlayerPrefs.SetFloat(key, value);
        else
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.Save();
    }
}
