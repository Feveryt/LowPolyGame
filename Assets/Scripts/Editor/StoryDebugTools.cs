using UnityEditor;
using UnityEngine;

/// <summary>提供剧情存档清除与快速重开 Play Mode 的中文编辑器菜单。</summary>
public static class StoryDebugTools
{
    private const string ClearMenuPath = "工具/Low Poly Game/剧情/清除剧情存档";
    private const string RestartMenuPath = "工具/Low Poly Game/剧情/重置剧情并重新开始";
    private const string RestartPendingKey = "LowPolyGame.StoryDebug.RestartPending";

    // 确保退出 Play Mode 后仍能继续完成清档和重新播放。
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary>清除任务进度与开场剧情记录，供重复测试剧情使用。</summary>
    [MenuItem(ClearMenuPath)]
    private static void ClearStoryProgress()
    {
        if (!EditorUtility.DisplayDialog(
                "清除剧情存档",
                "将清除两条任务进度和开场剧情记录，是否继续？",
                "清除",
                "取消"))
        {
            return;
        }

        ClearStoryKeys();
        Debug.Log("[StoryDebugTools] 剧情存档已清除。");
    }

    /// <summary>退出当前播放，清除剧情存档后重新进入 Play Mode。</summary>
    [MenuItem(RestartMenuPath)]
    private static void ResetStoryAndRestart()
    {
        if (!EditorUtility.DisplayDialog(
                "重置剧情并重新开始",
                "将清除剧情存档并重新进入 Play Mode，是否继续？",
                "重置并重开",
                "取消"))
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            SessionState.SetBool(RestartPendingKey, true);
            EditorApplication.isPlaying = false;
            return;
        }

        ClearStoryKeys();
        EditorApplication.isPlaying = true;
    }

    // 等待编辑器完全退出播放后清档，避免保存逻辑把旧进度重新写回。
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode ||
            !SessionState.GetBool(RestartPendingKey, false))
        {
            return;
        }

        SessionState.SetBool(RestartPendingKey, false);
        ClearStoryKeys();
        Debug.Log("[StoryDebugTools] 剧情存档已清除，正在重新进入 Play Mode。");
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    // 同步清除运行时缓存与持久化键，避免任务面板继续显示旧进度。
    private static void ClearStoryKeys()
    {
        if (Application.isPlaying)
            QuestService.ClearProgress();

        PlayerPrefs.DeleteKey(StorySaveKeys.QuestProgress);
        PlayerPrefs.DeleteKey(StorySaveKeys.OpeningSeen);
        PlayerPrefs.Save();
    }
}
