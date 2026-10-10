using System.Collections;
using UnityEngine;

/// <summary>
/// 攻击预警组件：在怪物前摇期间点亮红/黄预警光，并在真正的落点画出地面危险区。
/// 红光 = 不可格挡（只能翻滚/拉开），黄光 = 高破防消耗（建议翻滚）。
///
/// 表现分三层，故意做得互相独立，任何一层出问题都不至于让玩家看不到预警：
///   1）本体预警灯：挂在怪物身上，提示"这家伙要动手了"，带呼吸脉冲；
///   2）地面危险区：GroundTelegraphFX 生成的贴地圆盘／扇形 + 定界环 + 进度填充，
///      无光照、颜色精确等于招式语义色，并有一盏落在危险区中心的彩色点光；
///   3）脉冲地面冲击波：素材包的 Shockwave 预制体，按招式色染色后每隔一段时间脉冲一次。
/// 所有表现都是懒创建／一次性实例，未配置时不产生任何开销。
/// </summary>
[DisallowMultipleComponent]
public sealed class AttackTelegraph : MonoBehaviour
{
    // 地面范围圈使用的预制体（FX_Ring 尘圈）；为空时跳过敏化层。
    [SerializeField] private GameObject groundRingPrefab;
    // 脉冲地面冲击波使用的预制体（Treant Shockwave，一次性贴地横排）；为空时跳过。
    [SerializeField] private GameObject groundWavePrefab;
    // 地面冲击波的脉冲间隔，单位为秒。
    [SerializeField, Min(0.1f)] private float wavePulseInterval = 0.35f;
    // 单次地面冲击波的存活时长，单位为秒。
    [SerializeField, Min(0.1f)] private float groundWaveLifetime = 1.1f;
    // 地面冲击波相对危险区半径的缩放系数。
    [SerializeField, Min(0.1f)] private float groundWaveScale = 0.95f;
    // 预警灯默认高度偏移，单位为米。
    [SerializeField, Min(0f)] private float lightHeight = 2f;
    // 预警灯强度（低于最低强度时按最低强度处理）。
    [SerializeField, Min(0f)] private float lightIntensity = 8f;
    // 预警灯有效半径。
    [SerializeField, Min(0f)] private float lightRange = 10f;
    // 预警灯强度的最低下限，避免配置过小导致完全看不见。
    private const float MinimumLightIntensity = 6f;

