using UnityEngine;

/// <summary>
/// 玩家受击与死亡表现的事件协调组件。
/// 将 PlayerStats 的伤害/死亡事件转换为 PlayerAnimation 动画调用，不参与伤害数值计算。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerAnimation))]
public sealed class PlayerHitReaction : MonoBehaviour
{
    // 提供受伤结果、死亡状态和对应事件的玩家数值组件。
    [SerializeField] private PlayerStats playerStats;
    // 负责触发 Damaged 与 Dead Animator 参数的玩家动画组件。
    [SerializeField] private PlayerAnimation playerAnimation;
    // 受击动画未结束时累计达到该次数后触发霸体。
    [SerializeField, Min(1)] private int hitsBeforeSuperArmor = 3;
    // 霸体免伤持续时间，单位为秒。
    [SerializeField, Min(0f)] private float superArmorDuration = 1f;
    // Animator 状态查询尚未更新时，用于维持本次受击判定窗口的保底时长。
    [SerializeField, Min(0f)] private float hurtAnimationFallbackDuration = 0.75f;

    // 本次生命流程是否已经触发死亡动画，防止 DamageReceived 与 Died 重复播放。
    private bool deathAnimationTriggered;
    // 当前连续受击组已收到的有效伤害次数。
    private int consecutiveHitCount;
    // 当前受击判定窗口的结束时间。
    private float hurtWindowEndsAt = float.NegativeInfinity;

    // 缓存玩家数值和动画组件引用。
    private void Awake()
    {
        playerStats = playerStats != null ? playerStats : GetComponent<PlayerStats>();
        playerAnimation = playerAnimation != null ? playerAnimation : GetComponent<PlayerAnimation>();
    }

    // 启用时订阅伤害结果与死亡事件。
    private void OnEnable()
    {
        if (playerStats == null)
            return;

        playerStats.DamageReceived += OnDamageReceived;
        playerStats.Died += OnDied;
    }

    // 初始化完成后同步处理以死亡状态启用的玩家对象。
    private void Start()
    {
        if (playerStats != null && !playerStats.IsAlive)
            PlayDeathOnce();
    }

    // 每帧检测受击动画结束并清空连续受击计数。
    private void Update()
    {
        if (consecutiveHitCount > 0 && Time.time >= hurtWindowEndsAt &&
            (playerAnimation == null || !playerAnimation.IsPlayingHurt()))
        {
            consecutiveHitCount = 0;
            hurtWindowEndsAt = float.NegativeInfinity;
        }
    }

    // 禁用时取消事件订阅，避免对象重复启用后多次响应。
    private void OnDisable()
    {
        if (playerStats == null)
            return;

        playerStats.DamageReceived -= OnDamageReceived;
        playerStats.Died -= OnDied;
        consecutiveHitCount = 0;
        hurtWindowEndsAt = float.NegativeInfinity;
    }

    // 将有效伤害结果分流到受击动画或死亡动画。
    private void OnDamageReceived(DamageResult result)
    {
        if (!result.WasApplied)
            return;

        if (result.WasLethal)
        {
            consecutiveHitCount = 0;
            PlayDeathOnce();
            return;
        }

        if (deathAnimationTriggered || playerStats.IsDamageImmune)
            return;

        consecutiveHitCount++;
        if (consecutiveHitCount >= Mathf.Max(1, hitsBeforeSuperArmor))
        {
            consecutiveHitCount = 0;
            hurtWindowEndsAt = float.NegativeInfinity;
            playerAnimation?.CancelHurt();
            playerStats.GrantDamageImmunity(superArmorDuration);
            return;
        }

        hurtWindowEndsAt = Time.time + Mathf.Max(0f, hurtAnimationFallbackDuration);
        playerAnimation?.PlayHurt();
    }

    // 响应数值组件死亡事件，覆盖调试改血等非伤害死亡入口。
    private void OnDied(CharacterStats stats)
    {
        if (stats == playerStats)
            PlayDeathOnce();
    }

    // 保证一次生命流程最多触发一次死亡动画。
    private void PlayDeathOnce()
    {
        if (deathAnimationTriggered)
            return;

        deathAnimationTriggered = true;
        consecutiveHitCount = 0;
        hurtWindowEndsAt = float.NegativeInfinity;
        playerAnimation?.PlayDie();
    }
}
