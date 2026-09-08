using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证音效定义对缺失素材、随机片段与高频播放限制的基础保护行为。
/// </summary>
public sealed class SoundLibraryEditModeTests
{
    /// <summary>未配置片段时选择操作应安全失败。</summary>
    [Test]
    public void TryGetRandomClip_WithNoClips_ReturnsFalse()
    {
        SoundDefinition definition = new SoundDefinition();

        Assert.That(definition.TryGetRandomClip(out AudioClip clip), Is.False);
        Assert.That(clip, Is.Null);
    }

    /// <summary>含有空引用时仍应只返回有效音频片段。</summary>
    [Test]
    public void TryGetRandomClip_WithNullEntries_ReturnsValidClip()
    {
        SoundLibrary library = ScriptableObject.CreateInstance<SoundLibrary>();
        AudioClip expected = AudioClip.Create("TestClip", 64, 1, 44100, false);
        SerializedObject serializedLibrary = new SerializedObject(library);
        SerializedProperty clips = serializedLibrary.FindProperty("uiConfirm").FindPropertyRelative("clips");
        clips.arraySize = 2;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = null;
        clips.GetArrayElementAtIndex(1).objectReferenceValue = expected;
        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(library.UiConfirm.TryGetRandomClip(out AudioClip actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));

        Object.DestroyImmediate(expected);
        Object.DestroyImmediate(library);
    }

    /// <summary>冷却窗口内和超出最大并发时必须拒绝新的播放请求。</summary>
    [Test]
    public void CanPlay_RespectsCooldownAndConcurrency()
    {
        SoundLibrary library = ScriptableObject.CreateInstance<SoundLibrary>();
        SerializedObject serializedLibrary = new SerializedObject(library);
        SerializedProperty definition = serializedLibrary.FindProperty("uiConfirm");
        definition.FindPropertyRelative("cooldown").floatValue = 0.2f;
        definition.FindPropertyRelative("maxSimultaneous").intValue = 2;
        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(library.UiConfirm.CanPlay(1f, 0.9f, 0), Is.False);
        Assert.That(library.UiConfirm.CanPlay(1.2f, 0.9f, 2), Is.False);
        Assert.That(library.UiConfirm.CanPlay(1.2f, 0.9f, 1), Is.True);

        Object.DestroyImmediate(library);
    }

    /// <summary>缺少配置资产时的管理器语义调用不应抛出异常。</summary>
    [Test]
    public void AudioManager_WithMissingLibrary_DoesNotThrow()
    {
        GameObject managerObject = new GameObject("AudioManagerTest");
        AudioManager manager = managerObject.AddComponent<AudioManager>();

        Assert.DoesNotThrow(manager.PlayUiConfirm);

        Object.DestroyImmediate(managerObject);
    }
}
