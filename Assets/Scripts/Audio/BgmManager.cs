using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景常驻的背景音乐管理器，负责曲目解析、淡入淡出与音量通道应用。
/// 运行时自动自举，无需在场景中手动挂载；若场景中已放置实例，则以场景实例为准。
/// </summary>
[DisallowMultipleComponent]
public sealed class BgmManager : MonoBehaviour
{
    // 默认淡入时长。
    private const float DefaultFadeInSeconds = 1.5f;
    // 换曲与停止时的默认淡出时长。
    private const float DefaultFadeOutSeconds = 1f;
    // 稳定播放阶段跟随音量通道变化的速度，避免以后拖动音量滑条时出现突变。
    private const float VolumeSmoothingPerSecond = 2f;

    // 音乐播放器所处的运行阶段。
    private enum MusicState
    {
        Idle,
        FadingIn,
        FadingOut,
    }

    // 可选的背景音乐配置；留空时从 Resources 自动加载。
    [SerializeField] private BgmLibrary bgmLibrary;

    private AudioSource musicSource;
    private BgmTrack currentTrack;
    private BgmTrack pendingTrack;
    private MusicState state = MusicState.Idle;
    private float targetVolume;
    private float fadeSpeed;
    private float pendingFadeInSeconds = DefaultFadeInSeconds;
    private bool warnedMissingTrack;

    /// <summary>当前常驻实例；尚未初始化时返回 null。</summary>
    public static BgmManager Instance { get; private set; }

    /// <summary>正在播放的音频片段；没有播放时返回 null。</summary>
    public AudioClip CurrentClip =>
        musicSource != null && musicSource.isPlaying ? musicSource.clip : null;

