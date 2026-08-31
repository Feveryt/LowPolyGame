using System;

/// <summary>
/// 通用有限状态机的状态生命周期接口。
/// </summary>
public interface IState<TContext>
{
    /// <summary>进入状态时调用一次。</summary>
    void OnEnter(TContext context);

    /// <summary>状态处于活动状态时每帧调用。</summary>
    void OnLogic(TContext context);

    /// <summary>离开状态时调用一次。</summary>
    void OnExit(TContext context);

    /// <summary>返回当前是否允许普通转移离开该状态。</summary>
    bool CanExit(TContext context);
}

/// <summary>
/// 通过委托快速定义状态生命周期的默认实现。
/// </summary>
public sealed class State<TContext> : IState<TContext>
{
    private readonly Action<TContext> onEnter;
    private readonly Action<TContext> onLogic;
    private readonly Action<TContext> onExit;
    private readonly Func<TContext, bool> canExit;

    /// <summary>创建一个可选生命周期回调和退出条件的状态。</summary>
    public State(
        Action<TContext> onEnter = null,
        Action<TContext> onLogic = null,
        Action<TContext> onExit = null,
        Func<TContext, bool> canExit = null)
    {
        this.onEnter = onEnter;
        this.onLogic = onLogic;
        this.onExit = onExit;
        this.canExit = canExit;
    }

    /// <summary>执行进入回调。</summary>
    public void OnEnter(TContext context) => onEnter?.Invoke(context);

    /// <summary>执行逐帧逻辑回调。</summary>
    public void OnLogic(TContext context) => onLogic?.Invoke(context);

    /// <summary>执行退出回调。</summary>
    public void OnExit(TContext context) => onExit?.Invoke(context);

    /// <summary>检查状态是否允许离开。</summary>
    public bool CanExit(TContext context) => canExit == null || canExit(context);
}
