using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单条敌人血条预制体：绑定真实生命事件，显示名称、实时血量和延迟消退的损血残影。</summary>
[DisallowMultipleComponent]
public sealed class UIEnemyBossBar : MonoBehaviour
{
    // 左至右填充的当前生命图像。
    [SerializeField] private Image healthFill;
    // 位于红血下方的金色损血残影。
    [SerializeField] private Image damageTrail;
    // 与血槽分行的敌人名称。
    [SerializeField] private TMP_Text nameLabel;
    // 整条血槽与名称共同淡入淡出。
    [SerializeField] private CanvasGroup visibility;
    // 损血残影停留时间，单位秒。
    [SerializeField, Min(0f)] private float damageHold = 0.35f;
    // 残影每秒追赶的归一化生命比例。
    [SerializeField, Min(0.01f)] private float trailSpeed = 0.65f;
    // 当前订阅的数值组件。
    private EnemyStats stats;
    // 最近一次掉血之后开始收回残影的时刻。
    private float trailStartsAt;

    /// <summary>当前显示的真实生命比例，便于 HUD 检查。</summary>
    public float HealthFraction => healthFill != null ? healthFill.fillAmount : 0f;

    /// <summary>绑定目标并立即刷新满血/残血状态；重复绑定前先清理订阅。</summary>
    public void Bind(EnemyBossHealthTarget target)
    {
        Unsubscribe();
        stats = target.Stats;
        nameLabel.text = target.DisplayName;
        float fraction = stats != null ? stats.CurrentHealth / Mathf.Max(1f, stats.MaxHealth) : 0f;
        healthFill.fillAmount = fraction;
        damageTrail.fillAmount = fraction;
        visibility.alpha = 0f;
        if (stats != null)
            stats.ResourceChanged += OnResourceChanged;
    }

    /// <summary>由容器统一控制血槽和名称的透明度。</summary>
    public void SetVisibility(float alpha) => visibility.alpha = alpha;

    // 只处理生命变化；治疗立即补齐，掉血保留金色残影后渐退。
    private void OnResourceChanged(ResourceChangedEvent change)
    {
        if (change.ResourceType != ResourceType.Health)
            return;
        float fraction = change.Current / Mathf.Max(1f, change.Maximum);
        if (fraction < healthFill.fillAmount)
            trailStartsAt = Time.unscaledTime + damageHold;
        else
            damageTrail.fillAmount = fraction;
        healthFill.fillAmount = fraction;
    }

    // 使用不受时间缩放影响的时间更新残影，暂停期间不会产生跳变。
    private void Update()
    {
        if (Time.unscaledTime >= trailStartsAt)
            damageTrail.fillAmount = Mathf.MoveTowards(damageTrail.fillAmount, healthFill.fillAmount,
                trailSpeed * Time.unscaledDeltaTime);
    }

    // 删除行时释放对敌人数值事件的引用。
    private void OnDestroy() => Unsubscribe();

    // 解除旧目标的生命事件订阅。
    private void Unsubscribe()
    {
        if (stats != null)
            stats.ResourceChanged -= OnResourceChanged;
        stats = null;
    }
}
