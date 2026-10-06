using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

namespace LowPolyGame.EditorTools
{
    // TestAct 测试场景体检：核对玩家装置、两个 Boss、导航连通性与预制体组件继承。
    // 只读验证 + 缺失项自动补齐；每次执行输出完整报告到 Console。
    public static class TestActDoctor
    {
        const string TestActPath = "Assets/Scenes/GameScene/TestAct.unity";
        const string PlayerRigPath = "Assets/Resources/Prefabs/Player/Player Rig.prefab";
        const string GolemPath = "Assets/Resources/Prefabs/Enemy/StoneGolem.prefab";
        const string DragonPath = "Assets/Resources/Prefabs/Enemy/DragonBoss.prefab";

        [MenuItem("Tools/Combat/6. TestAct Doctor (Verify + Report)")]
        public static void RunDoctor()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[TestAct] 运行模式下不执行体检，请先退出运行模式。");
                return;
            }

            // 确保在 TestAct 场景中体检。
            Scene active = SceneManager.GetActiveScene();
            if (active.path != TestActPath)
            {
                if (active.isDirty)
                    EditorSceneManager.SaveScene(active);
                active = EditorSceneManager.OpenScene(TestActPath, OpenSceneMode.Single);
            }

            StringBuilder report = new StringBuilder();
            report.Append("[TestAct 体检] ");

            // 1. 根对象清单。
            report.Append("根对象=").Append(active.rootCount).Append("个; ");

            // 2. 玩家装置：PlayerController + 相机链路。
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                report.Append("玩家=").Append(player.name).Append('@').Append(player.transform.position.ToString("F1"));
                // 相机装置：主相机上的 CinemachineBrain + 场景中的虚拟相机。
                Camera main = Camera.main;
                int virtualCameras = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Count(m => m != null && m.GetType().Name.Contains("CinemachineVirtualCamera"));
                bool hasBrain = main != null &&
                    main.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "CinemachineBrain");
                report.Append(" 主相机=").Append(main != null).Append(" Brain=").Append(hasBrain)
                    .Append(" 虚拟相机=").Append(virtualCameras).Append("台");
            }
            else
            {
                report.Append("玩家=缺失(尝试补放)");
                PlaceActor<PlayerController>(PlayerRigPath, new Vector3(0f, 0f, -8f), Quaternion.identity);
            }
            report.Append("; ");

            // 3. 石头人：位置 + 新组件继承。
            StoneGolem golem = Object.FindFirstObjectByType<StoneGolem>();
            if (golem != null)
            {
                report.Append("石头人=").Append(golem.name).Append('@').Append(golem.transform.position.ToString("F1"))
                    .Append(" 预警=").Append(golem.GetComponent<AttackTelegraph>() != null)
                    .Append(" 石肤=").Append(golem.GetComponent<StoneGolemDefend>() != null)
                    .Append(" 导航代理=").Append(golem.GetComponent<NavMeshAgent>() != null);
            }
            else
            {
                report.Append("石头人=缺失(尝试补放)");
                PlaceActor<StoneGolem>(GolemPath, new Vector3(0f, 0f, 5f), Quaternion.Euler(0f, 180f, 0f));
            }
            report.Append("; ");

            // 4. 龙：位置 + 新组件继承。
            DragonBoss dragon = Object.FindFirstObjectByType<DragonBoss>();
            if (dragon != null)
            {
                report.Append("龙=").Append(dragon.name).Append('@').Append(dragon.transform.position.ToString("F1"))
                    .Append(" 预警=").Append(dragon.GetComponent<AttackTelegraph>() != null)
                    .Append(" 攻击=").Append(dragon.GetComponent<DragonBossAttack>() != null)
                    .Append(" 导航代理=").Append(dragon.GetComponent<NavMeshAgent>() != null);
            }
            else
            {
                report.Append("龙=缺失(尝试补放)");
                PlaceActor<DragonBoss>(DragonPath, new Vector3(0f, 0f, 15f), Quaternion.Euler(0f, 180f, 0f));
            }
            report.Append("; ");

            // 5. 导航连通性：玩家 -> 两个 Boss 的寻路测试。
            if (player != null)
            {
                if (golem != null)
                    report.Append("寻路玩家->石头人=").Append(CheckPath(player.transform.position, golem.transform.position));
                if (dragon != null)
                    report.Append(" 玩家->龙=").Append(CheckPath(player.transform.position, dragon.transform.position));
            }

            Debug.Log(report.ToString());

            // 6. 若补放过对象则保存。
            if (golem == null || dragon == null || player == null)
            {
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);
                Debug.Log("[TestAct] 体检补齐了缺失对象并保存场景。");
            }
        }

        // 测试两点间的导航路径是否连通。
        static string CheckPath(Vector3 from, Vector3 to)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
                return "无法计算";

            if (path.status == NavMeshPathStatus.PathComplete)
            {
                float length = 0f;
                for (int i = 1; i < path.corners.Length; i++)
                    length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                return $"连通({length:F1}m)";
            }

            return path.status.ToString();
        }

        // 补放缺失角色。
        static void PlaceActor<T>(string prefabPath, Vector3 spawn, Quaternion rotation) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TestAct] 找不到预制体：{prefabPath}");
                return;
            }

            Vector3 position = spawn;
            if (Physics.Raycast(spawn + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f,
                ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {typeof(T).Name}");
            Debug.Log($"[TestAct] 已补放 {typeof(T).Name} @ {position}");
        }

        // 一次性自动体检钩子已拆除（2026-10-06）：体检已完成。
        // 重新体检请手动执行 Tools/Combat/6 菜单项。
    }
}
