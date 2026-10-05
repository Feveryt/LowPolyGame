using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowPolyGame.EditorTools
{
    // 性能优化批处理（配合 Unity MCP 的菜单执行使用，也可手动点击 Tools/Perf 菜单）。
    // 全部操作幂等：重复执行只处理尚未处理的对象。
    //
    // 背景：Demo 1 场景的 95 盏点光全部实时计算、约 3500 个环境预制体实例均无 Static 标记、
    // 资源包模型未开启光照 UV，导致无烘焙、无合批。本工具按步骤逐一处理。
    public static class PerformanceBatch
    {
        const string ScenePrefabPath = "Assets/Resources/Prefabs/Scene/Scene.prefab";
        const string ArtRoot = "Assets/ArtRes";
        const string EnvPackRoot = "Assets/ArtRes/PolygonDungeon";
        const string ActiveSceneName = "Demo 1";

        // 环境物体需要的 Static 组合：烘焙接收 + 遮挡 + 合批。
        // 不含 NavigationStatic，避免影响已烘焙的 NavMesh。
        const StaticEditorFlags EnvFlags =
            StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.ContributeGI
            | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic;

        // 域加载诊断：读出 MCP 包的传输模式实际值，并强制启动 stdio 桥。
        // 仅在桥未运行时执行一次，问题定位后此钩子会自动变为空操作。
        [InitializeOnLoadMethod]
        static void BridgeDiagnostics()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    bool http = false;
                    var cfgType = System.Type.GetType(
                        "MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor");
                    if (cfgType != null)
                    {
                        var instance = cfgType.GetProperty("Instance")?.GetValue(null);
                        if (instance != null)
                            http = (bool)(cfgType.GetProperty("UseHttpTransport")?.GetValue(instance) ?? true);
                    }
                    else
                    {
                        Debug.Log("[Perf] diag: EditorConfigurationCache 类型未找到");
                    }
                    Debug.Log($"[Perf] diag: UseHttpTransport={http}");

                    EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", false);

                    var hostType = System.Type.GetType(
                        "MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost, MCPForUnity.Editor");
                    if (hostType == null)
                    {
                        Debug.Log("[Perf] diag: StdioBridgeHost 类型未找到（包未加载？）");
                        return;
                    }
                    var isRunning = (bool?)hostType.GetField("isRunning",
                        BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? false;
                    if (isRunning)
                    {
                        Debug.Log("[Perf] diag: 桥已在运行");
                        return;
                    }
                    hostType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)
                        ?.Invoke(null, null);
                    Debug.Log("[Perf] diag: 已调用 StdioBridgeHost.Start()");
                }
                catch (System.Exception ex)
                {
                    Debug.Log($"[Perf] diag 异常: {ex}");
                }
            };
        }

        // 步骤 1：给地下城资源包模型批量开启 Generate Lightmap UVs（烘焙前提）。
        // 首次执行会触发大量重导入，可能需要 10-30 分钟，属一次性成本。
        [MenuItem("Tools/Perf/1. Enable Lightmap UVs (PolygonDungeon)")]
        public static void EnableLightmapUVs()
        {
            int changed = 0, already = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EnvPackRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.generateSecondaryUV)
                    {
                        already++;
                        continue;
                    }
                    importer.generateSecondaryUV = true;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Perf] 步骤1 光照UV：新开启 {changed} 个模型，已有 {already} 个跳过");
        }

        // 步骤 2：处理 Scene.prefab 资产——
        //   a) 可烘焙的灯（无动画的点光 + 方向光）改 Baked；51 盏带闪烁动画的 "Point light 12" 保持 Realtime
        //   b) 灯以外的内容按跳过规则标记 Static（资产层标记，所有实例自动继承，不产生实例 override）
        [MenuItem("Tools/Perf/2. Prepare Scene.prefab (lights + static)")]
        public static void PrepareScenePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ScenePrefabPath);
            int baked = 0, realtime = 0, marked = 0, skipped = 0;
            try
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    bool animated = light.GetComponent<Animation>() != null;
                    if (animated && light.type != LightType.Directional)
                    {
                        light.lightmapBakeType = LightmapBakeType.Realtime;
                        realtime++;
                        continue;
                    }
                    if (light.lightmapBakeType != LightmapBakeType.Baked)
                    {
                        light.lightmapBakeType = LightmapBakeType.Baked;
                        baked++;
                    }
                }
                foreach (GameObject go in EnumerateAllChildren(root))
                {
                    if (ShouldSkip(go))
                    {
                        skipped++;
                        continue;
                    }
                    if ((GameObjectUtility.GetStaticEditorFlags(go) & EnvFlags) == EnvFlags)
                        continue;
                    GameObjectUtility.SetStaticEditorFlags(go, EnvFlags);
                    marked++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, ScenePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Perf] 步骤2 Scene.prefab：Baked {baked} 盏，保留 Realtime(闪烁) {realtime} 盏；Static 标记 {marked}，跳过 {skipped}");
        }

        // 步骤 3：处理打开的 Demo 1 场景——
        //   a) 直接摆在场景里的预制体实例：改其源预制体资产的 Static（避免逐实例 override）
        //   b) 不属于预制体的散置对象（如烘焙前遗漏的环境件）：逐个标记
        // 需要先打开 Demo 1 场景。
        [MenuItem("Tools/Perf/3. Mark Static: open scene (Demo 1)")]
        public static void MarkOpenSceneStatic()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name != ActiveSceneName)
            {
                Debug.LogError($"[Perf] 请先打开 {ActiveSceneName} 场景再执行（当前：{active.name}）");
                return;
            }

            int assetsMarked = 0, marked = 0, skipped = 0;
            var handledSources = new HashSet<Object>();

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.hideFlags != HideFlags.None)
                    continue;

                Object source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    if (sourcePath.StartsWith(EnvPackRoot))
                    {
                        if (handledSources.Add(source))
                            assetsMarked += MarkPrefabAssetStatic(sourcePath);
                        continue;
                    }
                }

                if (ShouldSkip(go))
                {
                    skipped++;
                    continue;
                }
                if ((GameObjectUtility.GetStaticEditorFlags(go) & EnvFlags) == EnvFlags)
                    continue;
                GameObjectUtility.SetStaticEditorFlags(go, EnvFlags);
                marked++;
            }

            EditorSceneManager.MarkSceneDirty(active);
            Debug.Log($"[Perf] 步骤3 场景Static：源预制体资产标记 {assetsMarked} 个对象，场景散置对象标记 {marked} 个，跳过 {skipped} 个");
        }

        // 步骤 4：创建 Light Probe Group（动态角色需要探针才能采样到烘焙光）。
        // 沿场景渲染包围盒按网格铺设，探针总数超过上限时自动加大间距。
        [MenuItem("Tools/Perf/4. Create Light Probe Grid (Demo 1)")]
        public static void CreateLightProbeGrid()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name != ActiveSceneName)
            {
                Debug.LogError($"[Perf] 请先打开 {ActiveSceneName} 场景再执行（当前：{active.name}）");
                return;
            }
            if (Object.FindFirstObjectByType<LightProbeGroup>() != null)
            {
                Debug.Log("[Perf] 已存在 Light Probe Group，跳过（如需重铺请先删除旧的）");
                return;
            }

            bool hasBounds = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer || renderer is SkinnedMeshRenderer)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (!hasBounds)
            {
                Debug.LogError("[Perf] 场景中没有可参考的 Renderer");
                return;
            }

            const int maxProbes = 600;
            float spacing = 10f;
            Vector3 size = bounds.size;
            while ((Mathf.CeilToInt(size.x / spacing) + 1) * (Mathf.CeilToInt(size.z / spacing) + 1) * 2 > maxProbes)
                spacing *= 1.5f;

            var positions = new List<Vector3>();
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / spacing));
            int nz = Mathf.Max(1, Mathf.CeilToInt(size.z / spacing));
            float[] heights = { bounds.min.y + 1f, bounds.min.y + Mathf.Min(size.y, 6f) };
            for (int iz = 0; iz <= nz; iz++)
                for (int ix = 0; ix <= nx; ix++)
                    foreach (float h in heights)
                        positions.Add(new Vector3(
                            bounds.min.x + ix * spacing,
                            h,
                            bounds.min.z + iz * spacing));

            var groupGo = new GameObject("Light Probe Group");
            var group = groupGo.AddComponent<LightProbeGroup>();
            group.probePositions = positions.ToArray();
            Undo.RegisterCreatedObjectUndo(groupGo, "Create Light Probe Grid");
            EditorSceneManager.MarkSceneDirty(active);
            Debug.Log($"[Perf] 步骤4 光照探针：间距 {spacing:F1}m，共 {positions.Count} 个探针（包围盒 {size.x:F0}x{size.y:F0}x{size.z:F0}m）。火把密集与走廊拐角可再手动加密。");
        }

        // 步骤 5：烘焙当前场景光照（GPU 光照器）。
        // 若显卡不支持 GPU 光照器，请在 Lighting 窗口改回 Progressive CPU 后点 Generate Lighting。
        [MenuItem("Tools/Perf/5. Generate Lighting (Demo 1)")]
        public static void GenerateLighting()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name != ActiveSceneName)
            {
                Debug.LogError($"[Perf] 请先打开 {ActiveSceneName} 场景再执行（当前：{active.name}）");
                return;
            }
            LightingSettings settings = Lightmapping.lightingSettings;
            if (settings == null)
            {
                Debug.LogError("[Perf] 场景没有 LightingSettings 资产，请先打开 Window > Rendering > Lighting 手动生成一次");
                return;
            }
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            Lightmapping.BakeAsync();
            Debug.Log("[Perf] 步骤5 已启动异步烘焙，可在 Lighting 窗口右下角查看进度");
        }

        static int MarkPrefabAssetStatic(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int marked = 0;
            try
            {
                foreach (GameObject go in EnumerateAllChildren(root))
                {
                    if (ShouldSkip(go) || (GameObjectUtility.GetStaticEditorFlags(go) & EnvFlags) == EnvFlags)
                        continue;
                    GameObjectUtility.SetStaticEditorFlags(go, EnvFlags);
                    marked++;
                }
                if (marked > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return marked;
        }

        static IEnumerable<GameObject> EnumerateAllChildren(GameObject root)
        {
            var stack = new Stack<Transform>();
            stack.Push(root.transform);
            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                yield return current.gameObject;
                for (int i = 0; i < current.childCount; i++)
                    stack.Push(current.GetChild(i));
            }
        }

        // 跳过规则：会动的都不标——
        //   自身或祖先带 Animation/Animator（动画道具、陷阱、角色）、UI(Canvas 整树)、
        //   相机与监听器、粒子（无需静态）、以及玩家/敌人等按名称兜底。
        static bool ShouldSkip(GameObject go)
        {
            for (Transform t = go.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<Animation>() != null || t.GetComponent<Animator>() != null)
                    return true;
                if (t.GetComponent<Canvas>() != null)
                    return true;
                if (t.GetComponent<Camera>() != null || t.GetComponent<AudioListener>() != null)
                    return true;
                if (t.GetComponent<ParticleSystem>() != null)
                    return true;
            }
            string name = go.name;
            return name.StartsWith("Player") || name.StartsWith("Character_")
                || name == "StoneGolem" || name.EndsWith("Canvas");
        }
    }
}

