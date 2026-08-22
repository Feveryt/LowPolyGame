using UnityEngine;

/// <summary>
/// 玩家专用运行时属性组件。
/// 在共享属性基类上增加奔跑体力的耗尽锁定与延迟恢复，不负责技能蓝量的具体消耗时机。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStats : CharacterStats
{
    // 体力归零后用于阻止立即反复奔跑的状态标记。
    private bool staminaExhausted;
    // 最近一次奔跑扣除体力的游戏时间。
    private float lastStaminaSpendTime = float.NegativeInfinity;

    /// <summary>当前是否有足够体力进入或维持奔跑。</summary>
    public bool CanSprint
    {
        get
        {
            if (!IsAlive || Definition == null || MaxStamina <= 0f)
                return false;

            return !staminaExhausted
                ? CurrentStamina > Mathf.Epsilon
                : CurrentStamina >= Definition.StaminaResumeThreshold;
        }
    }

    /// <summary>配置定义的每秒奔跑体力消耗。</summary>
    public float StaminaDrainPerSecond => Definition != null ? Definition.StaminaDrainPerSecond : 0f;

    // 初始化共享资源和玩家特有的体力状态。
    protected override void Awake()
    {
        base.Awake();
        staminaExhausted = false;
    }

    // 每帧处理停止奔跑后的体力延迟恢复。
    private void Update()
    {
        RecoverStamina();
    }

    /// <summary>为持续奔跑扣除体力，允许最后一帧消耗剩余不足额的体力。</summary>
    public bool SpendSprintStamina(float amount)
    {
        if (!CanSprint || amount <= 0f)
            return false;

        if (staminaExhausted && CurrentStamina >= Definition.StaminaResumeThreshold)
            staminaExhausted = false;

        float spent = SpendResourceUpTo(ResourceType.Stamina, amount);
        if (spent <= 0f)
        {
            staminaExhausted = true;
            return false;
        }

        lastStaminaSpendTime = Time.time;
        if (CurrentStamina <= Mathf.Epsilon)
            staminaExhausted = true;

        return true;
    }

    /// <summary>恢复所有资源时同时重置体力耗尽和恢复计时状态。</summary>
    public override void ResetRuntimeStats()
    {
        base.ResetRuntimeStats();
        staminaExhausted = false;
        lastStaminaSpendTime = float.NegativeInfinity;
    }

    // 在恢复延迟结束后按配置速率补充体力。
    private void RecoverStamina()
    {
        if (!IsAlive || Definition == null || MaxStamina <= 0f || CurrentStamina >= MaxStamina)
            return;

        if (Time.time < lastStaminaSpendTime + Definition.StaminaRecoveryDelay)
            return;

        RestoreStamina(Definition.StaminaRecoveryPerSecond * Time.deltaTime);
    }
}