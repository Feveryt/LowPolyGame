using Cinemachine;
using UnityEngine;

/// <summary>
/// Lives on the FreeLook camera so Cinemachine can discover the Input System
/// axis provider whenever the camera is enabled or its rigs are rebuilt.
/// </summary>
[DisallowMultipleComponent]
public sealed class CinemachineLookInputProvider : MonoBehaviour
{
    [SerializeField] private CameraModeController source;

    public void Initialize(CameraModeController sourceController)
    {
        source = sourceController;
    }

}
