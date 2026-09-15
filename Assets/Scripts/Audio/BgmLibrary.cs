using System;
using UnityEngine;

/// <summary>
/// 单首背景音乐的播放配置，供 BgmManager 读取。
/// </summary>
[Serializable]
public sealed class BgmTrack
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField] private bool loop = true;

    /// <summary>曲目使用的音频片段。</summary>
    public AudioClip Clip => clip;
    /// <summary>曲目在混音中的基础音量，最终音量还会乘以背景音乐通道音量。</summary>
    public float Volume => volume;
    /// <summary>是否循环播放。</summary>
    public bool Loop => loop;
    /// <summary>片段是否已经配置，可用于播放。</summary>
    public bool IsValid => clip != null;
}

/// <summary>
/// 背景音乐集中配置，按场景路径解析曲目，未匹配的场景回退到默认曲目。
/// </summary>
[CreateAssetMenu(menuName = "Low Poly Game/Audio/BGM Library", fileName = "BgmLibrary")]
public sealed class BgmLibrary : ScriptableObject
{
    /// <summary>针对单个场景覆盖的曲目配置。</summary>
    [Serializable]
    public sealed class SceneTrack
    {
        [SerializeField] private string scenePath;
        [SerializeField] private BgmTrack track = new BgmTrack();

        /// <summary>目标场景路径，可写完整路径或仅写场景文件名。</summary>
        public string ScenePath => scenePath;
        /// <summary>该场景使用的曲目。</summary>
        public BgmTrack Track => track;
    }

    [SerializeField] private BgmTrack defaultTrack = new BgmTrack();
    [SerializeField] private SceneTrack[] sceneTracks = Array.Empty<SceneTrack>();

    /// <summary>未匹配到场景覆盖时使用的默认曲目。</summary>
    public BgmTrack DefaultTrack => defaultTrack;

    /// <summary>按场景路径解析曲目：优先场景覆盖，其次默认曲目；没有可用片段时返回 false。</summary>
    public bool TryResolveTrack(string scenePath, out BgmTrack track)
    {
        if (!string.IsNullOrWhiteSpace(scenePath) && sceneTracks != null)
        {
            for (int index = 0; index < sceneTracks.Length; index++)
            {
                SceneTrack entry = sceneTracks[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ScenePath))
                    continue;
                if (PathsMatch(entry.ScenePath, scenePath) && entry.Track != null && entry.Track.IsValid)
                {
                    track = entry.Track;
                    return true;
                }
            }
        }

        track = defaultTrack;
        return track != null && track.IsValid;
    }

    // 兼容完整路径与仅写场景名两种配置方式，并忽略路径分隔符差异。
    private static bool PathsMatch(string configuredPath, string actualPath)
    {
        string configured = NormalizePath(configuredPath);
        string actual = NormalizePath(actualPath);
        if (string.Equals(configured, actual, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(GetSceneName(configured), GetSceneName(actual), StringComparison.OrdinalIgnoreCase);
    }

    // 统一路径中的分隔符并去掉首尾空白。
    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
    }

    // 取出不带 .unity 后缀的场景文件名，用于宽松匹配。
    private static string GetSceneName(string path)
    {
        int separatorIndex = path.LastIndexOf('/');
        string name = separatorIndex >= 0 ? path.Substring(separatorIndex + 1) : path;
        return name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            ? name.Substring(0, name.Length - ".unity".Length)
            : name;
    }
}
