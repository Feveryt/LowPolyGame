using System;
using System.Collections.Generic;

/// <summary>
/// 不依赖 Unity 的轻量通用有限状态机，负责状态生命周期和条件转移。
/// </summary>
public sealed class StateMachine<TContext, TState>
{
    private const int MaxTransitionsPerTick = 8;
    private readonly Dictionary<TState, IState<TContext>> states = new Dictionary<TState, IState<TContext>>();
    private readonly Dictionary<TState, List<StateTransition<TContext, TState>>> transitions = new Dictionary<TState, List<StateTransition<TContext, TState>>>();
    private readonly List<StateTransition<TContext, TState>> anyTransitions = new List<StateTransition<TContext, TState>>();
    private TState startState;
    private bool hasStartState;

    /// <summary>当前活动状态标识。</summary>
    public TState CurrentState { get; private set; }

    /// <summary>状态机是否已经完成初始化。</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>注册一个状态，重复注册会抛出参数错误。</summary>
    public void AddState(TState id, IState<TContext> state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (states.ContainsKey(id))
            throw new InvalidOperationException($"State '{id}' has already been registered.");

        states.Add(id, state);
        transitions[id] = new List<StateTransition<TContext, TState>>();
    }

    /// <summary>设置初始化时进入的起始状态。</summary>
    public void SetStartState(TState state)
    {
        EnsureStateExists(state);
        startState = state;
        hasStartState = true;
    }

    /// <summary>注册一条普通状态转移规则。</summary>
    public void AddTransition(TState from, TState to, Func<TContext, bool> condition, int priority = 0, bool force = false)
    {
        EnsureStateExists(from);
        EnsureStateExists(to);
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        InsertTransitionByPriority(
            transitions[from],
            new StateTransition<TContext, TState>(to, condition, force, priority));
    }

    /// <summary>注册一条可从任意状态触发的全局转移规则。</summary>
    public void AddAnyTransition(TState to, Func<TContext, bool> condition, bool force = false, int priority = 0)
    {
        EnsureStateExists(to);
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        InsertTransitionByPriority(
            anyTransitions,
            new StateTransition<TContext, TState>(to, condition, force, priority));
    }

    /// <summary>进入起始状态并启用状态机。</summary>
    public void Initialize(TContext context)
    {
        if (IsInitialized)
            return;
        if (!hasStartState)
            throw new InvalidOperationException("A start state has not been configured.");

        CurrentState = startState;
        IsInitialized = true;
        states[CurrentState].OnEnter(context);
    }

    /// <summary>执行当前状态逻辑，并处理本帧满足条件的转移。</summary>
    public void Tick(TContext context)
    {
        if (!IsInitialized)
            return;

        states[CurrentState].OnLogic(context);
        for (int transitionCount = 0; transitionCount < MaxTransitionsPerTick; transitionCount++)
        {
            StateTransition<TContext, TState> transition = FindTransition(context);
            if (transition == null)
                return;
            if (!transition.Force && !states[CurrentState].CanExit(context))
                return;
            if (!TryChangeState(transition.To, context))
                return;
        }
    }

    /// <summary>执行一次状态切换并调用对应的退出和进入回调。</summary>
    public bool TryChangeState(TState state, TContext context)
    {
        if (!IsInitialized || EqualityComparer<TState>.Default.Equals(CurrentState, state))
            return false;
        EnsureStateExists(state);

        states[CurrentState].OnExit(context);
        CurrentState = state;
        states[CurrentState].OnEnter(context);
        return true;
    }

    /// <summary>按全局优先级和当前状态规则查找第一条成立的转移。</summary>
    private StateTransition<TContext, TState> FindTransition(TContext context)
    {
        StateTransition<TContext, TState> any = FindFirstMatchingTransition(anyTransitions, context);
        if (any != null)
            return any;

        return FindFirstMatchingTransition(transitions[CurrentState], context);
    }

    /// <summary>按已排序的顺序查找第一条条件成立的规则，避免逐帧产生 LINQ 分配。</summary>
    private static StateTransition<TContext, TState> FindFirstMatchingTransition(
        List<StateTransition<TContext, TState>> transitionList,
        TContext context)
    {
        for (int index = 0; index < transitionList.Count; index++)
        {
            StateTransition<TContext, TState> transition = transitionList[index];
            if (transition.Condition(context))
                return transition;
        }

        return null;
    }

    /// <summary>按优先级插入规则，同优先级保持注册顺序。</summary>
    private static void InsertTransitionByPriority(
        List<StateTransition<TContext, TState>> list,
        StateTransition<TContext, TState> transition)
    {
        int insertIndex = 0;
        while (insertIndex < list.Count && list[insertIndex].Priority >= transition.Priority)
            insertIndex++;

        list.Insert(insertIndex, transition);
    }

    /// <summary>确认状态已注册，避免运行时出现难定位的字典异常。</summary>
    private void EnsureStateExists(TState state)
    {
        if (!states.ContainsKey(state))
            throw new InvalidOperationException($"State '{state}' has not been registered.");
    }
}
