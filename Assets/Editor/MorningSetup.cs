using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;

namespace LowPolyGame.EditorTools
{
    // 清晨总装配：一次性完成剩余的全部 Unity 侧操作（幂等，重复执行自动跳过）。
    //   1. SoundLibrary 绑定三个新命中音效（轻/重/受击）。
    //   2. Player.prefab 挂 HitFeedbackController（含 CinemachineImpulseSource）与 PlayerOutfit。
    //   3. Player Rig 全部虚拟相机挂 CinemachineImpulseListener（屏幕震动接收，两场景实例自动继承），
    //      并清理主相机上错误挂载的监听器（ImpulseListener 必须挂在虚拟相机上）。
    //   4. 运行 EquipmentIconGenerator：渲染装备图标 + 创建 14 个装备物品 + 追加目录。
    // 会话内自动执行一次；也可手动点击 Tools/Combat/9 菜单重跑。
    public static class MorningSetup
    {
        const string SoundLibraryPath = "Assets/Resources/Audio/SoundLibrary.asset";
        const string PlayerPrefabPath = "Assets/Resources/Prefabs/Player/Player.prefab";
        const string PlayerRigPath = "Assets/Resources/Prefabs/Player/Player Rig.prefab";
        const string LightHitClipPath = "Assets/Audio/SFX/Combat/player_light_hit.ogg";
        const string HeavyHitClipPath = "Assets/Audio/SFX/Combat/player_heavy_hit.ogg";
        const string HurtClipPath = "Assets/Audio/SFX/Combat/player_hurt.ogg";
        const string TestActPath = "Assets/Scenes/GameScene/TestAct.unity";
        const string Demo1Path = "Assets/Scenes/GameScene/Demo 1.unity";

        [MenuItem("Tools/Combat/9. Morning Setup (All-in-One)")]
        public static void RunAll()
        {
            BindHitSounds();
            SetupPlayerPrefab();
            SetupCameraImpulseListeners();

            EquipmentIconGenerator.GenerateAll();

            // 收尾：打开 TestAct 并补齐背包数据源（Player Resources 含 PlayerInventory + 背包 UI）。
            EditorSceneManager.OpenScene(TestActPath, OpenSceneMode.Single);
            EnsurePlayerResourcesInScene();
            TestActDoctor.RunDoctor();

            AssetDatabase.SaveAssets();
            Debug.Log("[Morning] 总装配完成：音效绑定 / 玩家反馈组件 / 虚拟相机震动监听 / 装备图标与物品 / 测试场景背包就绪。");
        }

        // TestAct 缺少 Player Resources（背包数据组件与 UI 所在）时自动补放。
        static void EnsurePlayerResourcesInScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/UI/Player Resources.prefab");
            if (prefab == null)
            {
                Debug.LogError("[Morning] 找不到 Player Resources.prefab");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(instance, "Place Player Resources");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Morning] TestAct 已补放 Player Resources（背包数据与 UI 源）。");
        }

