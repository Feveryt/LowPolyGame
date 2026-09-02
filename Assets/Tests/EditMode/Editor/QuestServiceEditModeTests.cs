using NUnit.Framework;
using UnityEngine;

/// <summary>验证《未署名的守卫》两条任务的顺序、幂等性与本地持久化行为。</summary>
public sealed class QuestServiceEditModeTests
{
    private QuestService service;

    // 每条测试从空任务存档创建独立服务。
    [SetUp]
    public void SetUp()
    {
        QuestService.ClearProgress();
        service = QuestService.Instance;
    }

    // 清理静态服务和 PlayerPrefs，避免测试间共享状态。
    [TearDown]
    public void TearDown()
    {
        QuestService.ClearProgress();
        if (service != null)
            Object.DestroyImmediate(service.gameObject);
    }

    /// <summary>记录页不能提前消耗，接取后只推进一次并可正确交付。</summary>
    [Test]
    public void LostPage_RequiresActiveQuest_AndCompletesOnce()
    {
        Assert.That(service.GetState(UnsignedGuardianQuestIds.LostPage), Is.EqualTo(QuestState.Available));
        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.RecordPage), Is.False);
        Assert.That(service.StartQuest(UnsignedGuardianQuestIds.LostPage), Is.True);
        Assert.That(service.StartQuest(UnsignedGuardianQuestIds.LostPage), Is.False);
        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.RecordPage), Is.True);
        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.RecordPage), Is.False);
        Assert.That(service.GetState(UnsignedGuardianQuestIds.LostPage), Is.EqualTo(QuestState.ReadyToTurnIn));
        Assert.That(service.SubmitQuest(UnsignedGuardianQuestIds.LostPage), Is.True);
        Assert.That(service.SubmitQuest(UnsignedGuardianQuestIds.LostPage), Is.False);
        Assert.That(service.GetState(UnsignedGuardianQuestIds.LostPage), Is.EqualTo(QuestState.Completed));
    }

    /// <summary>守卫任务严格按击败敌人、取得铭片、返回交付的顺序推进。</summary>
    [Test]
    public void GuardianTestimony_RequiresKillBeforeInscription()
    {
        Assert.That(service.NotifyEnemyKilled(UnsignedGuardianQuestIds.StoneGolem), Is.False);
        Assert.That(service.StartQuest(UnsignedGuardianQuestIds.GuardianTestimony), Is.True);
        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.SealingInscription), Is.False);
        Assert.That(service.NotifyEnemyKilled(UnsignedGuardianQuestIds.StoneGolem), Is.True);
        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.SealingInscription), Is.True);
        Assert.That(service.GetState(UnsignedGuardianQuestIds.GuardianTestimony), Is.EqualTo(QuestState.ReadyToTurnIn));
        Assert.That(service.SubmitQuest(UnsignedGuardianQuestIds.GuardianTestimony), Is.True);
        Assert.That(service.GetState(UnsignedGuardianQuestIds.GuardianTestimony), Is.EqualTo(QuestState.Completed));
    }
}
