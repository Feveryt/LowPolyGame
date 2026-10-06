using UnityEngine;

/// <summary>
/// 一次性粒子播放助手：把项目里常驻循环的 FX 预制体临时转为一次性爆发播放。
/// 项目 FX 目录的粒子全部是 looping 环境式配置，直接实例化会持续喷射；
/// 本助手在实例上关闭循环、清空持续发射率并注入一次 Burst，播完自动销毁。
/// 支持 startColor 染色，用于把同一预制体区分黄/红等语义色。
/// </summary>
public static class OneShotParticle
{
    /// <summary>
    /// 在指定位置播放一次粒子特效。
    /// tint 非空时覆盖所有子粒子系统的 startColor。
    /// convertToBurst=true 时把常驻循环粒子转成单次爆发；false 则保留持续喷射（适合火息等流状特效），
    /// 播放 duration 秒后销毁。对原本非循环的预制体不改动发射配置。
    /// </summary>
    public static GameObject Play(
        GameObject prefab,
        Vector3 position,
        float scale = 1f,
        float duration = 2f,
        Color? tint = null,
        Quaternion? rotation = null,
        bool convertToBurst = true)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(
            prefab,
            position,
            rotation ?? Quaternion.identity);
        instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            bool wasLooping = main.loop;
            main.loop = false;

            if (tint.HasValue)
                main.startColor = tint.Value;

            if (wasLooping && convertToBurst)
            {
                // 常驻式粒子转为单次爆发：清空持续发射率，按最大粒子数注入初始 Burst。
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0f;
                short burstCount = (short)Mathf.Clamp(main.maxParticles, 1, 60);
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });
            }

            particles.Play(true);
        }

        Object.Destroy(instance, duration);
        return instance;
    }
}
