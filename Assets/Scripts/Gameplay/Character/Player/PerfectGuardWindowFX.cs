using UnityEngine;

/// <summary>
/// 精准防御（弹反）窗口的视觉提示：进入可弹反的时间段时闪出一道蓝色光盾，随后迅速熄灭。
///
/// 表现由三层程序生成的图形 + 一盏蓝色点光组成（全部用 Sprites/Default，不依赖任何美术资源）：
///   - 光盾圆盘：低透明度蓝色底盘，形状可读；
///   - 光盾外环：高亮度蓝环，勾出"盾已经架好"的轮廓；
///   - 扩散脉冲环：快速向外扩张并淡出，强调"这一瞬间"；
///   - 蓝色点光：窗口开启瞬间最亮，然后按平方衰减熄灭。
/// 空闲时整个对象关闭，不产生任何绘制与开销。
/// </summary>
[DisallowMultipleComponent]
public sealed class PerfectGuardWindowFX : MonoBehaviour
{
    // 光盾相对角色原点的抬升高度（约胸口位置）。
    [SerializeField, Min(0f)] private float flashHeight = 1.15f;
    // 光盾相对角色正面的前移距离。
    [SerializeField, Min(0f)] private float forwardOffset = 0.3f;
    // 光盾圆环半径，单位为米。
    [SerializeField, Min(0.05f)] private float shieldRadius = 0.82f;
    // 闪光颜色（蓝）。
    [SerializeField] private Color flashColor = new Color(0.35f, 0.78f, 1f, 1f);
    // 点光峰值强度。
    [SerializeField, Min(0f)] private float lightIntensity = 7f;
    // 点光有效半径。
    [SerializeField, Min(0f)] private float lightRange = 6.5f;
    // 扩散脉冲环的最大放大倍率。
    [SerializeField, Min(1f)] private float pulseScale = 1.75f;

    // 圆盘网格分段数。
    private const int Segments = 48;
    // 外环厚度占半径的比例。
    private const float RingThickness = 0.1f;
    // 各层顶点色透明度：底盘 / 外环 / 脉冲环。
    private const float DiscAlpha = 0.18f;
    private const float RingAlpha = 0.95f;
    private const float PulseAlpha = 0.7f;

    // 静态共享网格。
    private static Mesh discMesh;
    private static Mesh ringMesh;

    // 点光与图形容器。
    private Light flashLight;
    private Transform visuals;
    private Transform pulseLayer;

    // 光盾主体材质（底盘 + 外环）与脉冲环材质，各自独立衰减。
    private Material coreMaterial;
    private Material pulseMaterial;

    // 本次闪光的剩余时间与总时长。
    private float remaining;
    private float duration;

    // 当前是否正在播放闪光。
    private bool IsFlashing => remaining > 0f;