        [InitializeOnLoadMethod]
        static void AutoRunOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("MorningSetup.AutoRanV2", false))
                    return;

                SessionState.SetBool("MorningSetup.AutoRanV2", true);
                try
                {
                    RunAll();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Morning] 自动装配失败：{ex}");
                }
            };
        }

        // ---- 1. 音效绑定 ----
        static void BindHitSounds()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            if (library == null)
            {
                Debug.LogError($"[Morning] 找不到 SoundLibrary：{SoundLibraryPath}");
                return;
            }

            SerializedObject so = new SerializedObject(library);
            BindClip(so, "playerLightHit", LightHitClipPath);
            BindClip(so, "playerHeavyHit", HeavyHitClipPath);
            BindClip(so, "playerHurtImpact", HurtClipPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }

        // 把 AudioClip 塞进 SoundDefinition 的 clips 数组（空时才绑）。
        static void BindClip(SerializedObject so, string fieldName, string clipPath)
        {
            SerializedProperty definition = so.FindProperty(fieldName);
            if (definition == null)
            {
                Debug.LogWarning($"[Morning] SoundLibrary 缺少字段 {fieldName}");
                return;
            }

            SerializedProperty clips = definition.FindPropertyRelative("clips");
            if (clips == null || clips.arraySize > 0)
                return;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[Morning] 找不到音效文件：{clipPath}");
                return;
            }

            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        }

        // ---- 2. 玩家预制体 ----
        static void SetupPlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool changed = false;
            try
            {
                if (root.GetComponent<HitFeedbackController>() == null)
                {
                    root.AddComponent<HitFeedbackController>();
                    changed = true;
                }
                if (root.GetComponent<PlayerOutfit>() == null)
                {
                    root.AddComponent<PlayerOutfit>();
                    changed = true;
                }

                // 默认武器物品（水晶长剑）绑定给 PlayerOutfit，供卸下武器时恢复外观。
                ItemDefinition crystalSword = FindItemById(2000);
                if (crystalSword != null)
                {
                    SerializedObject so = new SerializedObject(root.GetComponent<PlayerOutfit>());
                    SerializedProperty prop = so.FindProperty("defaultWeaponItem");
                    if (prop != null && prop.objectReferenceValue == null)
                    {
                        prop.objectReferenceValue = crystalSword;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log("[Morning] Player.prefab 已挂载 HitFeedbackController + PlayerOutfit。");
                }
                else
                {
                    Debug.Log("[Morning] Player.prefab 组件已就绪，跳过。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 按 ID 查找物品定义（装备生成器创建的水晶长剑 = 2000）。
        static ItemDefinition FindItemById(int id)
        {
            string[] candidates = System.IO.Directory.GetFiles(
                "Assets/GameData/Definitions/Items", "Equip_2000_*.asset");
            return candidates.Length > 0
                ? AssetDatabase.LoadAssetAtPath<ItemDefinition>(candidates[0])
                : null;
        }

        // ---- 3. 虚拟相机震动监听 ----
        // CinemachineImpulseListener 必须挂在 CinemachineVirtualCamera 上（挂在主相机会报
        // "requires a Cinemachine Virtual Camera component"）。监听器挂在 Player Rig.prefab
        // 的虚拟相机上（prefab 级），TestAct 与 Demo 1 的实例自动继承。
        static void SetupCameraImpulseListeners()
        {
            GameObject rig = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            bool changed = false;
            try
            {
                foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
                {
                    // 移除错误挂到相机本体的监听器（若有）。
                    foreach (var wrongListener in camera.GetComponents<CinemachineImpulseListener>())
                    {
                        Object.DestroyImmediate(wrongListener);
                        changed = true;
                    }
                }

                foreach (var vcam in rig.GetComponentsInChildren<CinemachineVirtualCamera>(true))
                {
                    if (vcam.GetComponent<CinemachineImpulseListener>() == null)
                    {
                        vcam.gameObject.AddComponent<CinemachineImpulseListener>();
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(rig, PlayerRigPath);
                    Debug.Log("[Morning] Player Rig 全部虚拟相机已挂 CinemachineImpulseListener。");
                }
                else
                {
                    Debug.Log("[Morning] 虚拟相机震动监听已就绪，跳过。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }

            // 清理两个场景主相机上错误挂载的监听器（历史遗留）。
            CleanSceneCameraListener(TestActPath);
            CleanSceneCameraListener(Demo1Path);
        }

        // 移除场景主相机上错误挂载的 ImpulseListener（若有）。
        static void CleanSceneCameraListener(string scenePath)
        {
            Scene previous = SceneManager.GetActiveScene();
            if (previous.isDirty)
                EditorSceneManager.SaveScene(previous);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera main = Camera.main;
            if (main == null)
                return;

            var wrongListeners = main.GetComponents<CinemachineImpulseListener>();
            bool removed = false;
            foreach (var listener in wrongListeners)
            {
                Object.DestroyImmediate(listener);
                removed = true;
            }

            if (removed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Morning] 已移除 {scene.name} 主相机上错误挂载的震动监听器。");
            }
        }
    }
}
