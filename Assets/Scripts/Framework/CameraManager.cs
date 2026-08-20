using UnityEngine;

/// <summary>
/// 相机管理器
/// 职责：预留统一的 Cinemachine 相机构图、锁定、过场和震动调度入口
/// </summary>
public class CameraManager : MonoBehaviour
{
    // Cinemachine FreeLook 引用
    // 单一 Cinemachine FreeLook 引用（锁定时仅改变构图，不切换战斗相机）
    // 过场虚拟相机引用（仅过场需要时临时接管画面）
    // 锁定目标：LockOnTarget(Transform target)
    // 取消锁定：CancelLockOn()
    // 相机震动：Shake(float amplitude, float frequency, float duration)
    // 灵敏度调整（跟随设置）
    // 镜头碰撞检测（避免穿墙/穿地）
}
