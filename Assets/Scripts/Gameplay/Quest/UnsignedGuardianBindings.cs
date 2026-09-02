using UnityEngine;

/// <summary>集中保存《未署名的守卫》场景中 NPC、记录页与石头人的可视化引用。</summary>
public sealed class UnsignedGuardianBindings : MonoBehaviour
{
    [SerializeField] private GameObject recordPage;
    [SerializeField] private GameObject sealingInscription;
    [SerializeField] private QuestWorldReporter recordPageReporter;
    [SerializeField] private QuestWorldReporter golemReporter;

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

    // 将任务阶段转换为记录页与铭片的场景可见性。
    private void Refresh(string _)
    {
        bool pageActive = QuestService.Instance.IsInState(UnsignedGuardianQuestIds.LostPage, QuestState.Active);
        bool inscriptionActive = QuestService.Instance.IsInState(UnsignedGuardianQuestIds.GuardianTestimony, QuestState.Active) &&
            golemReporter != null && golemReporter.Consumed;
        if (recordPage != null && recordPageReporter != null && recordPage.activeSelf != pageActive)
            recordPage.SetActive(pageActive);
        if (sealingInscription != null && sealingInscription.activeSelf != inscriptionActive)
            sealingInscription.SetActive(inscriptionActive);
    }
}

/// <summary>剧情资产、对话和世界对象共用的稳定标识。</summary>
public static class UnsignedGuardianQuestIds
{
    /// <summary>调查记录页任务 ID。</summary>
    public const string LostPage = "unsigned_guardian.lost_page";
    /// <summary>石头人证词任务 ID。</summary>
    public const string GuardianTestimony = "unsigned_guardian.guardian_testimony";
    /// <summary>记录页交互目标 ID。</summary>
    public const string RecordPage = "unsigned_guardian.record_page";
    /// <summary>石头人击败目标 ID。</summary>
    public const string StoneGolem = "unsigned_guardian.stone_golem";
    /// <summary>封印铭片交互目标 ID。</summary>
    public const string SealingInscription = "unsigned_guardian.sealing_inscription";
}
