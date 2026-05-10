using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件系统
/// 职责：解耦模块间通信，通过字符串/枚举事件名 + 委托来广播和监听事件
/// 典型事件：OnEnemyKilled, OnPlayerDamaged, OnItemPickup, OnLevelUp, OnSceneLoaded, OnGamePaused
/// </summary>
public class EventManager : MonoBehaviour
{
    // 事件字典：Dictionary<string, Action<object[]>> eventDict
    // 注册监听：AddListener(string eventName, Action<object[]> callback)
    // 移除监听：RemoveListener(string eventName, Action<object[]> callback)
    // 触发事件：TriggerEvent(string eventName, params object[] args)
    // 清空所有事件（场景切换时调用）

    // ---- 常用事件名称常量（用 const string 统一管理，避免拼写错误）----
    // ENEMY_KILLED, PLAYER_DAMAGED, PLAYER_HEAL, ITEM_PICKUP
    // LEVEL_UP, SKILL_UNLOCKED, SCENE_LOADED, GAME_PAUSED, GAME_RESUMED
    // QUEST_PROGRESS, DIALOG_START, DIALOG_END, BOSS_DEFEATED
}