    // 懒创建的预警灯。
    private Light warningLight;
    // 灯光淡出进度。
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
        // 场景大量使用烘焙光，强制逐像素渲染才能让预警光真正被看见。
        warningLight.renderMode = LightRenderMode.ForcePixel;
    }

    /// <summary>
    /// 圆形危险区预警：闪烁本体预警灯，并在落点画出贴地圆盘 + 定界环 + 进度填充 + 脉冲地面冲击波。
    /// groundRingPosition/groundRingRadius 缺省（或用 radius ≤ 0）时只闪本体灯。
    /// </summary>
    /// <param name="color">招式语义色：红＝不可格挡、黄＝高破防。</param>
    /// <param name="duration">前摇时长；地面进度填充会在这段时间内长满。</param>
    /// <param name="groundRingPosition">危险区中心的地面点。</param>
    /// <param name="groundRingRadius">危险区半径，应与实际伤害半径一致。</param>
    public void Flash(Color color, float duration, Vector3? groundRingPosition = null, float groundRingRadius = 0f)
    {
        StartBossLight(color, duration);

        if (!groundRingPosition.HasValue || groundRingRadius <= 0f)
            return;

        Vector3 center = groundRingPosition.Value;

        // 无光照贴地图形：始终可见，不受场景亮度影响。
        GroundTelegraphFX.ShowDisc(center, groundRingRadius, color, duration);

        // 素材包的尘圈：染色后作为附加细节层。
        PlayTintedDecal(groundRingPrefab, center, groundRingRadius * 2f, duration + 0.4f, color);

        RestartPulse(center, groundRingRadius, duration, color);
    }

    /// <summary>
    /// 扇形危险区预警（龙息这类锥形范围技）：标出射程与张角，而不是用一个不相干的圆圈。
    /// </summary>
    /// <param name="color">预警颜色。</param>
    /// <param name="duration">前摇时长。</param>
    /// <param name="origin">扇形顶点（BOSS 脚下／嘴部）。</param>
    /// <param name="forward">扇形朝向。</param>
    /// <param name="range">扇形长度，应与实际射程一致。</param>
    /// <param name="halfAngle">扇形半角（度），应与实际判定半角一致。</param>
    public void FlashCone(Color color, float duration, Vector3 origin, Vector3 forward, float range, float halfAngle)
    {
        StartBossLight(color, duration);

        GroundTelegraphFX.ShowCone(origin, forward, range, halfAngle, color, duration);

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        RestartPulse(origin + flatForward * (range * 0.35f), range * 0.45f, duration, color);
    }

    // 点亮并呼吸式淡出本体预警灯。
    private void StartBossLight(Color color, float duration)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        EnsureLight();
        warningLight.color = color;
        warningLight.intensity = Mathf.Max(lightIntensity, MinimumLightIntensity);
        lightFadeRemaining = duration;
        lightFadeDuration = Mathf.Max(0.01f, duration);
        fadeRoutine = StartCoroutine(FadeLightRoutine());
    }

    // 播放一次染色尘圈（关闭循环、注入爆发、到期自动销毁）。
    private void PlayTintedDecal(GameObject prefab, Vector3 position, float scale, float lifetime, Color color)
    {
        if (prefab == null)
            return;

        OneShotParticle.Play(
            prefab,
            position + Vector3.up * 0.1f,
            Mathf.Max(0.1f, scale),
            Mathf.Max(0.3f, lifetime),
            color);
    }

    // 重开脉冲地面冲击波协程。
    private void RestartPulse(Vector3 position, float radius, float duration, Color color)
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseGroundWaveRoutine(position, radius, duration, color));
    }

    // 淡出预警灯：加一点呼吸脉冲，让它在静态场景里更容易被注意到。
    private IEnumerator FadeLightRoutine()
    {
        float baseIntensity = Mathf.Max(lightIntensity, MinimumLightIntensity);
        float phase = 0f;

        while (lightFadeRemaining > 0f)
        {
            lightFadeRemaining -= Time.deltaTime;
            phase += Time.deltaTime * 9f;

            float fade = Mathf.Clamp01(lightFadeRemaining / lightFadeDuration);
            float pulse = 0.78f + 0.22f * Mathf.Sin(phase);
            warningLight.intensity = baseIntensity * fade * pulse;
            yield return null;
        }

        warningLight.intensity = 0f;
        fadeRoutine = null;
    }

    // 在前摇期间按间隔在危险区脉冲地面冲击波（用招式颜色染色，红=不可格挡、黄=高破防）。
    private IEnumerator PulseGroundWaveRoutine(Vector3 position, float radius, float duration, Color color)
    {
        float elapsed = 0f;

        // 先把地面波的位置与朝向定好：始终沿 BOSS 指向危险区的方向铺开。
        Vector3 toZone = position - transform.position;
        toZone.y = 0f;
        Quaternion rotation = toZone.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toZone.normalized, Vector3.up)
            : transform.rotation;

        while (elapsed < duration)
        {
            if (groundWavePrefab != null)
            {
                OneShotParticle.Play(
                    groundWavePrefab,
                    position + Vector3.up * 0.05f,
                    Mathf.Max(0.5f, radius * groundWaveScale),
                    groundWaveLifetime,
                    color,
                    rotation);
            }

            yield return new WaitForSeconds(wavePulseInterval);
            elapsed += wavePulseInterval;
        }

        pulseRoutine = null;
    }
}
