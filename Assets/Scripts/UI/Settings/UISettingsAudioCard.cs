using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "声音"卡片的运行时状态组件：订阅全局音量服务，
/// 静音时统一把音效/音乐两行调暗并禁止拖动，让"全部静音"开关的效果在界面上立即可见。
/// </summary>
public sealed class UISettingsAudioCard : MonoBehaviour
{
    // 静音时音量行的整体不透明度。
    private const float MutedRowAlpha = 0.45f;

    // 音量行的滑条与行节点（行节点上的 CanvasGroup 用于整体调暗）。
    private readonly List<Slider> volumeSliders = new List<Slider>();
    private readonly List<CanvasGroup> volumeRowGroups = new List<CanvasGroup>();
    // 静音开关引用，用于外部状态变化时回写勾选状态。
    private Toggle muteToggle;

    /// <summary>注册一条音量行，供静音联动调暗与禁用。</summary>
    public void RegisterVolumeRow(Slider slider)
    {
        if (slider == null)
            return;

        volumeSliders.Add(slider);

        Transform rowTransform = slider.transform.parent;
        if (rowTransform == null)
            return;

        CanvasGroup group = rowTransform.GetComponent<CanvasGroup>();
        if (group == null)
            group = rowTransform.gameObject.AddComponent<CanvasGroup>();
        volumeRowGroups.Add(group);
    }

    /// <summary>注册静音开关，用于外部状态变化时回写勾选状态。</summary>
    public void RegisterToggle(Toggle toggle)
    {
        muteToggle = toggle;
    }

    // 面板显示时订阅音量变化。
    private void OnEnable()
    {
        AudioVolumeSettings.Changed += Refresh;
        Refresh();
    }

    // 面板隐藏或销毁时取消订阅，避免静态事件持有已失效对象。
    private void OnDisable()
    {
        AudioVolumeSettings.Changed -= Refresh;
    }

    /// <summary>依据当前静音状态刷新勾选框与音量行的可交互性。</summary>
    public void Refresh()
    {
        bool muted = AudioVolumeSettings.Muted;

        // 静音状态也可能由其他入口改变，这里回写但不触发回调，避免递归。
        if (muteToggle != null && muteToggle.isOn != muted)
            muteToggle.SetIsOnWithoutNotify(muted);

        for (int i = 0; i < volumeSliders.Count; i++)
        {
            Slider slider = volumeSliders[i];
            if (slider != null)
                slider.interactable = !muted;
        }

        for (int i = 0; i < volumeRowGroups.Count; i++)
        {
            CanvasGroup group = volumeRowGroups[i];
            if (group != null)
                group.alpha = muted ? MutedRowAlpha : 1f;
        }
    }
}
