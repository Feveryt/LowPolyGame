using UnityEngine;

/// <summary>
/// 场景中播放镜头序列的轻量触发器。
/// 可用于新场景入口、区域触发器或由任务/事件直接调用的对象。
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraSequenceTrigger : MonoBehaviour
{
    // 进入本区域或启用对象时播放的镜头资产。
    [SerializeField] private CameraSequenceAsset sequence;
    // 是否在对象启用后自动播放一次。
    [SerializeField] private bool playOnEnable;
    // 是否在玩家首次进入触发器时播放。
    [SerializeField] private bool playOnPlayerEnter = true;
    // 是否只能成功播放一次。
    [SerializeField] private bool playOnce = true;
    // 触发器已经成功启动播放的次数。
    private int playCount;

    /// <summary>由任务、事件或 UnityEvent 调用以播放配置镜头。</summary>
    public void Play()
    {
        if (sequence == null || (playOnce && playCount > 0))
            return;

        if (CameraDirector.Instance.Play(sequence))
            playCount++;
    }

    // 对象启用时执行可选的入口镜头播放。
    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    // 玩家进入 Trigger Collider 时执行可选的区域镜头播放。
    private void OnTriggerEnter(Collider other)
    {
        if (!playOnPlayerEnter || other.GetComponentInParent<PlayerController>() == null)
            return;

        Play();
    }
}
