using System;
using UnityEngine;

/// <summary>
/// 全局音量设置服务，集中保存音效与背景音乐两条通道的音量并持久化到 PlayerPrefs。
/// 设置界面只需调用 SetSfxVolume / SetBgmVolume，音频系统通过 Changed 事件实时响应。
/// </summary>
public static class AudioVolumeSettings
{
    /// <summary>音效通道音量的持久化键。</summary>
    public const string SfxVolumeKey = "Audio.SfxVolume";
    /// <summary>背景音乐通道音量的持久化键。</summary>
    public const string BgmVolumeKey = "Audio.BgmVolume";
    /// <summary>未保存过设置时使用的音效默认音量，保持既有音效响度不变。</summary>
    public const float DefaultSfxVolume = 1f;
    /// <summary>未保存过设置时使用的音乐默认音量，代表曲目配置中的设计音量。</summary>
    public const float DefaultBgmVolume = 1f;

    // 是否已经从 PlayerPrefs 读取过数值。
    private static bool loaded;
    // 两条通道的当前音量，取值范围 0 到 1。
    private static float sfxVolume = DefaultSfxVolume;
    private static float bgmVolume = DefaultBgmVolume;

    /// <summary>任意一条通道音量变化后触发，监听方自行读取需要的通道。</summary>
    public static event Action Changed;

    /// <summary>音效通道音量，取值 0 到 1。</summary>
    public static float SfxVolume
    {
        get
        {
            EnsureLoaded();
            return sfxVolume;
        }
    }

    /// <summary>背景音乐通道音量，取值 0 到 1。</summary>
    public static float BgmVolume
    {
        get
        {
            EnsureLoaded();
            return bgmVolume;
        }
    }

    /// <summary>设置音效通道音量，自动钳制到 0 到 1 并写入 PlayerPrefs。</summary>
    public static void SetSfxVolume(float value)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, sfxVolume))
            return;

        sfxVolume = clamped;
        PlayerPrefs.SetFloat(SfxVolumeKey, clamped);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>设置背景音乐通道音量，自动钳制到 0 到 1 并写入 PlayerPrefs。</summary>
    public static void SetBgmVolume(float value)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, bgmVolume))
            return;

        bgmVolume = clamped;
        PlayerPrefs.SetFloat(BgmVolumeKey, clamped);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>恢复两条通道的默认音量，并清除已保存的设置。</summary>
    public static void ResetToDefaults()
    {
        EnsureLoaded();
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.DeleteKey(BgmVolumeKey);
        PlayerPrefs.Save();
        sfxVolume = DefaultSfxVolume;
        bgmVolume = DefaultBgmVolume;
        Changed?.Invoke();
    }

    /// <summary>丢弃内存缓存并重新从 PlayerPrefs 读取，供放弃修改或测试重置使用。</summary>
    public static void Reload()
    {
        loaded = false;
        EnsureLoaded();
        Changed?.Invoke();
    }

    // 首次访问时读取持久化数值，保证读写共用同一份缓存。
    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));
    }
}
