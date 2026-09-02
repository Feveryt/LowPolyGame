using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>镜头序列的运行时工作模式。</summary>
public enum CameraSequenceType
{
    /// <summary>按 Scene 视图记录的世界空间关键帧播放。</summary>
    StaticSequence,
    /// <summary>将玩家与当前交互对象持续保持在同一画面中。</summary>
    DialogueTwoShot,
}

/// <summary>
/// 可复用的镜头调度资产。
/// 静态序列保存场景关键帧；对话序列保存双人构图参数，具体角色由运行时注入。
/// </summary>
[CreateAssetMenu(menuName = "Low Poly Game/Camera/Camera Sequence", fileName = "CameraSequence_")]
public sealed class CameraSequenceAsset : ScriptableObject
{
    // 本序列采用的镜头行为。
    [SerializeField] private CameraSequenceType sequenceType = CameraSequenceType.StaticSequence;
    // 从当前玩家镜头切入本序列的混合时长，单位秒。
    [SerializeField, Min(0f)] private float blendInDuration = 0.6f;
    // 本序列结束后回到玩家镜头的混合时长，单位秒。
    [SerializeField, Min(0f)] private float blendOutDuration = 0.5f;
    // 静态序列是否在最后一帧停留后自动结束。
    [SerializeField] private bool autoComplete = true;
    // 最后一帧额外停留时长，单位秒。
    [SerializeField, Min(0f)] private float endHoldDuration = 0.2f;
    // 由录制工具按时间升序写入的镜头关键帧。
    [SerializeField] private List<CameraShotKeyframe> keyframes = new List<CameraShotKeyframe>();

    [Header("Dialogue Two Shot")]
    // 双人镜头相对目标组中心的位置偏移，采用世界坐标方向。
    [SerializeField] private Vector3 dialogueCameraOffset = new Vector3(4f, 2.2f, -5f);
    // 双人模型在画面中期望占据的比例。
    [SerializeField, Range(0.1f, 0.95f)] private float dialogueFramingSize = 0.72f;
    // 双人构图的基础视角，单位度。
    [SerializeField, Range(1f, 179f)] private float dialogueFieldOfView = 42f;
    // 对话对象的构图半径，近似表示角色身体占据范围。
    [SerializeField, Min(0f)] private float dialogueTargetRadius = 0.8f;

    /// <summary>镜头播放模式。</summary>
    public CameraSequenceType SequenceType => sequenceType;
    /// <summary>切入本镜头的默认混合时长。</summary>
    public float BlendInDuration => blendInDuration;
    /// <summary>退出本镜头的默认混合时长。</summary>
    public float BlendOutDuration => blendOutDuration;
    /// <summary>静态序列是否在播放完成后自行归还镜头。</summary>
    public bool AutoComplete => autoComplete;
    /// <summary>静态序列末帧的额外停留时间。</summary>
    public float EndHoldDuration => endHoldDuration;
    /// <summary>按时间升序保存的镜头关键帧。</summary>
    public IReadOnlyList<CameraShotKeyframe> Keyframes => keyframes;
    /// <summary>双人镜头相对目标中心的世界偏移。</summary>
    public Vector3 DialogueCameraOffset => dialogueCameraOffset;
    /// <summary>双人镜头的画面占比。</summary>
    public float DialogueFramingSize => dialogueFramingSize;
    /// <summary>双人镜头的基础视角。</summary>
    public float DialogueFieldOfView => dialogueFieldOfView;
    /// <summary>双人构图使用的角色半径。</summary>
    public float DialogueTargetRadius => dialogueTargetRadius;

    /// <summary>返回静态序列的总播放时间，不包含切入和切出混合。</summary>
    public float GetStaticDuration()
    {
        if (keyframes == null || keyframes.Count == 0)
            return 0f;

        return Mathf.Max(0f, keyframes[keyframes.Count - 1].Time) + endHoldDuration;
    }

#if UNITY_EDITOR
    /// <summary>由录制窗口追加一个已采样的场景视图关键帧。</summary>
    public void AddKeyframe(CameraShotKeyframe keyframe)
    {
        keyframes.Add(keyframe);
        keyframes.Sort((left, right) => left.Time.CompareTo(right.Time));
    }

    /// <summary>由录制窗口删除指定下标的关键帧。</summary>
    public void RemoveKeyframeAt(int index)
    {
        if (index >= 0 && index < keyframes.Count)
            keyframes.RemoveAt(index);
    }
#endif
}

/// <summary>静态镜头在某个时间点的位姿和镜头参数。</summary>
[Serializable]
public struct CameraShotKeyframe
{
    // 从序列开始计算的关键帧时间，单位秒。
    [SerializeField, Min(0f)] private float time;
    // 摄像机世界坐标。
    [SerializeField] private Vector3 position;
    // 摄像机世界空间欧拉角。
    [SerializeField] private Vector3 rotationEuler;
    // 透视摄像机的垂直视角，单位度。
    [SerializeField, Range(1f, 179f)] private float fieldOfView;
    // 移动到本关键帧时使用的插值曲线。
    [SerializeField] private AnimationCurve easing;

    /// <summary>关键帧时间。</summary>
    public float Time => time;
    /// <summary>世界空间位置。</summary>
    public Vector3 Position => position;
    /// <summary>世界空间旋转。</summary>
    public Quaternion Rotation => Quaternion.Euler(rotationEuler);
    /// <summary>垂直视角。</summary>
    public float FieldOfView => fieldOfView;
    /// <summary>从上一关键帧到本帧的插值曲线。</summary>
    public AnimationCurve Easing => easing;

    /// <summary>使用 Scene 视图相机状态创建一条关键帧。</summary>
    public CameraShotKeyframe(float keyframeTime, Vector3 cameraPosition, Quaternion cameraRotation, float cameraFieldOfView)
    {
        time = Mathf.Max(0f, keyframeTime);
        position = cameraPosition;
        rotationEuler = cameraRotation.eulerAngles;
        fieldOfView = Mathf.Clamp(cameraFieldOfView, 1f, 179f);
        easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    /// <summary>计算从本关键帧前往下一帧的平滑插值比例。</summary>
    public float EvaluateEasing(float normalizedTime)
    {
        return easing == null ? normalizedTime : Mathf.Clamp01(easing.Evaluate(normalizedTime));
    }
}
