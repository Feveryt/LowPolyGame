using System.Collections;
using UnityEngine;

/// <summary>
/// 攻击预警组件：在怪物前摇期间点亮红/黄预警光、投射范围圈，并以脉冲地面冲击波标示危险区。
/// 红光 = 不可格挡（只能翻滚/拉开），黄光 = 高破防消耗（建议翻滚）。
/// 灯光、范围圈与地面波均为懒创建/一次性实例，未配置时不产生任何表现。
/// </summary>
[DisallowMultipleComponent]
public sealed class AttackTelegraph : MonoBehaviour
{
    // 地面范围圈使用的预制体（FX_Ring 尘圈）；为空时跳过。
    [SerializeField] private GameObject groundRingPrefab;
    // 脉冲地面冲击波使用的预制体（Treant Shockwave，一次性贴地横排）；为空时跳过。
    [SerializeField] private GameObject groundWavePrefab;
    // 地面冲击波的脉冲间隔，单位为秒。
    [SerializeField, Min(0.1f)] private float wavePulseInterval = 0.35f;
    // 单次地面冲击波的存活时长，单位为秒。
    [SerializeField, Min(0.1f)] private float groundWaveLifetime = 1.1f;
    // 地面冲击波相对危险区半径的缩放系数。
    [SerializeField, Min(0.1f)] private float groundWaveScale = 0.7f;
    // 预警灯默认高度偏移，单位为米。
    [SerializeField, Min(0f)] private float lightHeight = 2f;
    // 预警灯强度。
    [SerializeField, Min(0f)] private float lightIntensity = 7f;
    // 预警灯有效半径。
    [SerializeField, Min(0f)] private float lightRange = 9f;

    // 懒创建的预警灯。
    private Light warningLight;
    // 灯光当前淡出进度。
    private float lightFadeRemaining;
    // 灯光淡出总时长。
    private float lightFadeDuration;
    // 当前淡出协程。
    private Coroutine fadeRoutine;
    // 当前脉冲地面波协程。
    private Coroutine pulseRoutine;

    // 创建预警灯并归零强度。
    private void Awake()
    {
        EnsureLight();
    }

    // 禁用时停止一切持续表现。
    private void OnDisable()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (warningLight != null)
            warningLight.intensity = 0f;
    }

    // 确保预警灯存在。
    private void EnsureLight()
    {
        if (warningLight != null)
            return;

        GameObject lightObject = new GameObject("TelegraphLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * lightHeight;
        warningLight = lightObject.AddComponent<Light>();
        warningLight.type = LightType.Point;
        warningLight.range = lightRange;
        warningLight.intensity = 0f;
        warningLight.shadows = LightShadows.None;
    }

    /// <summary>
    /// 以指定颜色闪烁预警灯，并在攻击落点显示范围圈与脉冲地面冲击波。
    /// groundRingPosition/groundRingRadius 缺省时只闪灯。
    /// </summary>
    public void Flash(Color color, float duration, Vector3? groundRingPosition = null, float groundRingRadius = 0f)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        EnsureLight();
        warningLight.color = color;
        warningLight.intensity = lightIntensity;
        lightFadeRemaining = duration;
        lightFadeDuration = Mathf.Max(0.01f, duration);
        fadeRoutine = StartCoroutine(FadeLightRoutine());

        if (groundRingPosition.HasValue && groundRingRadius > 0f)
        {
            SpawnGroundRing(groundRingPosition.Value, groundRingRadius, duration);

            if (pulseRoutine != null)
                StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseGroundWaveRoutine(
                groundRingPosition.Value, groundRingRadius, duration, color));
        }
    }

    // 淡出预警灯。
    private IEnumerator FadeLightRoutine()
    {
        while (lightFadeRemaining > 0f)
        {
            lightFadeRemaining -= Time.deltaTime;
            warningLight.intensity = lightIntensity * Mathf.Clamp01(lightFadeRemaining / lightFadeDuration);
            yield return null;
        }

        warningLight.intensity = 0f;
        fadeRoutine = null;
    }

    // 在前摇期间按间隔在危险区脉冲地面冲击波（用招式颜色染色，红=不可格挡、黄=高破防）。
    private IEnumerator PulseGroundWaveRoutine(Vector3 position, float radius, float duration, Color color)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (groundWavePrefab != null)
            {
                OneShotParticle.Play(
                    groundWavePrefab,
                    position + Vector3.up * 0.05f,
                    radius * groundWaveScale,
                    groundWaveLifetime,
                    color);
            }

            yield return new WaitForSeconds(wavePulseInterval);
            elapsed += wavePulseInterval;
        }

        pulseRoutine = null;
    }

    // 在指定位置生成地面范围圈，播放完自动回收。
    private void SpawnGroundRing(Vector3 position, float radius, float duration)
    {
        if (groundRingPrefab == null)
            return;

        GameObject ring = Instantiate(
            groundRingPrefab,
            position + Vector3.up * 0.1f,
            Quaternion.identity);
        ring.transform.localScale = Vector3.one * (radius * 2f);
        Destroy(ring, duration + 0.5f);
    }
}
