using System.Collections;
using UnityEngine;

/// <summary>
/// 程序化简易攻击特效：完全不依赖美术资源与 ShaderGraph。
/// SlashArc：程序生成 120° 扇环"斩击弧"网格（Sprites/Default 支持顶点色半透明），
///           命中瞬间在攻击者前方快速放大并淡出。
/// CubeBurst：彩色方块粒子爆（Unity 默认粒子即为方块），作为一切命中的兜底反馈。
/// 所有材质运行时创建、用后即焚；片段数少、生命周期短，性能开销可忽略。
/// </summary>
public static class SimpleAttackFX
{
    /// <summary>在 origin 前方立起一道弧形斩击特效（竖直平面，朝 forward 方向）。</summary>
    public static void SlashArc(Vector3 origin, Vector3 forward, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        GameObject go = new GameObject("SlashArc");
        go.transform.position = origin + Vector3.up * 1.2f;
        go.transform.rotation =
            Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        Mesh arc = BuildArcMesh(radius);
        filter.sharedMesh = arc;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        renderer.sharedMaterial = material;

        FXLifetime lifetime = go.AddComponent<FXLifetime>();
        lifetime.Initialize(material, arc, 0.28f, radius * 0.7f, radius * 1.25f);
    }

    /// <summary>彩色方块粒子爆（命中反馈兜底方案）。</summary>
    public static void CubeBurst(Vector3 position, int count, Color color, float speed = 5f)
    {
        GameObject go = new GameObject("CubeBurst");
        go.transform.position = position;

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = speed;
        main.startSize = 0.22f;
        main.startColor = color;
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 60)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        renderer.material = material;

        particles.Play();
        Object.Destroy(go, 1.2f);
        Object.Destroy(material, 1.4f);
    }

    /// <summary>生成朝 +Z 的水平扇环网格（内半径 0.45 倍、覆盖 120°、外缘更亮）。</summary>
    static Mesh BuildArcMesh(float radius)
    {
        const int segments = 20;
        const float sweep = 120f;
        float inner = radius * 0.45f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (-sweep * 0.5f + sweep * i / segments);
            Vector3 direction = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            vertices[i * 2] = direction * inner;
            vertices[i * 2 + 1] = direction * radius;
            colors[i * 2] = new Color(1f, 1f, 1f, 0.25f);
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0.95f);
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

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            colors = colors,
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>特效生命周期：缩放由小放大、材质透明度线性淡出，结束连同网格与材质一起销毁。</summary>
    [DisallowMultipleComponent]
    sealed class FXLifetime : MonoBehaviour
    {
        Material material;
        Mesh mesh;
        float duration;
        float startScale;
        float endScale;
        float elapsed;

        /// <summary>初始化生命周期参数；mesh 会在组件销毁时一并清理。</summary>
        public void Initialize(Material material, Mesh mesh, float duration, float startScale, float endScale)
        {
            this.material = material;
            this.mesh = mesh;
            this.duration = Mathf.Max(0.05f, duration);
            this.startScale = startScale;
            this.endScale = endScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            if (material != null)
            {
                Color color = material.color;
                color.a = Mathf.Lerp(0.95f, 0f, t);
                material.color = color;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
                if (mesh != null)
                    Destroy(mesh, 0.5f);
                if (material != null)
                    Destroy(material, 0.5f);
            }
        }
    }
}
