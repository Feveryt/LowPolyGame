using UnityEngine;

/// <summary>
/// 音效/音乐管理器（单例）
/// 职责：BGM 播放/切换/淡入淡出，音效播放（2D/3D），音量控制，音频资源管理
/// </summary>
public class AudioManager : MonoBehaviour
{
    // AudioSource 池（至少一个 BGM 源 + 多个 SFX 源）
    // BGM 音量 / SFX 音量 / 主音量（可存档）
    // 播放 BGM：PlayBGM(AudioClip clip, float fadeTime)
    // 播放 2D 音效：PlaySFX(AudioClip clip, float volume)
    // 播放 3D 音效（跟随某个位置）：PlaySFXAtPoint(AudioClip clip, Vector3 position)
    // 停止 BGM / 暂停所有音效 / 恢复
    // 静音开关
}
