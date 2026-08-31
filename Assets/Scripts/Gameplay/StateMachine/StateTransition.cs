using System;

/// <summary>
/// 描述一条带条件和优先级的状态转移规则。
/// </summary>
public sealed class StateTransition<TContext, TState>
{
    /// <summary>转移后的目标状态。</summary>
    public TState To { get; }

    /// <summary>决定转移是否成立的条件。</summary>
    public Func<TContext, bool> Condition { get; }

    /// <summary>是否可以忽略当前状态退出条件并立即切换。</summary>
    public bool Force { get; }

    /// <summary>同一来源状态下的检查优先级。</summary>
    public int Priority { get; }

    /// <summary>创建状态转移规则。</summary>
    public StateTransition(TState to, Func<TContext, bool> condition, bool force, int priority)
    {
        To = to;
        Condition = condition;
        Force = force;
        Priority = priority;
    }
}
