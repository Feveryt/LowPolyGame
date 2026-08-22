using UnityEngine;

/// <summary>
/// 敌人刷怪管理器
/// 职责：按波次/区域刷怪，控制刷新节奏，BOSS 房间触发
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // 刷怪点 Transform 数组
    // 波次配置：WaveConfig（每波敌人类型、数量、刷新间隔）
    // 当前波次 / 总波次
    // 开始刷怪：StartWave(int waveIndex)
    // 单个刷怪：SpawnEnemy(int enemyId, Vector3 position)
    // 区域触发（玩家进入区域时刷怪）
    // 所有敌人死亡时触发事件（开门/奖励等）
    // 刷怪上限（防止场景敌人过多）
}
