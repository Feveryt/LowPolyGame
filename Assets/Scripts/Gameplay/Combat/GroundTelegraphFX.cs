using UnityEngine;

/// <summary>
/// 地面预警表现：程序生成的不受光照影响的贴地危险区图形（圆盘／扇形），
/// 用于在 BOSS 前摇期间清楚标出"哪里会挨打"和"还有多久挨打"。
///
/// 结构（全部用 Sprites/Default，无光照、支持顶点色与半透明）：
///   - 外圈定界环：固定的明亮圆环，精确等于实际伤害半径；
///   - 底色区域：低透明度填充，标出整个危险范围；
///   - 进度填充：随前摇时间从 0 长满到危险半径，直观提示落点时机；
///   - 落点补光：危险区中心的彩色点光，在昏暗场景里照亮地面。
///
/// 网格静态缓存（单位半径，XZ 平面），同一个网格被所有实例共享；
/// 每个实例只额外创建一个材质，生命周期结束一并销毁。
/// 相比直接复用美术包里的环境粒子（FX_Ring 是 20 秒循环 + 预热的环境尘圈），
/// 这套图形不受场景光照与背景影响，在白地面和黑地牢里都能看清。
/// </summary>
public sealed class GroundTelegraphFX : MonoBehaviour
{
    // 圆盘网格分段数。
    private const int DiscSegments = 64;
    // 定界环厚度占半径的比例。
    private const float RimThickness = 0.07f;
    // 贴地各个图层的抬升高度，避免与地面深度冲突。
    private const float DiscHeight = 0.03f;
    private const float FillHeight = 0.045f;
    private const float RimHeight = 0.06f;
    // 顶点色透明度：底色区域 / 进度填充 / 定界环。
    private const float AreaAlpha = 0.20f;
    private const float FillAlpha = 0.34f;
    private const float RimAlpha = 0.95f;
    // 整体淡入 / 淡出时长（秒）。
    private const float FadeInDuration = 0.10f;
    private const float FadeOutDuration = 0.22f;
    // 前摇期间的呼吸脉冲频率与幅度。
    private const float PulseFrequency = 9f;
    private const float PulseAmplitude = 0.18f;

    // 静态共享网格。
    private static Mesh discMesh;
    private static Mesh rimMesh;
    private static Mesh cachedSectorMesh;
    private static float cachedSectorHalfAngle = float.NaN;

    // 本次表现使用的材质（每实例一份，销毁时回收）。
    private Material material;
    // 随进度生长的填充层。
    private Transform fillLayer;
    // 危险区半径 / 扇形长度。
    private float radius;
    // 总时长与已过时间。
    private float duration;
    private float elapsed;
    // 预警颜色。
    private Color color;
    // 用于呼吸脉冲的相位。
    private float pulsePhase;

    /// <summary>
    /// 在指定位置显示一个圆形危险区（贴地圆盘）。
    /// </summary>
    /// <param name="center">危险区中心的地面点。</param>
    /// <param name="radius">危险区半径，应等于实际伤害半径。</param>
    /// <param name="color">预警颜色（红＝不可格挡、黄＝高破防）。</param>
    /// <param name="duration">前摇总时长，进度填充会在这段时间内长满。</param>
    /// <param name="withLight">是否在危险区中心补一盏彩色点光。</param>
    public static GroundTelegraphFX ShowDisc(Vector3 center, float radius, Color color, float duration, bool withLight = true)
    {
        if (radius <= 0.01f)
            return null;

        GroundTelegraphFX fx = Create(color, duration, radius);
        if (fx == null)
            return null;

        fx.BuildLayers(EnsureDiscMesh(), EnsureRimMesh(), Quaternion.identity);
        fx.ConfigureLight(center, radius, color, withLight, 0.7f);
        return fx;
    }

    /// <summary>
    /// 在指定位置显示一个扇形危险区（用于龙息这类锥形范围技）。
    /// </summary>
    /// <param name="origin">扇形顶点（BOSS 嘴部/脚下位置）。</param>
    /// <param name="forward">扇形朝向。</param>
    /// <param name="range">扇形长度（等于实际射程）。</param>
    /// <param name="halfAngle">扇形半角（度）。</param>
    /// <param name="color">预警颜色。</param>
    /// <param name="duration">前摇总时长。</param>
    /// <param name="withLight">是否补一盏跟随扇形中轴的彩色点光。</param>
    public static GroundTelegraphFX ShowCone(Vector3 origin, Vector3 forward, float range, float halfAngle,
        Color color, float duration, bool withLight = true)
    {
        if (range <= 0.01f || halfAngle <= 0.5f)
            return null;

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        GroundTelegraphFX fx = Create(color, duration, range);
        if (fx == null)
            return null;

        Quaternion yaw = Quaternion.LookRotation(flatForward, Vector3.up);
        fx.BuildLayers(EnsureSectorMesh(halfAngle), EnsureRimMesh(), yaw);

        // 点光放在扇形中轴的前半段，朝向上方照亮整个扇面。
        Vector3 lightPoint = origin + flatForward * (range * 0.5f);
        fx.ConfigureLight(lightPoint, range * 0.5f, color, withLight, 1.1f);
        fx.transform.SetPositionAndRotation(origin, yaw);
        return fx;
    }

    // 创建宿主对象与材质。
    private static GroundTelegraphFX Create(Color color, float duration, float radius)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        GameObject host = new GameObject("GroundTelegraphFX");
        GroundTelegraphFX fx = host.AddComponent<GroundTelegraphFX>();
        fx.transform.position = Vector3.zero;

