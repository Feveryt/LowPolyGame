using UnityEngine;

/// <summary>
/// 单类音效的素材变体与播放规则，供 AudioManager 统一执行。
/// </summary>
[System.Serializable]
public sealed class SoundDefinition
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float minVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;
    [SerializeField, Min(0.01f)] private float minPitch = 1f;
    [SerializeField, Min(0.01f)] private float maxPitch = 1f;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField, Min(1)] private int maxSimultaneous = 1;
    [SerializeField] private bool spatial;
    [SerializeField, Min(0.01f)] private float minDistance = 1f;
    [SerializeField, Min(0.01f)] private float maxDistance = 20f;

    /// <summary>相邻两次播放之间的最短间隔。</summary>
    public float Cooldown => cooldown;
    /// <summary>同一音效允许同时存在的最大播放数。</summary>
    public int MaxSimultaneous => maxSimultaneous;
    /// <summary>是否按三维距离衰减播放。</summary>
    public bool IsSpatial => spatial;
    /// <summary>三维声音保持完整音量的距离。</summary>
    public float MinDistance => minDistance;
    /// <summary>三维声音衰减至静音的最远距离。</summary>
    public float MaxDistance => maxDistance;

    /// <summary>随机返回一个有效片段；未配置有效片段时返回 false。</summary>
    public bool TryGetRandomClip(out AudioClip clip)
    {
        clip = null;
        if (clips == null || clips.Length == 0)
            return false;

        int validCount = 0;
        for (int index = 0; index < clips.Length; index++)
            if (clips[index] != null)
                validCount++;

        if (validCount == 0)
            return false;

        int selected = Random.Range(0, validCount);
        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] == null)
                continue;
            if (selected-- == 0)
            {
                clip = clips[index];
                return true;
            }
        }
        return false;
    }

    /// <summary>随机计算本次播放所需的音量与音高。</summary>
    public void GetPlaybackSettings(out float volume, out float pitch)
    {
        volume = Random.Range(Mathf.Min(minVolume, maxVolume), Mathf.Max(minVolume, maxVolume));
        pitch = Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
    }

    /// <summary>按给定运行时状态判断该音效是否允许再次播放。</summary>
    public bool CanPlay(float currentTime, float lastPlayedTime, int activeCount)
    {
        return activeCount < maxSimultaneous && currentTime >= lastPlayedTime + cooldown;
    }

    // 修正 Inspector 可能输入的无效范围。
    private void OnValidate()
    {
        minVolume = Mathf.Clamp01(minVolume);
        maxVolume = Mathf.Clamp01(maxVolume);
        minPitch = Mathf.Max(0.01f, minPitch);
        maxPitch = Mathf.Max(0.01f, maxPitch);
        cooldown = Mathf.Max(0f, cooldown);
        maxSimultaneous = Mathf.Max(1, maxSimultaneous);
        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
    }
}

/// <summary>
/// 首版游戏音效的集中配置资产；业务代码仅通过 AudioManager 的语义化方法访问这些定义。
/// </summary>
[CreateAssetMenu(menuName = "Low Poly Game/Audio/Sound Library", fileName = "SoundLibrary")]
public sealed class SoundLibrary : ScriptableObject
{
    [Header("UI")]
    [SerializeField] private SoundDefinition uiConfirm = new SoundDefinition();
    [SerializeField] private SoundDefinition uiOpen = new SoundDefinition();
    [SerializeField] private SoundDefinition uiClose = new SoundDefinition();
    [SerializeField] private SoundDefinition uiDialogueAdvance = new SoundDefinition();
    [SerializeField] private SoundDefinition uiPickupRecordPage = new SoundDefinition();
    [SerializeField] private SoundDefinition uiPickupInscription = new SoundDefinition();
    [SerializeField] private SoundDefinition uiQuestUpdated = new SoundDefinition();
    [SerializeField] private SoundDefinition uiQuestCompleted = new SoundDefinition();
    [Header("Player Combat")]
    [SerializeField] private SoundDefinition playerLightSwing = new SoundDefinition();
    [SerializeField] private SoundDefinition playerHeavySwing = new SoundDefinition();
    [SerializeField] private SoundDefinition playerRoll = new SoundDefinition();
    [SerializeField] private SoundDefinition playerStoneHit = new SoundDefinition();
    [Header("Stone Golem")]
    [SerializeField] private SoundDefinition stoneGolemAttackWindup = new SoundDefinition();
    [SerializeField] private SoundDefinition stoneGolemImpact = new SoundDefinition();
    [SerializeField] private SoundDefinition stoneGolemHurt = new SoundDefinition();
    [SerializeField] private SoundDefinition stoneGolemDeath = new SoundDefinition();
    [SerializeField] private SoundDefinition stoneGolemCollapse = new SoundDefinition();

    /// <summary>UI 确认操作音效。</summary>
    public SoundDefinition UiConfirm => uiConfirm;
    /// <summary>UI 打开音效。</summary>
    public SoundDefinition UiOpen => uiOpen;
    /// <summary>UI 关闭音效。</summary>
    public SoundDefinition UiClose => uiClose;
    /// <summary>对话普通推进音效。</summary>
    public SoundDefinition UiDialogueAdvance => uiDialogueAdvance;
    /// <summary>记录页拾取音效。</summary>
    public SoundDefinition UiPickupRecordPage => uiPickupRecordPage;
    /// <summary>封印铭片拾取音效。</summary>
    public SoundDefinition UiPickupInscription => uiPickupInscription;
    /// <summary>任务目标推进音效。</summary>
    public SoundDefinition UiQuestUpdated => uiQuestUpdated;
    /// <summary>任务提交完成音效。</summary>
    public SoundDefinition UiQuestCompleted => uiQuestCompleted;
    /// <summary>玩家轻攻击挥空音效。</summary>
    public SoundDefinition PlayerLightSwing => playerLightSwing;
    /// <summary>玩家重攻击挥空音效。</summary>
    public SoundDefinition PlayerHeavySwing => playerHeavySwing;
    /// <summary>玩家成功翻滚音效。</summary>
    public SoundDefinition PlayerRoll => playerRoll;
    /// <summary>玩家攻击石材目标成功的命中音效。</summary>
    public SoundDefinition PlayerStoneHit => playerStoneHit;
    /// <summary>石头守卫攻击起手预警音效。</summary>
    public SoundDefinition StoneGolemAttackWindup => stoneGolemAttackWindup;
    /// <summary>石头守卫攻击落点音效。</summary>
    public SoundDefinition StoneGolemImpact => stoneGolemImpact;
    /// <summary>石头守卫非致命受击音效。</summary>
    public SoundDefinition StoneGolemHurt => stoneGolemHurt;
    /// <summary>石头守卫死亡主体音效。</summary>
    public SoundDefinition StoneGolemDeath => stoneGolemDeath;
    /// <summary>石头守卫死亡后的坍塌尾音。</summary>
    public SoundDefinition StoneGolemCollapse => stoneGolemCollapse;
}
