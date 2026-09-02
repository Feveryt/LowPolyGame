using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 运行时镜头调度入口。
/// 通过高优先级虚拟相机临时接管现有 FreeLook，负责静态关键帧和对话双人构图的播放与回退。
/// </summary>
[DefaultExecutionOrder(-90)]
public sealed class CameraDirector : MonoBehaviour
{
    // 场景中所有玩家镜头之上的调度镜头优先级。
    private const int DirectorPriority = 100;
    // 常驻跨场景调度器实例。
    private static CameraDirector instance;

    // 调度镜头的虚拟相机。
    private CinemachineVirtualCamera virtualCamera;
    // 对话双方使用的动态构图目标组。
    private CinemachineTargetGroup dialogueTargetGroup;
    // 虚拟相机的目标组构图组件。
    private CinemachineGroupComposer groupComposer;
    // 当前正在播放的镜头资产。
    private CameraSequenceAsset activeSequence;
    // 当前序列的第一个动态目标，通常为玩家。
    private Transform primaryTarget;
    // 当前序列的第二个动态目标，通常为 NPC。
    private Transform secondaryTarget;
    // 静态序列已播放的时间。
    private float sequenceElapsed;
    // 静态序列播放期间是否已锁定玩家视角输入。
    private bool lookInputWasEnabled;
    // 当前被暂时锁定视角输入的玩家输入组件。
    private InputManager inputManager;
    // 首次接管前主相机 Brain 的默认混合配置。
    private CinemachineBlendDefinition originalBlend;
    // 是否已缓存过主相机原始混合配置。
    private bool hasCapturedOriginalBlend;
    // 等待回切完成后恢复 Brain 配置的协程。
    private Coroutine restoreBlendRoutine;

