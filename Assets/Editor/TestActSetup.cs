using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

namespace LowPolyGame.EditorTools
{
    // TestAct 测试场景部署：一次性放好玩家装置与两个 Boss，并烘焙导航网格。
    // 布局（Plane 200x200 平台，y=0 地面）：
    //   玩家 (-8) <- 石头人 (+5) <- 龙 (+15)，玩家出生点背向道具，双方入口正对。
    // 幂等：已存在的对象跳过；重复执行只补缺失项并重建导航。
    public static class TestActSetup
    {
        const string TestActPath = "Assets/Scenes/GameScene/TestAct.unity";
        const string PlayerRigPath = "Assets/Resources/Prefabs/Player/Player Rig.prefab";
        const string GolemPath = "Assets/Resources/Prefabs/Enemy/StoneGolem.prefab";
        const string DragonPath = "Assets/Resources/Prefabs/Enemy/DragonBoss.prefab";

        // 出生点（世界坐标，y 由射线吸附到地面）。
        static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -8f);
        static readonly Vector3 GolemSpawn = new Vector3(0f, 0f, 5f);
        static readonly Vector3 DragonSpawn = new Vector3(0f, 0f, 15f);

        [MenuItem("Tools/Combat/5. Setup TestAct Scene (Player + Bosses + NavMesh)")]
        public static void SetupTestAct()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[TestAct] 运行模式下不执行场景部署，请先退出运行模式。");
                return;
            }

            // 1. 保存当前场景，避免切换丢改动。
            Scene current = SceneManager.GetActiveScene();
            if (current.isDirty)
                EditorSceneManager.SaveScene(current);

            // 2. 打开 TestAct。
            Scene testScene = EditorSceneManager.OpenScene(TestActPath, OpenSceneMode.Single);

            // 3. 放置玩家装置（Player Rig 含角色 + 探索/战斗双 Cinemachine 相机）。
            int placed = 0;
            placed += PlaceIfMissing<PlayerController>(PlayerRigPath, PlayerSpawn, Quaternion.identity);
            placed += PlaceIfMissing<StoneGolem>(GolemPath, GolemSpawn, Quaternion.Euler(0f, 180f, 0f));
            placed += PlaceIfMissing<DragonBoss>(DragonPath, DragonSpawn, Quaternion.Euler(0f, 180f, 0f));

            // 4. 烘焙导航网格（NavMeshSurface 收集全部物理碰撞体，不依赖静态标记）。
            int triangles = BuildNavMesh();

            // 5. 保存场景。
            if (placed > 0 || triangles > 0)
            {
                EditorSceneManager.MarkSceneDirty(testScene);
                EditorSceneManager.SaveScene(testScene);
            }

            Debug.Log($"[TestAct] 测试场景部署完成：新放置 {placed} 个对象，导航网格 {triangles} 三角形。布局：玩家(0,-8) 石头人(0,5) 龙(0,15)。");
        }

        // 若场景内不存在指定类型组件，则实例化预制体并放到出生点。
        static int PlaceIfMissing<T>(string prefabPath, Vector3 spawn, Quaternion rotation) where T : Component
        {
            T existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                Debug.Log($"[TestAct] 已存在 {typeof(T).Name}（{existing.name}），跳过放置。");
                return 0;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TestAct] 找不到预制体：{prefabPath}");
                return 0;
            }

            // 射线吸附地面高度，避免悬空或埋入。
            Vector3 position = spawn;
            if (Physics.Raycast(spawn + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f,
                ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {typeof(T).Name}");

            Debug.Log($"[TestAct] 已放置 {typeof(T).Name}：{instance.name} @ {position}");
            return 1;
        }

        // 确保 NavMeshSurface 存在并重建导航网格；返回三角面数。
        static int BuildNavMesh()
        {
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                GameObject surfaceObject = new GameObject("NavMeshSurface");
                surface = surfaceObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                Debug.Log("[TestAct] 已创建 NavMeshSurface（收集全部物理碰撞体）。");
            }

            surface.BuildNavMesh();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices != null ? triangulation.indices.Length / 3 : 0;

            // 验证三个出生点是否落在导航网格上。
            LogSample("玩家出生点", PlayerSpawn);
            LogSample("石头人出生点", GolemSpawn);
            LogSample("龙出生点", DragonSpawn);
            return triangles;
        }

        // 记录某个点最近的导航网格采样结果，用于验证可达性。
        static void LogSample(string label, Vector3 point)
        {
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas);
            Debug.Log(found
                ? $"[TestAct] 导航验证 {label}：命中 {hit.position}（距离 {hit.distance:F2}m）"
                : $"[TestAct] 导航验证 {label}：未命中导航网格！");
        }

        // 一次性自动部署钩子已拆除（2026-10-06）：部署已完成。
        // 重新部署请手动执行 Tools/Combat/5 菜单项。
    }
}
