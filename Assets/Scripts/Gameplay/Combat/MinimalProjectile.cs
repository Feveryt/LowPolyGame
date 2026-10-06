using System;
using UnityEngine;

/// <summary>
/// 极简投射物：直线飞行、命中首个 IDamageable 后结算并回收。
/// 供石头人震波与龙息火球复用；通过 Launch 完成初始化。
/// 支持自转（抛出的石头打转）、命中事件（供外部挂碎石/爆花表现）——
/// 命中目标、撞上非目标碰撞体或超时消失时都会触发 Impacted，携带消失点坐标。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MinimalProjectile : MonoBehaviour
{
    // 飞行速度，单位为米每秒。
    [SerializeField, Min(0.1f)] private float speed = 10f;
    // 最长存活时间，超时自动销毁，单位为秒。
    [SerializeField, Min(0.1f)] private float lifetime = 1.5f;
    // 自转速度（度每秒，三轴同速），0 表示不自转。
    [SerializeField, Min(0f)] private float spinSpeed = 0f;

    // 当前飞行方向。
    private Vector3 direction;
    // 伤害结算使用的完整请求。
    private DamageRequest damageRequest;
    // 发起者数值组件，用于归因。
    private CharacterStats owner;
    // 发起者根对象，避免命中自己。
    private Transform ownerRoot;
    // 剩余存活时间。
    private float remainingLife;
    // 是否已经触发过命中/消失事件，防止重复。
    private bool impactReported;

    /// <summary>投射物消失时触发（命中目标、撞墙或超时），参数为消失点世界坐标。</summary>
    public event Action<Vector3> Impacted;

    /// <summary>初始化并发射投射物。</summary>
    public void Launch(
        Vector3 direction,
        CharacterStats owner,
        DamageRequest damageRequest,
        float? speedOverride = null,
        float? lifetimeOverride = null,
        float? spinSpeedOverride = null)
    {
        this.direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : transform.forward;
        this.owner = owner;
        this.ownerRoot = owner != null ? owner.transform.root : null;
        this.damageRequest = damageRequest;

        if (speedOverride.HasValue)
            speed = Mathf.Max(0.1f, speedOverride.Value);
        if (lifetimeOverride.HasValue)
            lifetime = Mathf.Max(0.1f, lifetimeOverride.Value);
        if (spinSpeedOverride.HasValue)
            spinSpeed = Mathf.Max(0f, spinSpeedOverride.Value);

        remainingLife = lifetime;
    }

    // 直线飞行、自转与超时检测。
    private void Update()
    {
        transform.position += direction * (speed * Time.deltaTime);

        if (spinSpeed > 0f)
            transform.Rotate(Vector3.up + Vector3.right, spinSpeed * Time.deltaTime, Space.Self);

        remainingLife -= Time.deltaTime;
        if (remainingLife <= 0f)
            ReportImpact();
    }

    // 命中可受击目标后结算并销毁。
    private void OnTriggerEnter(Collider other)
    {
        if (impactReported)
            return;

        if (ownerRoot != null && other.transform.root == ownerRoot)
            return;

        CharacterStats target = other.GetComponentInParent<CharacterStats>();
        if (target != null && target.IsAlive && target != owner)
        {
            target.TakeDamage(damageRequest);
            ReportImpact();
            return;
        }

        // 撞上场景障碍物（墙/地面/道具）：也视为消失点，触发表现但不结算伤害。
        if (!other.isTrigger)
            ReportImpact();
    }

    // 通知外部表现层并销毁自身。
    private void ReportImpact()
    {
        if (impactReported)
            return;

        impactReported = true;
        Impacted?.Invoke(transform.position);
        Destroy(gameObject);
    }
}
