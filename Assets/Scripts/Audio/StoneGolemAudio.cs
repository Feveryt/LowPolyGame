using System.Collections;
using UnityEngine;

/// <summary>
/// 将石头守卫的伤害和死亡事件转换为分层三维音效，不在敌人逻辑中直接引用音频资源。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolem))]
public sealed class StoneGolemAudio : MonoBehaviour
{
    [SerializeField, Min(0f)] private float collapseDelay = 0.2f;
    private CharacterStats characterStats;
    private bool deathHandled;

    // 缓存守卫运行时属性。
    private void Awake()
    {
        StoneGolem golem = GetComponent<StoneGolem>();
        characterStats = golem != null ? golem.Stats : GetComponent<CharacterStats>();
    }

    // 订阅守卫的战斗事件。
    private void OnEnable()
    {
        if (characterStats == null)
            characterStats = GetComponent<CharacterStats>();
        if (characterStats == null)
            return;
        characterStats.DamageReceived += OnDamageReceived;
        characterStats.Died += OnDied;
    }

    // 解除事件订阅并停止延迟尾音。
    private void OnDisable()
    {
        if (characterStats != null)
        {
            characterStats.DamageReceived -= OnDamageReceived;
            characterStats.Died -= OnDied;
        }
        StopAllCoroutines();
    }

    // 仅在非致命有效伤害后播放裂石受击声。
    private void OnDamageReceived(DamageResult result)
    {
        if (result.WasApplied && !result.WasLethal)
            AudioManager.Instance?.PlayStoneGolemHurt(transform.position);
    }

    // 将死亡主体声和坍塌尾音拆开，确保每次死亡只触发一次。
    private void OnDied(CharacterStats stats)
    {
        if (deathHandled)
            return;
        deathHandled = true;
        AudioManager.Instance?.PlayStoneGolemDeath(transform.position);
        StartCoroutine(PlayCollapseAfterDelay());
    }

    // 等待死亡主体声起音后播放坍塌声。
    private IEnumerator PlayCollapseAfterDelay()
    {
        if (collapseDelay > 0f)
            yield return new WaitForSeconds(collapseDelay);
        AudioManager.Instance?.PlayStoneGolemCollapse(transform.position);
    }
}
