using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对象池管理器（单例）
/// 职责：复用频繁创建销毁的对象（子弹/特效/伤害数字/掉落物），减少 GC 压力
/// </summary>
public class PoolManager : MonoBehaviour
{
    // 池字典：Dictionary<string, Queue<GameObject>> poolDict
    // 预设预加载：PreloadPool(GameObject prefab, int count)
    // 从池中获取：Get(GameObject prefab, Vector3 position, Quaternion rotation)
    // 回收到池中：Release(GameObject obj)
    // 清空某个池 / 清空所有池
    // 根节点（整理场景层级用）
}
