using UnityEngine;

/// <summary>
/// 相机管理器
/// 职责：管理 Cinemachine 相机状态切换（自由视角/锁定视角/过场/震动）
/// </summary>
public class CameraManager : MonoBehaviour
{
    // Cinemachine FreeLook 引用
    // Cinemachine Virtual Camera 组引用（锁定目标相机、过场相机）
    // 切换相机：SwitchCamera(string cameraName, float blendTime)
    // 锁定目标：LockOnTarget(Transform target)
    // 取消锁定：CancelLockOn()
    // 相机震动：Shake(float amplitude, float frequency, float duration)
    // 灵敏度调整（跟随设置）
    // 镜头碰撞检测（避免穿墙/穿地）
}
