using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗反馈组件：精准防御等关键时刻的短促顿帧（hitstop）与局部特效。
/// 顿帧通过极短的全局时间缩放实现，使用真实时间恢复，可安全叠加。
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatFeedback : MonoBehaviour
{
    // 顿帧期间使用的最小时间缩放。
    [SerializeField, Range(0.01f, 0.5f)] private float hitStopTimeScale = 0.05f;
    // 顿帧的实际时长，单位为秒。
    [SerializeField, Range(0f, 0.3f)] private float parryHitStopDuration = 0.06f;
    // 破防时的顿帧时长，单位为秒。
    [SerializeField, Range(0f, 0.3f)] private float guardBreakHitStopDuration = 0.12f;

    // 当前顿帧流程实例，保证叠加请求只延长当前顿帧。
    private Coroutine activeHitStop;

    /// <summary>播放精准防御反馈：短顿帧。</summary>
    public void PlayPerfectGuardFeedback()
    {
        StartHitStop(parryHitStopDuration);
    }

    /// <summary>播放破防反馈：更长顿帧。</summary>
    public void PlayGuardBreakFeedback()
    {
        StartHitStop(guardBreakHitStopDuration);
    }

    // 启动或延长一次顿帧。
    private void StartHitStop(float duration)
    {
        if (duration <= 0f)
            return;

        if (activeHitStop != null)
            StopCoroutine(activeHitStop);

        activeHitStop = StartCoroutine(HitStopRoutine(duration));
    }

    // 冻结时间缩放并在真实时间结束后恢复。
    private IEnumerator HitStopRoutine(float duration)
    {
        float previousScale = Time.timeScale;
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousScale;
        activeHitStop = null;
    }

    // 组件禁用时恢复时间缩放，防止顿帧卡死全局时间。
    private void OnDisable()
    {
        if (activeHitStop != null)
        {
            StopCoroutine(activeHitStop);
            activeHitStop = null;
        }

        Time.timeScale = 1f;
    }
}
