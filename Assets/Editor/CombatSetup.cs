using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LowPolyGame.EditorTools
{
    // 魂类战斗系统装配脚本：一键完成玩家防御的动画连线与预制体组件挂载。
    // 全部操作幂等：重复执行只补充缺失的参数、状态、过渡与组件。
    public static class CombatSetup
    {
    const string PlayerControllerPath = "Assets/ArtRes/Animator/Player.controller";
    const string PlayerPrefabPath = "Assets/Resources/Prefabs/Player/Player.prefab";
    const string AnimRoot = "Assets/ArtRes/Dynamic Sword Animset/Animations/InPlace";
    const string SparklesFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Sparkles.prefab";
    const string GolemPrefabPath = "Assets/Resources/Prefabs/Enemy/StoneGolem.prefab";
    const string RingFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Ring.prefab";
    const string GlowCubeFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Glow_Cube.prefab";
    const string GolemAttackDefinitionPath = "Assets/GameData/Definitions/Characters/StoneGolem/StoneGolemAttack_StoneGolem.asset";
    const string RockRoundPath = "Assets/ArtRes/PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rock_Round_01.prefab";
    const string DustFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Dust_Large_Soft.prefab";
    const string TreantWavePath = "Assets/ArtRes/Forest Creatures Pack/Treant Guard/Prefabs/Shockwave-Yellow.prefab";

    [MenuItem("Tools/Combat/1. Setup Player Guard (Animator + Prefab)")]
    public static void SetupPlayerGuard()
    {
        SetupPlayerGuardAnimator();
        SetupPlayerGuardPrefab();
    }

    [MenuItem("Tools/Combat/2. Setup Stone Golem Combat (Prefab + Definition)")]
    public static void SetupStoneGolemCombat()
    {
        SetupGolemPrefab();
        SetupGolemAttackDefinition();
    }

    [MenuItem("Tools/Combat/4. Setup Hurt Reactions (Player + Golem)")]
    public static void SetupHurtReactions()
    {
        SetupPlayerHurtTransitions();
        SetupGolemHurtTransitions();
    }

    // ---- 受击反应修正 ----
    // 资源包/旧控制器的受击过渡只从待机出发：攻击/移动中被命中时受击动画不播放，
    // 表现为"受击延迟"。这里为所有战斗状态补上直达受击状态的过渡。
    static void SetupPlayerHurtTransitions()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[Combat] 找不到玩家控制器：{PlayerControllerPath}");
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState hurt = FindState(machine, "Damage_01");
        if (hurt == null)
        {
            Debug.LogError("[Combat] 玩家控制器中找不到 Damage_01 状态。");
            return;
        }

        int added = 0;
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state == null || state == hurt)
                continue;

            // 只从攻击与装备切换状态补过渡；移动树已有，防御态的受击走格挡反应。
            bool isCombatState = state.name.StartsWith("Attack") ||
                state.name.Contains("Eqip") || state.name.StartsWith("Jump") ||
                state.name.StartsWith("Stun");
            if (!isCombatState)
                continue;

            if (EnsureTransition(state, hurt, "Damaged", 0f, 0.06f))
                added++;
        }

        EditorUtility.SetDirty(controller);
        Debug.Log($"[Combat] 玩家受击过渡补充完成：新增 {added} 条（攻击/装备/硬直状态 -> Damage_01）。");
    }

    static void SetupGolemHurtTransitions()
    {
        const string golemControllerPath =
            "Assets/ArtRes/Forest Creatures Pack/Forest Golem/Controller/Forest Golem Controller.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(golemControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[Combat] 找不到石头人控制器：{golemControllerPath}");
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState hurt = FindState(machine, "TakeDamage");
        AnimatorState die = FindState(machine, "Die");
        if (hurt == null || die == null)
        {
            Debug.LogError("[Combat] 石头人控制器中找不到 TakeDamage/Die 状态。");
            return;
        }

        int hurtAdded = 0, dieAdded = 0;
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state == null || state == hurt || state == die || state.name == "Idle")
                continue;

            // 移动与攻击状态全部补受击过渡；死亡过渡只从攻击与移动状态补（Idle 已有）。
            bool isLocomotionOrAttack = state.name.Contains("Walk") || state.name.Contains("Run") ||
                state.name.Contains("Strafe") || state.name.Contains("Jump") ||
                state.name == "Punch" || state.name == "DoublePunch" ||
                state.name == "Hit Ground" || state.name == "Spell Cast" || state.name == "Defend";

            if (!isLocomotionOrAttack)
                continue;

            if (EnsureTransition(state, hurt, "Take Damage", 0f, 0.06f))
                hurtAdded++;
            if (EnsureTransition(state, die, "Die", 0f, 0.08f))
                dieAdded++;
        }

        EditorUtility.SetDirty(controller);
        Debug.Log($"[Combat] 石头人受击过渡补充完成：受击 +{hurtAdded} 条，死亡 +{dieAdded} 条。");
    }

    // 一次性自动装配钩子已拆除（2026-10-06）：全部装配已完成并通过验证。
    // 如需重新装配（例如修改了绑定素材），手动执行 Tools/Combat 菜单下的对应项即可。

        // ---- 动画连线 ----
        static void SetupPlayerGuardAnimator()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Combat] 找不到玩家控制器：{PlayerControllerPath}");
                return;
            }

            EnsureParameter(controller, "BlockStart", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "BlockEnd", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "BlockDamage", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "PerfectGuard", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "GuardBreak", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(machine, "Equipped Locomotion");
            if (locomotion == null)
            {
                Debug.LogError("[Combat] 玩家控制器中找不到 Equipped Locomotion 状态。");
                return;
            }

            AnimatorState blockStart = EnsureState(machine, "Block_Start", $"{AnimRoot}/Block_Start.FBX");
            AnimatorState blockLoop = EnsureState(machine, "Block_Loop", $"{AnimRoot}/Block_Loop.FBX");
            AnimatorState blockGuard = EnsureState(machine, "Block_Guard", $"{AnimRoot}/Block_Guard.FBX");
            AnimatorState blockDamage = EnsureState(machine, "Block_Damage", $"{AnimRoot}/Block_Damage.FBX");
            AnimatorState blockEnd = EnsureState(machine, "Block_End", $"{AnimRoot}/Block_End.FBX");
            AnimatorState stun = EnsureState(machine, "Stun", $"{AnimRoot}/Stun.FBX");

            // 举盾：持武器移动树 -> 举盾。
            EnsureTransition(locomotion, blockStart, "BlockStart", exitTime: 0f, duration: 0.08f);

            // 举盾 -> 持盾循环。
            EnsureExitTransition(blockStart, blockLoop, exitTime: 0.8f, duration: 0.15f);

            // 收盾 -> 收盾动作 -> 回到移动树。
            EnsureTransition(blockLoop, blockEnd, "BlockEnd", exitTime: 0f, duration: 0.12f);
            EnsureExitTransition(blockEnd, locomotion, exitTime: 0.9f, duration: 0.15f);

            // 盾击反应：举盾/持盾 -> 格挡受击 -> 回到持盾循环。
            EnsureTransition(blockStart, blockDamage, "BlockDamage", exitTime: 0f, duration: 0.06f);
            EnsureTransition(blockLoop, blockDamage, "BlockDamage", exitTime: 0f, duration: 0.06f);
            EnsureExitTransition(blockDamage, blockLoop, exitTime: 0.85f, duration: 0.1f);

            // 精准防御：持盾 -> 弹开动作 -> 回到持盾循环。
            EnsureTransition(blockLoop, blockGuard, "PerfectGuard", exitTime: 0f, duration: 0.05f);
            EnsureTransition(blockStart, blockGuard, "PerfectGuard", exitTime: 0f, duration: 0.05f);
            EnsureExitTransition(blockGuard, blockLoop, exitTime: 0.75f, duration: 0.12f);

            // 破防：所有防御状态 -> 硬直 -> 移动树。
            EnsureTransition(blockStart, stun, "GuardBreak", exitTime: 0f, duration: 0.1f);
            EnsureTransition(blockLoop, stun, "GuardBreak", exitTime: 0f, duration: 0.1f);
            EnsureTransition(blockGuard, stun, "GuardBreak", exitTime: 0f, duration: 0.1f);
            EnsureTransition(blockDamage, stun, "GuardBreak", exitTime: 0f, duration: 0.1f);
            EnsureExitTransition(stun, locomotion, exitTime: 1f, duration: 0.15f);

            // 翻滚打断防御：所有防御状态可被四方向翻滚打断。
            AnimatorState rollFront = FindState(machine, "Rolling_Front");
            AnimatorState rollBack = FindState(machine, "Rolling_Back_01");
            AnimatorState rollLeft = FindState(machine, "Rolling_Left");
            AnimatorState rollRight = FindState(machine, "Rolling_Right");
            AnimatorState[] guardStates = { blockStart, blockLoop, blockDamage, blockGuard };
            foreach (AnimatorState guardState in guardStates)
            {
                EnsureTransition(guardState, rollFront, "RollForward", exitTime: 0f, duration: 0.05f);
                EnsureTransition(guardState, rollBack, "RollBack", exitTime: 0f, duration: 0.05f);
                EnsureTransition(guardState, rollLeft, "RollLeft", exitTime: 0f, duration: 0.05f);
                EnsureTransition(guardState, rollRight, "RollRight", exitTime: 0f, duration: 0.05f);
            }

            EditorUtility.SetDirty(controller);
            Debug.Log("[Combat] 玩家控制器防御连线完成：6 状态 / 5 参数 / 31 条过渡已确保存在。");
        }

        // ---- 预制体组件 ----
        static void SetupPlayerGuardPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                GameObject host = FindComponentHost(root);
                if (host == null)
                {
                    Debug.LogError($"[Combat] {PlayerPrefabPath} 中找不到 PlayerCombat 宿主对象。");
                    return;
                }

                bool changed = false;
                if (host.GetComponent<PlayerGuard>() == null)
                {
                    Undo.RegisterCompleteObjectUndo(root, "Setup Player Guard");
                    host.AddComponent<PlayerGuard>();
                    changed = true;
                }
                if (host.GetComponent<CombatFeedback>() == null)
                {
                    if (!changed)
                        Undo.RegisterCompleteObjectUndo(root, "Setup Player Guard");
                    host.AddComponent<CombatFeedback>();
                    changed = true;
                }

                PlayerGuard guard = host.GetComponent<PlayerGuard>();
                GameObject sparkles = AssetDatabase.LoadAssetAtPath<GameObject>(SparklesFxPath);
                if (guard != null && sparkles != null)
                {
                    SerializedObject so = new SerializedObject(guard);
                    SerializedProperty prop = so.FindProperty("perfectGuardEffectPrefab");
                    if (prop != null && prop.objectReferenceValue == null)
                    {
                        prop.objectReferenceValue = sparkles;
                        so.ApplyModifiedProperties();
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log("[Combat] Player.prefab 已挂载 PlayerGuard + CombatFeedback，并绑定弹反特效 FX_Sparkles。");
                }
                else
                {
                    Debug.Log("[Combat] Player.prefab 组件已就绪，跳过。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 在预制体中定位挂载玩家战斗组件的对象（优先根节点）。
        static GameObject FindComponentHost(GameObject root)
        {
            if (root.GetComponent<PlayerCombat>() != null || root.GetComponent<PlayerController>() != null)
                return root;

            PlayerCombat child = root.GetComponentInChildren<PlayerCombat>(true);
            return child != null ? child.gameObject : null;
        }

        // ---- 石头人装配 ----
        static void SetupGolemPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GolemPrefabPath);
            try
            {
                bool changed = false;
                if (root.GetComponent<AttackTelegraph>() == null)
                {
                    root.AddComponent<AttackTelegraph>();
                    changed = true;
                }
                if (root.GetComponent<StoneGolemDefend>() == null)
                {
                    root.AddComponent<StoneGolemDefend>();
                    changed = true;
                }

                AttackTelegraph telegraph = root.GetComponent<AttackTelegraph>();
                GameObject ring = AssetDatabase.LoadAssetAtPath<GameObject>(RingFxPath);
                if (telegraph != null && ring != null)
                {
                    SerializedObject so = new SerializedObject(telegraph);
                    SerializedProperty ringProp = so.FindProperty("groundRingPrefab");
                    if (ringProp != null && ringProp.objectReferenceValue == null)
                    {
                        ringProp.objectReferenceValue = ring;
                        changed = true;
                    }

                    // 脉冲地面冲击波（攻击预警强化）。
                    SerializedProperty waveProp = so.FindProperty("groundWavePrefab");
                    GameObject wave = AssetDatabase.LoadAssetAtPath<GameObject>(TreantWavePath);
                    if (waveProp != null && wave != null && waveProp.objectReferenceValue == null)
                    {
                        waveProp.objectReferenceValue = wave;
                        changed = true;
                    }

                    so.ApplyModifiedProperties();
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, GolemPrefabPath);
                    Debug.Log("[Combat] StoneGolem.prefab 已挂载 AttackTelegraph + StoneGolemDefend，绑定 FX_Ring 地面圈与脉冲地面波。");
                }
                else
                {
                    Debug.Log("[Combat] StoneGolem.prefab 组件已就绪，跳过。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 给石头人攻击配置资产绑定攻击表现素材：震波石头、碎石、烟尘与地面波。
        static void SetupGolemAttackDefinition()
        {
            StoneGolemAttackDefinition definition =
                AssetDatabase.LoadAssetAtPath<StoneGolemAttackDefinition>(GolemAttackDefinitionPath);
            if (definition == null)
            {
                Debug.LogError($"[Combat] 找不到石头人攻击配置：{GolemAttackDefinitionPath}");
                return;
            }

            SerializedObject so = new SerializedObject(definition);
            int changes = 0;

            // 震波视觉：从旧的发光方块迁移为真实石头模型（保留用户手动改过的其它值）。
            GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockRoundPath);
            GameObject glowCube = AssetDatabase.LoadAssetAtPath<GameObject>(GlowCubeFxPath);
            SerializedProperty shockwaveProp = so.FindProperty("shockwaveVisualPrefab");
            if (shockwaveProp != null && rock != null &&
                (shockwaveProp.objectReferenceValue == null ||
                 shockwaveProp.objectReferenceValue == glowCube))
            {
                shockwaveProp.objectReferenceValue = rock;
                changes++;
            }

            // 碎石模型列表。
            SerializedProperty debrisProp = so.FindProperty("debrisPrefabs");
            if (debrisProp != null && debrisProp.arraySize == 0)
            {
                string[] debrisPaths =
                {
                    "Assets/ArtRes/PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rock_Pebble_01.prefab",
                    "Assets/ArtRes/PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rock_Pebble_02.prefab",
                    "Assets/ArtRes/PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rock_Pebble_03.prefab",
                    "Assets/ArtRes/PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rock_Square_Simple_01.prefab",
                    "Assets/ArtRes/PolygonDungeon/Prefabs/Props/SM_Prop_Brick_01.prefab",
                };
                debrisProp.arraySize = debrisPaths.Length;
                for (int index = 0; index < debrisPaths.Length; index++)
                {
                    debrisProp.GetArrayElementAtIndex(index).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(debrisPaths[index]);
                }
                changes++;
            }

            // 烟尘与地面冲击波。
            SerializedProperty dustProp = so.FindProperty("impactDustPrefab");
            GameObject dust = AssetDatabase.LoadAssetAtPath<GameObject>(DustFxPath);
            if (dustProp != null && dust != null && dustProp.objectReferenceValue == null)
            {
                dustProp.objectReferenceValue = dust;
                changes++;
            }

            SerializedProperty waveProp = so.FindProperty("impactWavePrefab");
            GameObject wave = AssetDatabase.LoadAssetAtPath<GameObject>(TreantWavePath);
            if (waveProp != null && wave != null && waveProp.objectReferenceValue == null)
            {
                waveProp.objectReferenceValue = wave;
                changes++;
            }

            if (changes > 0)
            {
                so.ApplyModifiedProperties();
                Debug.Log($"[Combat] 石头人攻击配置绑定完成：{changes} 项（震波石头/碎石×5/烟尘/地面波）。");
            }
            else
            {
                Debug.Log("[Combat] 石头人攻击配置已就绪，跳过。");
            }
        }

        // ---- Animator API 工具 ----
        static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name && p.type == type))
                return;

            AnimatorControllerParameter existing = Array.Find(
                controller.parameters, p => p.name == name);
            if (existing != null)
            {
                existing.type = type;
                return;
            }

            controller.AddParameter(name, type);
        }

        static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            return machine.states
                .Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name == name);
        }

        static AnimatorState EnsureState(AnimatorStateMachine machine, string name, string clipPath)
        {
            AnimatorState state = FindState(machine, name);
            if (state != null)
                return state;

            AnimationClip clip = LoadSingleClip(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[Combat] 无法从 {clipPath} 加载动画片段。");
                return null;
            }

            state = machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = true;
            return state;
        }

        static AnimationClip LoadSingleClip(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault();
        }

        // 创建带触发条件的过渡（立即触发，不依赖退出时间）。返回是否新增了过渡。
        static bool EnsureTransition(
            AnimatorState source,
            AnimatorState destination,
            string condition,
            float exitTime,
            float duration)
        {
            if (source == null || destination == null)
                return false;

            bool exists = source.transitions.Any(t =>
                t.destinationState == destination &&
                t.conditions.Any(c => c.parameter == condition && c.mode == AnimatorConditionMode.If));
            if (exists)
                return false;

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = exitTime > 0f;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.AddCondition(AnimatorConditionMode.If, 0f, condition);
            return true;
        }

        // 创建纯退出时间过渡（动作播完自动切换）。
        static void EnsureExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            if (source == null || destination == null)
                return;

            bool exists = source.transitions.Any(t =>
                t.destinationState == destination &&
                t.conditions.Length == 0 &&
                t.hasExitTime);
            if (exists)
                return;

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.offset = 0f;
        }
    }
}