    /// <summary>当前运行时唯一的镜头调度器。</summary>
    public static CameraDirector Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject root = new GameObject(nameof(CameraDirector));
                instance = root.AddComponent<CameraDirector>();
            }

            return instance;
        }
    }

    /// <summary>当前是否正由调度器接管主相机。</summary>
    public bool IsPlaying => activeSequence != null;

    // 建立单例并让调度器随场景切换保留。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureCameraRig();
    }

    // 场景切换或延迟生成主相机后补齐 Cinemachine 运行时组件。
    private void Update()
    {
        EnsureCameraRig();

        if (activeSequence == null || activeSequence.SequenceType != CameraSequenceType.StaticSequence)
            return;

        sequenceElapsed += Time.unscaledDeltaTime;
        ApplyStaticSequence(activeSequence, sequenceElapsed);
        if (activeSequence.AutoComplete && sequenceElapsed >= activeSequence.GetStaticDuration())
            Stop();
    }

    // 销毁时释放输入锁定和单例引用。
    private void OnDestroy()
    {
        RestoreBrainBlendImmediately();
        RestoreLookInput();
        if (instance == this)
            instance = null;
    }

    /// <summary>播放静态场景镜头或对话双人镜头；新播放会安全打断旧播放。</summary>
    public bool Play(CameraSequenceAsset sequence, Transform primary = null, Transform secondary = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning($"[{nameof(CameraDirector)}] Cannot play a null sequence.", this);
            return false;
        }

        EnsureCameraRig();
        if (virtualCamera == null)
        {
            Debug.LogWarning($"[{nameof(CameraDirector)}] A Main Camera is required before a sequence can play.", this);
            return false;
        }

        Stop();
        CancelBlendRestore();
        CaptureOriginalBrainBlend();
        activeSequence = sequence;
        primaryTarget = primary != null ? primary : FindFirstObjectByType<PlayerController>()?.transform;
        secondaryTarget = secondary;
        sequenceElapsed = 0f;
        LockLookInput();

        ConfigureBrainBlend(sequence.BlendInDuration);
        virtualCamera.gameObject.SetActive(true);
        virtualCamera.Priority = DirectorPriority;

        if (sequence.SequenceType == CameraSequenceType.StaticSequence)
        {
            if (sequence.Keyframes.Count == 0)
            {
                Debug.LogWarning($"[{nameof(CameraDirector)}] Static sequence '{sequence.name}' has no keyframes.", sequence);
                Stop();
                return false;
            }

            ConfigureStaticCamera();
            ApplyStaticSequence(sequence, 0f);
            return true;
        }

        if (primaryTarget == null || secondaryTarget == null)
        {
            Debug.LogWarning($"[{nameof(CameraDirector)}] Dialogue sequence '{sequence.name}' requires both player and NPC targets.", sequence);
            Stop();
            return false;
        }

        ConfigureDialogueCamera(sequence);
        return true;
    }

    /// <summary>归还主相机控制权并恢复播放前的视角输入状态。</summary>
    public void Stop()
    {
        if (activeSequence == null && virtualCamera == null)
            return;

        float blendOut = activeSequence != null ? activeSequence.BlendOutDuration : 0f;
        ConfigureBrainBlend(blendOut);
        activeSequence = null;
        primaryTarget = null;
        secondaryTarget = null;
        sequenceElapsed = 0f;

        if (virtualCamera != null)
        {
            virtualCamera.Priority = 0;
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
        }

        if (dialogueTargetGroup != null)
        {
            dialogueTargetGroup.m_Targets = System.Array.Empty<CinemachineTargetGroup.Target>();
            dialogueTargetGroup.gameObject.SetActive(false);
        }

        RestoreLookInput();
        ScheduleBlendRestore(blendOut);
    }

    // 创建或重新寻找主相机对应的 Cinemachine Brain 与调度镜头。
    private void EnsureCameraRig()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

        if (virtualCamera != null)
            return;

        GameObject rigRoot = new GameObject("Camera Director Rig");
        rigRoot.transform.SetParent(transform, false);
        virtualCamera = rigRoot.AddComponent<CinemachineVirtualCamera>();
        virtualCamera.Priority = 0;
        virtualCamera.gameObject.SetActive(false);

        dialogueTargetGroup = new GameObject("Dialogue Target Group").AddComponent<CinemachineTargetGroup>();
        dialogueTargetGroup.transform.SetParent(transform, false);
        dialogueTargetGroup.gameObject.SetActive(false);
    }

    // 以当前序列的混合时长更新主相机 Brain 的默认混合设置。
    private void ConfigureBrainBlend(float duration)
    {
        Camera mainCamera = Camera.main;
        CinemachineBrain brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, duration);
    }

    // 在首次调度前缓存主相机原有的混合设置，避免演出改变其他镜头行为。
    private void CaptureOriginalBrainBlend()
    {
        if (hasCapturedOriginalBlend)
            return;

        Camera mainCamera = Camera.main;
        CinemachineBrain brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (brain == null)
            return;

        originalBlend = brain.m_DefaultBlend;
        hasCapturedOriginalBlend = true;
    }

    // 在回切混合结束后恢复主相机原有的默认混合配置。
    private void ScheduleBlendRestore(float duration)
    {
        CancelBlendRestore();
        if (!hasCapturedOriginalBlend)
            return;

        restoreBlendRoutine = StartCoroutine(RestoreBlendAfterDelay(duration));
    }

    // 等待未缩放时间，确保暂停状态下也能恢复相机配置。
    private IEnumerator RestoreBlendAfterDelay(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        RestoreBrainBlendImmediately();
        restoreBlendRoutine = null;
    }

    // 取消尚未完成的回切恢复任务。
    private void CancelBlendRestore()
    {
        if (restoreBlendRoutine == null)
            return;

        StopCoroutine(restoreBlendRoutine);
        restoreBlendRoutine = null;
    }

    // 立即还原主相机 Brain 的默认混合设置。
    private void RestoreBrainBlendImmediately()
    {
        if (!hasCapturedOriginalBlend)
            return;

        Camera mainCamera = Camera.main;
        CinemachineBrain brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
            brain.m_DefaultBlend = originalBlend;

        hasCapturedOriginalBlend = false;
    }

    // 配置一个不跟随目标、完全由关键帧控制的镜头管线。
    private void ConfigureStaticCamera()
    {
        virtualCamera.Follow = null;
        virtualCamera.LookAt = null;
        virtualCamera.DestroyCinemachineComponent<CinemachineTransposer>();
        virtualCamera.DestroyCinemachineComponent<CinemachineComposer>();
        virtualCamera.DestroyCinemachineComponent<CinemachineGroupComposer>();
        groupComposer = null;
    }

    // 根据时间在线性区段中插值静态关键帧并写入虚拟相机状态。
    private void ApplyStaticSequence(CameraSequenceAsset sequence, float time)
    {
        int count = sequence.Keyframes.Count;
        CameraShotKeyframe first = sequence.Keyframes[0];
        CameraShotKeyframe last = sequence.Keyframes[count - 1];

        if (time <= first.Time || count == 1)
        {
            SetStaticCameraState(first.Position, first.Rotation, first.FieldOfView);
            return;
        }

        if (time >= last.Time)
        {
            SetStaticCameraState(last.Position, last.Rotation, last.FieldOfView);
            return;
        }

        for (int index = 0; index < count - 1; index++)
        {
            CameraShotKeyframe from = sequence.Keyframes[index];
            CameraShotKeyframe to = sequence.Keyframes[index + 1];
            if (time < from.Time || time > to.Time)
                continue;

            float duration = Mathf.Max(0.0001f, to.Time - from.Time);
            float progress = to.EvaluateEasing((time - from.Time) / duration);
            SetStaticCameraState(
                Vector3.LerpUnclamped(from.Position, to.Position, progress),
                Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, progress),
                Mathf.LerpUnclamped(from.FieldOfView, to.FieldOfView, progress));
            return;
        }
    }

    // 将世界空间位姿与透视参数直接写入静态虚拟相机。
    private void SetStaticCameraState(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        virtualCamera.transform.SetPositionAndRotation(position, rotation);
        LensSettings lens = virtualCamera.m_Lens;
        lens.FieldOfView = fieldOfView;
        virtualCamera.m_Lens = lens;
    }

    // 使用 TargetGroup 和 GroupComposer 配置可复用的玩家-NPC 双人构图。
    private void ConfigureDialogueCamera(CameraSequenceAsset sequence)
    {
        dialogueTargetGroup.gameObject.SetActive(true);
        dialogueTargetGroup.m_Targets = new[]
        {
            new CinemachineTargetGroup.Target { target = primaryTarget, weight = 1f, radius = sequence.DialogueTargetRadius },
            new CinemachineTargetGroup.Target { target = secondaryTarget, weight = 1f, radius = sequence.DialogueTargetRadius },
        };

        virtualCamera.Follow = dialogueTargetGroup.transform;
        virtualCamera.LookAt = dialogueTargetGroup.transform;
        CinemachineTransposer transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
            transposer = virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        transposer.m_FollowOffset = sequence.DialogueCameraOffset;
        transposer.m_XDamping = 0.5f;
        transposer.m_YDamping = 0.5f;
        transposer.m_ZDamping = 0.5f;

        groupComposer = virtualCamera.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer == null)
            groupComposer = virtualCamera.AddCinemachineComponent<CinemachineGroupComposer>();
        groupComposer.m_GroupFramingSize = sequence.DialogueFramingSize;
        groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.ZoomOnly;
        groupComposer.m_FrameDamping = 0.5f;
        groupComposer.m_MinimumFOV = 25f;
        groupComposer.m_MaximumFOV = 70f;

        LensSettings lens = virtualCamera.m_Lens;
        lens.FieldOfView = sequence.DialogueFieldOfView;
        virtualCamera.m_Lens = lens;
    }

    // 记录并锁定当前玩家的视角输入，避免演出期间 FreeLook 继续被用户驱动。
    private void LockLookInput()
    {
        inputManager = FindFirstObjectByType<InputManager>();
        if (inputManager == null)
            return;

        lookInputWasEnabled = inputManager.LookInputEnabled;
        inputManager.SetLookInputEnabled(false);
    }

    // 仅在本调度器曾锁定输入时恢复原始视角输入状态。
    private void RestoreLookInput()
    {
        if (inputManager != null)
            inputManager.SetLookInputEnabled(lookInputWasEnabled);

        inputManager = null;
    }
}
