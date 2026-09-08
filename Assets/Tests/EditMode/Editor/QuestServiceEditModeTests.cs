using NUnit.Framework;
using System.Linq;
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

    /// <summary>任务面板快照只显示已接取任务，并正确映射顺序目标的完成和当前状态。</summary>
    [Test]
    public void VisibleSnapshots_ExcludeAvailableQuest_AndExposeSequentialObjectiveState()
    {
        Assert.That(service.GetVisibleQuests(), Is.Empty);

        Assert.That(service.StartQuest(UnsignedGuardianQuestIds.LostPage), Is.True);
        QuestSnapshot active = service.GetVisibleQuests().Single();
        Assert.That(active.State, Is.EqualTo(QuestState.Active));
        Assert.That(active.Objectives[0].IsCurrent, Is.True);
        Assert.That(active.Objectives[0].IsCompleted, Is.False);

        Assert.That(service.NotifyInteraction(UnsignedGuardianQuestIds.RecordPage), Is.True);
        QuestSnapshot readyToTurnIn = service.GetVisibleQuests().Single();
        Assert.That(readyToTurnIn.State, Is.EqualTo(QuestState.ReadyToTurnIn));
        Assert.That(readyToTurnIn.Objectives[0].IsCompleted, Is.True);
        Assert.That(readyToTurnIn.Objectives[0].IsCurrent, Is.False);

        Assert.That(service.SubmitQuest(UnsignedGuardianQuestIds.LostPage), Is.True);
        QuestSnapshot completed = service.GetVisibleQuests().Single();
        Assert.That(completed.State, Is.EqualTo(QuestState.Completed));
        Assert.That(completed.Objectives[0].IsCompleted, Is.True);
    }
}