        fx.color = color;
        fx.duration = Mathf.Max(0.12f, duration);
        fx.radius = radius;
        fx.pulsePhase = Random.value * Mathf.PI * 2f;

        fx.material = new Material(shader);
        fx.material.color = new Color(color.r, color.g, color.b, 0f);
        fx.material.renderQueue = 3000;
        return fx;
    }

    // 建立三层贴地图形：底色区域 + 进度填充 + 定界环。
    private void BuildLayers(Mesh shape, Mesh rim, Quaternion yaw)
    {
        // 底色区域：铺满整个危险范围。
        CreateLayer("Area", shape, radius, DiscHeight, yaw);

        // 进度填充：从中心长满到危险半径。
        GameObject fill = CreateLayer("Fill", shape, 0.001f, FillHeight, yaw);
        fillLayer = fill.transform;

        // 定界环：精确等于伤害半径的明亮圆环。
        CreateLayer("Rim", rim, radius, RimHeight, yaw);
    }

    // 创建一个贴地图层。
    private GameObject CreateLayer(string layerName, Mesh mesh, float scale, float height, Quaternion yaw)
    {
        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);
        layer.transform.localPosition = new Vector3(0f, height, 0f);
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale = new Vector3(scale, 1f, scale);

        MeshFilter filter = layer.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return layer;
    }

    // 在危险区中心补一盏彩色点光（有些场景只有烘焙光，动态点光能让落点更醒目）。
    private void ConfigureLight(Vector3 center, float zoneRadius, Color lightColor, bool enabled, float height)
    {
        if (!enabled)
        {
            transform.position = center;
            return;
        }

        GameObject lightObject = new GameObject("ZoneLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, height, 0f);
        Light zoneLight = lightObject.AddComponent<Light>();
        zoneLight.type = LightType.Point;
        zoneLight.color = lightColor;
        zoneLight.range = Mathf.Max(6f, zoneRadius * 2.8f);
        zoneLight.intensity = 6f;
        zoneLight.shadows = LightShadows.None;
        zoneLight.renderMode = LightRenderMode.ForcePixel;

        transform.position = center;
    }

    // 每帧推进进度、呼吸脉冲与整体淡入淡出。
    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        pulsePhase += Time.deltaTime * PulseFrequency;

        // 进度填充按 sqrt 生长，前半段就能看到明显推进，计时更易读。
        if (fillLayer != null)
            fillLayer.localScale = new Vector3(radius * Mathf.Sqrt(progress), 1f, radius * Mathf.Sqrt(progress));

        // 接近落点时脉冲加强，形成紧迫感。
        float urgency = Mathf.Lerp(0.6f, 1f, progress);
        float pulse = 1f - PulseAmplitude * urgency * (0.5f + 0.5f * Mathf.Sin(pulsePhase));

        // 淡入 → 保持 → 淡出。
        float alpha = Mathf.Clamp01(elapsed / FadeInDuration);
        float remaining = duration - elapsed;
        if (remaining < FadeOutDuration)
            alpha *= Mathf.Clamp01(remaining / FadeOutDuration);

        if (material != null)
            material.color = new Color(color.r, color.g, color.b, alpha * Mathf.Lerp(1f, pulse, progress));

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    // 销毁时回收材质，避免泄漏。
    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
            material = null;
        }
    }

    // 单位半径实心圆盘（XZ 平面，朝 +Y）。
    private static Mesh EnsureDiscMesh()
    {
        if (discMesh == null)
            discMesh = BuildRingMesh(DiscSegments, 0f, AreaAlpha * 0.55f, AreaAlpha, "GT_Disc");
        return discMesh;
    }

    // 单位半径定界环。
    private static Mesh EnsureRimMesh()
    {
        if (rimMesh == null)
            rimMesh = BuildRingMesh(DiscSegments, 1f - RimThickness, RimAlpha, RimAlpha, "GT_Rim");
        return rimMesh;
    }

    // 按半角缓存扇形网格（同一次战斗半角固定，只需构建一次）。
    private static Mesh EnsureSectorMesh(float halfAngle)
    {
        if (cachedSectorMesh != null && Mathf.Abs(cachedSectorHalfAngle - halfAngle) < 0.01f)
            return cachedSectorMesh;

        cachedSectorMesh = BuildSectorMesh(halfAngle, DiscSegments, "GT_Sector");
        cachedSectorHalfAngle = halfAngle;
        return cachedSectorMesh;
    }

    // 构建单位半径的环形（innerRadius=0 即实心圆盘）网格，顶点色带透明度。
    private static Mesh BuildRingMesh(int segments, float innerRadius, float innerAlpha, float outerAlpha, string meshName)
    {
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            vertices[i * 2] = direction * innerRadius;
            vertices[i * 2 + 1] = direction;
            colors[i * 2] = new Color(1f, 1f, 1f, innerAlpha);
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, outerAlpha);
        }

        for (int i = 0; i < segments; i++)
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

        return Finalize(new Mesh { name = meshName, vertices = vertices, triangles = triangles, colors = colors });
    }

    // 构建单位半径、以 +Z 为中轴、朝 +Y 的扇形网格。
    private static Mesh BuildSectorMesh(float halfAngle, int segments, string meshName)
    {
        Vector3[] vertices = new Vector3[segments + 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        colors[0] = new Color(1f, 1f, 1f, AreaAlpha * 0.6f);

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
            vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            colors[i + 1] = new Color(1f, 1f, 1f, AreaAlpha);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        return Finalize(new Mesh { name = meshName, vertices = vertices, triangles = triangles, colors = colors });
    }

    // 统一结算法线与包围盒。
    private static Mesh Finalize(Mesh mesh)
    {
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
