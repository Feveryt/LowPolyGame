using UnityEngine;

/// <summary>集中保存《未署名的守卫》场景中 NPC、记录页与石头人的可视化引用。</summary>
public sealed class UnsignedGuardianBindings : MonoBehaviour
{
    [SerializeField] private GameObject recordPage;
    [SerializeField] private GameObject sealingInscription;
    [SerializeField] private GameObject halfBurntArchive;
    [SerializeField] private QuestWorldReporter recordPageReporter;
    [SerializeField] private QuestWorldReporter golemReporter;
    [SerializeField] private QuestWorldReporter dragonReporter;

    // 根据任务状态显示当前阶段允许交互的世界物件。
    private void OnEnable()
    {
        QuestService.Instance.QuestChanged += Refresh;
        Refresh(string.Empty);
    }

    // 解除任务状态刷新订阅。
    private void OnDisable()
    {
        QuestService.Instance.QuestChanged -= Refresh;
    }

    // 将任务阶段转换为记录页、铭片与残页的场景可见性。
    private void Refresh(string _)
    {
        bool pageActive = QuestService.Instance.IsInState(UnsignedGuardianQuestIds.LostPage, QuestState.Active);
        bool inscriptionActive = QuestService.Instance.IsInState(UnsignedGuardianQuestIds.GuardianTestimony, QuestState.Active) &&
            golemReporter != null && golemReporter.Consumed;
        // 残页在“灰烬中的档案”进行中、且巨龙已被击败后才显现，与铭片的解锁规则保持一致。
        bool archiveActive = QuestService.Instance.IsInState(UnsignedGuardianQuestIds.EmberArchive, QuestState.Active) &&
            dragonReporter != null && dragonReporter.Consumed;
        if (recordPage != null && recordPageReporter != null && recordPage.activeSelf != pageActive)
            recordPage.SetActive(pageActive);
        if (sealingInscription != null && sealingInscription.activeSelf != inscriptionActive)
            sealingInscription.SetActive(inscriptionActive);
        if (halfBurntArchive != null && halfBurntArchive.activeSelf != archiveActive)
            halfBurntArchive.SetActive(archiveActive);
    }
}

/// <summary>剧情资产、对话和世界对象共用的稳定标识。</summary>
public static class UnsignedGuardianQuestIds
{
    /// <summary>调查记录页任务 ID。</summary>
    public const string LostPage = "unsigned_guardian.lost_page";
    /// <summary>石头人证词任务 ID。</summary>
    public const string GuardianTestimony = "unsigned_guardian.guardian_testimony";
    /// <summary>击败巨龙并夺回残页的后续任务 ID。</summary>
    public const string EmberArchive = "unsigned_guardian.ember_archive";
    /// <summary>记录页交互目标 ID。</summary>
    public const string RecordPage = "unsigned_guardian.record_page";
    /// <summary>石头人击败目标 ID。</summary>
    public const string StoneGolem = "unsigned_guardian.stone_golem";
    /// <summary>封印铭片交互目标 ID。</summary>
    public const string SealingInscription = "unsigned_guardian.sealing_inscription";
    /// <summary>巨龙击败目标 ID。</summary>
    public const string DragonBoss = "unsigned_guardian.dragon_boss";
    /// <summary>未烧尽残页交互目标 ID。</summary>
    public const string HalfBurntArchive = "unsigned_guardian.half_burnt_archive";
}
