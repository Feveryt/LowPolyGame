using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跨场景复用的首版音效管理器，统一从 SoundLibrary 读取规则并负责冷却、并发限制和三维声源回收。
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private sealed class ActivePlayback
    {
        public SoundDefinition Definition;
        public AudioSource Source;
        public float ReleaseAt;
    }

    [SerializeField] private SoundLibrary soundLibrary;
    private AudioSource twoDimensionalSource;
    private readonly Dictionary<SoundDefinition, float> lastPlayedTimes = new Dictionary<SoundDefinition, float>();
    private readonly List<ActivePlayback> activePlaybacks = new List<ActivePlayback>();
    private readonly Stack<AudioSource> spatialSourcePool = new Stack<AudioSource>();
    private QuestService questService;

    /// <summary>当前全局音效管理器；场景未挂载时返回 null。</summary>
    public static AudioManager Instance { get; private set; }

    // 保留首个场景实例并初始化固定二维声源。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        soundLibrary = soundLibrary != null ? soundLibrary : Resources.Load<SoundLibrary>("Audio/SoundLibrary");
        twoDimensionalSource = GetComponent<AudioSource>();
        if (twoDimensionalSource == null)
            twoDimensionalSource = gameObject.AddComponent<AudioSource>();
        twoDimensionalSource.playOnAwake = false;
        twoDimensionalSource.spatialBlend = 0f;
    }

    // 绑定任务领域事件，使任务提示不依赖 UI 刷新时机。
    private void OnEnable()
    {
        if (Instance != this)
            return;

        questService = QuestService.Instance;
        questService.ObjectiveCompleted += OnQuestObjectiveCompleted;
        questService.QuestCompleted += OnQuestCompleted;
    }

    // 清理跨场景事件订阅。
    private void OnDisable()
    {
        if (questService == null)
            return;

        questService.ObjectiveCompleted -= OnQuestObjectiveCompleted;
        questService.QuestCompleted -= OnQuestCompleted;
        questService = null;
    }

    // 回收已播放完成的临时三维声源，并释放对应的并发计数。
    private void Update()
    {
        float now = Time.unscaledTime;
        for (int index = activePlaybacks.Count - 1; index >= 0; index--)
        {
            ActivePlayback playback = activePlaybacks[index];
            if (now < playback.ReleaseAt)
                continue;

            activePlaybacks.RemoveAt(index);
            if (playback.Source == null || playback.Source == twoDimensionalSource)
                continue;

            playback.Source.Stop();
            playback.Source.clip = null;
            playback.Source.gameObject.SetActive(false);
            spatialSourcePool.Push(playback.Source);
        }
    }

    // 释放单例引用，避免编辑器域重载后持有已销毁对象。
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>播放通用 UI 确认声。</summary>
    public void PlayUiConfirm() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiConfirm : null);
    /// <summary>播放 UI 打开声。</summary>
    public void PlayUiOpen() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiOpen : null);
    /// <summary>播放 UI 关闭声。</summary>
    public void PlayUiClose() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiClose : null);
    /// <summary>播放对话推进声。</summary>
    public void PlayUiDialogueAdvance() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiDialogueAdvance : null);
    /// <summary>播放记录页拾取声。</summary>
    public void PlayUiPickupRecordPage() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiPickupRecordPage : null);
    /// <summary>播放封印铭片拾取声。</summary>
    public void PlayUiPickupInscription() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiPickupInscription : null);
    /// <summary>播放任务目标推进声。</summary>
    public void PlayUiQuestUpdated() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiQuestUpdated : null);
    /// <summary>播放任务提交完成声。</summary>
    public void PlayUiQuestCompleted() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.UiQuestCompleted : null);
    /// <summary>播放玩家轻攻击挥刀声。</summary>
    public void PlayPlayerLightSwing() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.PlayerLightSwing : null);
    /// <summary>播放玩家重攻击挥刀声。</summary>
    public void PlayPlayerHeavySwing() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.PlayerHeavySwing : null);
    /// <summary>播放玩家成功翻滚声。</summary>
    public void PlayPlayerRoll() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.PlayerRoll : null);
    /// <summary>播放玩家命中石材目标的反馈声。</summary>
    public void PlayPlayerStoneHit() => PlayTwoDimensional(soundLibrary != null ? soundLibrary.PlayerStoneHit : null);
    /// <summary>在守卫位置播放攻击起手预警声。</summary>
    public void PlayStoneGolemAttackWindup(Vector3 position) => PlaySpatial(soundLibrary != null ? soundLibrary.StoneGolemAttackWindup : null, position);
    /// <summary>在守卫位置播放攻击落点声。</summary>
    public void PlayStoneGolemImpact(Vector3 position) => PlaySpatial(soundLibrary != null ? soundLibrary.StoneGolemImpact : null, position);
    /// <summary>在守卫位置播放受击裂石声。</summary>
    public void PlayStoneGolemHurt(Vector3 position) => PlaySpatial(soundLibrary != null ? soundLibrary.StoneGolemHurt : null, position);
    /// <summary>在守卫位置播放死亡主体声。</summary>
    public void PlayStoneGolemDeath(Vector3 position) => PlaySpatial(soundLibrary != null ? soundLibrary.StoneGolemDeath : null, position);
    /// <summary>在守卫位置播放死亡坍塌尾音。</summary>
    public void PlayStoneGolemCollapse(Vector3 position) => PlaySpatial(soundLibrary != null ? soundLibrary.StoneGolemCollapse : null, position);

    // 响应明确的任务目标完成事件。
    private void OnQuestObjectiveCompleted(string questId, string objectiveId) => PlayUiQuestUpdated();
    // 响应明确的任务提交完成事件。
    private void OnQuestCompleted(string questId) => PlayUiQuestCompleted();

    // 以固定二维声源播放即时反馈，并记录其估算生命周期。
    private void PlayTwoDimensional(SoundDefinition definition)
    {
        if (!TryPreparePlayback(definition, out AudioClip clip, out float volume, out float pitch))
            return;

        twoDimensionalSource.pitch = pitch;
        twoDimensionalSource.PlayOneShot(clip, volume);
        TrackPlayback(definition, twoDimensionalSource, clip, pitch);
    }

    // 从池中租用临时声源，在世界位置播放带距离衰减的效果声。
    private void PlaySpatial(SoundDefinition definition, Vector3 position)
    {
        if (!TryPreparePlayback(definition, out AudioClip clip, out float volume, out float pitch))
            return;

        AudioSource source = RentSpatialSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = definition.MinDistance;
        source.maxDistance = definition.MaxDistance;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = false;
        source.clip = clip;
        source.Play();
        TrackPlayback(definition, source, clip, pitch);
    }

    // 检查片段、冷却和并发限制，并取得本次随机播放参数。
    private bool TryPreparePlayback(SoundDefinition definition, out AudioClip clip, out float volume, out float pitch)
    {
        clip = null;
        volume = 0f;
        pitch = 1f;
        if (definition == null || !definition.TryGetRandomClip(out clip))
            return false;

        float now = Time.unscaledTime;
        float lastPlayed = lastPlayedTimes.TryGetValue(definition, out float value) ? value : float.NegativeInfinity;
        if (!definition.CanPlay(now, lastPlayed, GetActiveCount(definition)))
            return false;

        lastPlayedTimes[definition] = now;
        definition.GetPlaybackSettings(out volume, out pitch);
        return true;
    }

    // 记录音效的预计结束时间，用于并发计算与声源复用。
    private void TrackPlayback(SoundDefinition definition, AudioSource source, AudioClip clip, float pitch)
    {
        activePlaybacks.Add(new ActivePlayback
        {
            Definition = definition,
            Source = source,
            ReleaseAt = Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)),
        });
    }

    // 获取同类音效当前的活跃播放数量。
    private int GetActiveCount(SoundDefinition definition)
    {
        int count = 0;
        for (int index = 0; index < activePlaybacks.Count; index++)
            if (activePlaybacks[index].Definition == definition)
                count++;
        return count;
    }

    // 获取或创建可回收的三维 AudioSource。
    private AudioSource RentSpatialSource()
    {
        if (spatialSourcePool.Count > 0)
        {
            AudioSource pooledSource = spatialSourcePool.Pop();
            pooledSource.gameObject.SetActive(true);
            return pooledSource;
        }

        GameObject sourceObject = new GameObject("Spatial Sfx");
        sourceObject.transform.SetParent(transform);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }
}