    // 创建一个程序生成的环形网格（XY 平面，法线朝 +Z，便于用 LookRotation 正对角色前方）。
    private static Mesh BuildRingMesh(float innerRadius, float innerAlpha, float outerAlpha, string meshName)
    {
        Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[Segments * 6];

        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.PI * 2f * i / Segments;
            Vector3 direction = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            vertices[i * 2] = direction * innerRadius;
            vertices[i * 2 + 1] = direction;
            colors[i * 2] = new Color(1f, 1f, 1f, innerAlpha);
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, outerAlpha);
        }

        for (int i = 0; i < Segments; i++)
        {
            int innerCurrent = i * 2;
            int outerCurrent = i * 2 + 1;
            int innerNext = (i + 1) * 2;
            int outerNext = (i + 1) * 2 + 1;
            triangles[i * 6] = innerCurrent;
            triangles[i * 6 + 1] = innerNext;
            triangles[i * 6 + 2] = outerCurrent;
            triangles[i * 6 + 3] = outerCurrent;
            triangles[i * 6 + 4] = innerNext;
            triangles[i * 6 + 5] = outerNext;
        }

        Mesh mesh = new Mesh { name = meshName, vertices = vertices, triangles = triangles, colors = colors };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 建立光盾与点光；只在首次闪光时构建。
    private void EnsureVisuals()
    {
        if (coreMaterial != null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        if (discMesh == null)
            discMesh = BuildRingMesh(0f, DiscAlpha * 0.5f, DiscAlpha, "PG_Disc");
        if (ringMesh == null)
            ringMesh = BuildRingMesh(1f - RingThickness, RingAlpha, RingAlpha, "PG_Ring");

        coreMaterial = new Material(shader) { renderQueue = 3000 };
        coreMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        pulseMaterial = new Material(shader) { renderQueue = 3000 };
        pulseMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        GameObject visualsObject = new GameObject("ShieldVisuals");
        visualsObject.transform.SetParent(transform, false);
        visuals = visualsObject.transform;

        CreateLayer(visuals, "Shield Disc", discMesh, coreMaterial, 1f);
        CreateLayer(visuals, "Shield Ring", ringMesh, coreMaterial, 1f);
        Transform pulse = CreateLayer(visuals, "Shield Pulse", ringMesh, pulseMaterial, 1f);
        pulseLayer = pulse;

        GameObject lightObject = new GameObject("PerfectGuardLight");
        lightObject.transform.SetParent(visuals, false);
        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.range = lightRange;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.renderMode = LightRenderMode.ForcePixel;
    }

    // 创建一个贴片的渲染层。
    private Transform CreateLayer(Transform parent, string layerName, Mesh mesh, Material material, float scale)
    {
        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(parent, false);
        layer.transform.localScale = new Vector3(scale, scale, 1f);

        MeshFilter filter = layer.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return layer.transform;
    }

    /// <summary>
    /// 闪一次蓝光：duration 秒内从最亮衰减到熄灭，通常传入精准防御窗口时长。
    /// </summary>
    public void Flash(float flashDuration)
    {
        if (flashDuration <= 0.01f)
            return;

        EnsureVisuals();
        if (coreMaterial == null)
            return;

        duration = flashDuration;
        remaining = flashDuration;

        // 以最亮状态起手，再逐帧衰减。
        if (visuals != null)
            visuals.gameObject.SetActive(true);
        SetAlpha(1f, 1f);
        if (flashLight != null)
            flashLight.intensity = lightIntensity;

        // 熄灭时会关掉自身 Update，这里必须重新打开。
        enabled = true;
        RefreshTransform();
    }

    // 每帧推进衰减、脉冲扩散与位置跟随。
    private void Update()
    {
        if (!IsFlashing)
        {
            // 熄灭后收起所有图形，避免无谓的绘制。
            SetAlpha(0f, 0f);
            if (flashLight != null)
                flashLight.intensity = 0f;
            if (visuals != null)
                visuals.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        remaining -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - remaining / duration);

        // 闪出 → 熄灭：用幂次曲线让开头最亮、随后迅速衰减。
        float coreAlpha = Mathf.Pow(1f - t, 1.6f);
        float pulseAlpha = Mathf.Pow(1f - t, 3f);

        SetAlpha(coreAlpha, pulseAlpha);

        if (visuals != null)
        {
            visuals.gameObject.SetActive(true);
            float pop = Mathf.Lerp(0.88f, 1.12f, Mathf.Sqrt(t));
            visuals.localScale = new Vector3(shieldRadius * pop, shieldRadius * pop, 1f);
        }

        if (pulseLayer != null)
        {
            float pulse = Mathf.Lerp(1f, pulseScale, t);
            pulseLayer.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (flashLight != null)
            flashLight.intensity = lightIntensity * Mathf.Pow(1f - t, 2f);

        RefreshTransform();
    }

    // 同步材质透明度。
    private void SetAlpha(float coreAlpha, float pulseAlpha)
    {
        if (coreMaterial != null)
            coreMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, coreAlpha);
        if (pulseMaterial != null)
            pulseMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, pulseAlpha);
    }

    // 让光盾跟随角色位置并始终朝向角色正前方。
    private void RefreshTransform()
    {
        if (visuals == null)
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        visuals.position = transform.position + Vector3.up * flashHeight + forward * forwardOffset;
        visuals.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    // 销毁时回收材质。
    private void OnDestroy()
    {
        if (coreMaterial != null)
            Destroy(coreMaterial);
        if (pulseMaterial != null)
            Destroy(pulseMaterial);
    }
}
