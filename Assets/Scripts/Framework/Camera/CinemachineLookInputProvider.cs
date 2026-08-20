using Cinemachine;
using UnityEngine;

/// <summary>
/// Lives on the FreeLook camera so Cinemachine can discover the Input System
/// axis provider whenever the camera is enabled or its rigs are rebuilt.
/// </summary>
[DisallowMultipleComponent]
public sealed class CinemachineLookInputProvider : MonoBehaviour
{
    // 提供 Input System 视角轴数据的单相机控制器。
    [SerializeField] private CameraModeController source;

    // 为运行时创建或重建的相机绑定视角输入来源。
    public void Initialize(CameraModeController sourceController)
    {
        source = sourceController;
    }

}