    // 在首个场景加载后自举播放器，保证从任意场景进入都存在唯一的音乐来源。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject host = new GameObject("BgmManager");
        host.AddComponent<BgmManager>();
    }

    // 初始化单例、专用声源与配置，并订阅场景切换和音量变化。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        bgmLibrary = bgmLibrary != null ? bgmLibrary : Resources.Load<BgmLibrary>("Audio/BgmLibrary");

        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        musicSource.volume = 0f;

        AudioVolumeSettings.Changed += OnVolumeSettingsChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayTrackForScene(SceneManager.GetActiveScene().path);
    }

    // 释放事件订阅与单例引用，避免跨场景残留。
    private void OnDestroy()
    {
        AudioVolumeSettings.Changed -= OnVolumeSettingsChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    // 逐帧推进淡入淡出与音量平滑，使用非缩放时间以兼容暂停状态。
    private void Update()
    {
        if (musicSource == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        switch (state)
        {
            case MusicState.FadingIn:
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVolume, fadeSpeed * deltaTime);
                if (Mathf.Approximately(musicSource.volume, targetVolume))
                    state = MusicState.Idle;
                break;

            case MusicState.FadingOut:
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, fadeSpeed * deltaTime);
                if (Mathf.Approximately(musicSource.volume, 0f))
                    CompleteFadeOut();
                break;

            default:
                musicSource.volume =
                    Mathf.MoveTowards(musicSource.volume, targetVolume, VolumeSmoothingPerSecond * deltaTime);
                break;
        }
    }

    /// <summary>播放指定曲目；与当前曲目相同则保持连续播放，不重新开始。</summary>
    public void PlayTrack(BgmTrack track, float fadeSeconds = DefaultFadeInSeconds)
    {
        if (track == null || !track.IsValid)
        {
            WarnMissingTrackOnce();
            return;
        }

        if (musicSource != null && musicSource.isPlaying && currentTrack != null &&
            currentTrack.Clip == track.Clip)
        {
            currentTrack = track;
            RefreshTargetVolume();
            // 若此刻正在为换曲淡出，取消换曲并重新淡回当前曲目。
            pendingTrack = null;
            if (state == MusicState.FadingOut)
            {
                fadeSpeed = Mathf.Max(targetVolume - musicSource.volume, 0.0001f) / DefaultFadeInSeconds;
                state = MusicState.FadingIn;
            }
            return;
        }

        float fadeOutSeconds = musicSource != null && musicSource.isPlaying ? DefaultFadeOutSeconds : 0f;
        if (fadeOutSeconds > 0f)
        {
            pendingTrack = track;
            pendingFadeInSeconds = Mathf.Max(0f, fadeSeconds);
            BeginFadeOut(fadeOutSeconds);
            return;
        }

        StartTrack(track, fadeSeconds);
    }

    /// <summary>淡出并停止当前背景音乐。</summary>
    public void Stop(float fadeSeconds = DefaultFadeOutSeconds)
    {
        pendingTrack = null;
        if (musicSource == null || !musicSource.isPlaying)
            return;

        if (fadeSeconds > 0f)
        {
            BeginFadeOut(fadeSeconds);
            return;
        }

        StopImmediately();
    }

    // 解析场景对应曲目并播放，同一曲目跨场景时保持连续播放。
    private void PlayTrackForScene(string scenePath)
    {
        if (bgmLibrary == null)
        {
            WarnMissingTrackOnce();
            return;
        }

        if (bgmLibrary.TryResolveTrack(scenePath, out BgmTrack track))
            PlayTrack(track);
        else
            WarnMissingTrackOnce();
    }

    // 场景加载完成后切换到该场景所需的曲目。
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayTrackForScene(scene.path);
    }

    // 音量通道变化时刷新目标音量，当前正处于淡入淡出的曲目会跟随新目标。
    private void OnVolumeSettingsChanged()
    {
        RefreshTargetVolume();
    }

    // 立即切换片段并从零音量淡入。
    private void StartTrack(BgmTrack track, float fadeSeconds)
    {
        currentTrack = track;
        pendingTrack = null;
        musicSource.clip = track.Clip;
        musicSource.loop = track.Loop;
        RefreshTargetVolume();

        if (fadeSeconds > 0f && targetVolume > 0f)
        {
            musicSource.volume = 0f;
            musicSource.Play();
            fadeSpeed = targetVolume / fadeSeconds;
            state = MusicState.FadingIn;
            return;
        }

        musicSource.volume = targetVolume;
        musicSource.Play();
        state = MusicState.Idle;
    }

    // 按给定时长把当前音量线性推到零。
    private void BeginFadeOut(float fadeSeconds)
    {
        fadeSpeed = Mathf.Max(musicSource.volume, 0.0001f) / Mathf.Max(0.01f, fadeSeconds);
        state = MusicState.FadingOut;
    }

    // 淡出结束后切换到待播曲目，没有待播曲目时彻底停止播放。
    private void CompleteFadeOut()
    {
        if (pendingTrack != null)
        {
            BgmTrack next = pendingTrack;
            StartTrack(next, pendingFadeInSeconds);
            return;
        }

        StopImmediately();
    }

    // 清空当前片段并回到静音状态。
    private void StopImmediately()
    {
        musicSource.Stop();
        musicSource.clip = null;
        musicSource.volume = 0f;
        currentTrack = null;
        pendingTrack = null;
        state = MusicState.Idle;
    }

    // 按曲目基础音量与音乐通道音量计算最终目标音量。
    private void RefreshTargetVolume()
    {
        targetVolume = currentTrack != null
            ? Mathf.Clamp01(currentTrack.Volume) * AudioVolumeSettings.BgmVolume
            : 0f;
    }

    // 配置缺失时只记录一次警告，避免每次切场景刷屏。
    private void WarnMissingTrackOnce()
    {
        if (warnedMissingTrack)
            return;

        warnedMissingTrack = true;
        Debug.LogWarning("[BgmManager] 未找到可播放的背景音乐配置，请检查 Resources/Audio/BgmLibrary。", this);
    }
}
