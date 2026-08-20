using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件系统（字符串事件兼容层）
///
/// 职责：解耦模块间通信，通过字符串/枚举事件名 + 委托来广播和监听事件。
///
/// ?? 架构演进说明：
/// 本类保留字符串事件 API 以兼容旧代码，但【新代码推荐使用 QFramework 类型事件】：
///   - 发送：this.SendEvent(new OnEnemyKilled(enemyId));
///   - 监听：this.RegisterEvent<OnEnemyKilled>(e => ...).UnRegisterWhenGameObjectDestroyed(gameObject);
/// 类型事件的好处：编译期类型安全、无需维护字符串常量、IDE 可跳转。
///
/// 典型事件：OnEnemyKilled, OnPlayerDamaged, OnItemPickup, OnLevelUp, OnSceneLoaded, OnGamePaused
/// </summary>
public class EventManager : MonoBehaviour
{
    // 全局事件管理器的懒加载单例实例。
    private static EventManager mInstance;

    /// <summary>单例（懒加载：首次访问时自动创建常驻对象）</summary>
    public static EventManager Instance
    {
        get
        {
            if (mInstance == null)
            {
                var go = new GameObject(nameof(EventManager));
                mInstance = go.AddComponent<EventManager>();
                DontDestroyOnLoad(go);
            }

            return mInstance;
        }
    }

    /// <summary>事件字典：事件名 → 回调列表</summary>
    private readonly Dictionary<string, Action<object[]>> mEventDict = new();

    /// <summary>注册监听</summary>
    public void AddListener(string eventName, Action<object[]> callback)
    {
        if (mEventDict.TryGetValue(eventName, out var actions))
        {
            actions += callback;
            mEventDict[eventName] = actions;
        }
        else
        {
            mEventDict[eventName] = callback;
        }
    }

    /// <summary>移除监听</summary>
    public void RemoveListener(string eventName, Action<object[]> callback)
    {
        if (!mEventDict.TryGetValue(eventName, out var actions)) return;

        actions -= callback;

        if (actions == null)
        {
            mEventDict.Remove(eventName);
        }
        else
        {
            mEventDict[eventName] = actions;
        }
    }

    /// <summary>触发事件（注意：回调中抛异常不影响其他监听者）</summary>
    public void TriggerEvent(string eventName, params object[] args)
    {
        if (!mEventDict.TryGetValue(eventName, out var actions)) return;

        foreach (Action<object[]> handler in actions.GetInvocationList())
        {
            try
            {
                handler?.Invoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventManager] 事件 {eventName} 的监听者执行异常: {e}");
            }
        }
    }

    /// <summary>清空所有事件（场景切换时调用）</summary>
    public void ClearAll() => mEventDict.Clear();

    // 销毁时清理监听列表并释放静态实例引用。
    private void OnDestroy()
    {
        mEventDict.Clear();
        mInstance = null;
    }
}
