using UnityEngine;
using Cinemachine;

/// <summary>
/// 命中反馈控制器：挂在玩家身上，通过 Cinemachine Impulse 让主相机产生分级屏幕震动。
/// 轻攻击命中轻震、重攻击命中重震、被命中中震；被格挡/被弹反的攻击不触发。
/// 主相机需要挂 CinemachineImpulseListener（由装配脚本保证）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineImpulseSource))]
public sealed class HitFeedbackController : MonoBehaviour
{
    // 轻攻击命中的震动强度。
    [SerializeField, Min(0f)] private float lightHitShake = 0.35f;
    // 重攻击命中的震动强度。
    [SerializeField, Min(0f)] private float heavyHitShake = 0.9f;
    // 玩家被命中时的震动强度。
    [SerializeField, Min(0f)] private float hurtShake = 0.6f;
    // 格挡穿透伤害的轻震动强度。
    [SerializeField, Min(0f)] private float blockedShake = 0.15f;
    // 震动的随机方向扰动幅度（避免每次手感完全一致）。
    [SerializeField, Range(0f, 1f)] private float directionJitter = 0.35f;

    // Cinemachine 冲击源。
    private CinemachineImpulseSource impulseSource;

    /// <summary>全局共享访问点（玩家唯一）。</summary>
    public static HitFeedbackController Instance { get; private set; }

    // 缓存单例与冲击源。
    private void Awake()
    {
        Instance = this;
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // 销毁时清空单例引用。
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>玩家攻击命中敌人（确认扣血）时的震动；blocked 表示穿透格挡的削减伤害。</summary>
    public void TriggerHit(AttackType attackType, bool blocked)
    {
        float strength = blocked
            ? blockedShake
            : attackType == AttackType.HeavyAttack ? heavyHitShake : lightHitShake;
        Fire(strength);
    }

    /// <summary>玩家被敌人命中（确认扣血）时的震动。</summary>
    public void TriggerHurt()
    {
        Fire(hurtShake);
    }

    // 以指定强度沿随机方向触发一次冲击。
    private void Fire(float strength)
    {
        if (impulseSource == null || strength <= 0f)
            return;

        Vector3 velocity = new Vector3(
            Random.Range(-directionJitter, directionJitter),
            Random.Range(-directionJitter, directionJitter),
            strength);
        impulseSource.GenerateImpulse(velocity);
    }
}
