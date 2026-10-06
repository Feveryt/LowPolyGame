using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace LowPolyGame.EditorTools
{
    // 龙 BOSS 一键装配：
    //   1. 从资源包控制器提取动画片段，构建专属战斗控制器（Idle/移动/四招/受击/死亡）。
    //   2. 生成数值、敌人配置与攻击配置三类资产。
    //   3. 组装 BOSS 预制体（模型 + 碰撞 + 导航 + 战斗组件）。
    //   4. 摆放进 Demo 1 场景并重建 NavMesh。
    // 全部操作幂等：重复执行只更新已存在的资产与对象。
    public static class BossCombatSetup
    {
        const string PackControllerPath = "Assets/ArtRes/Forest Creatures Pack/Fantasy Dragon/Controller/Fantasy Dragon.controller";
        const string DragonModelFbx = "Assets/ArtRes/Forest Creatures Pack/Fantasy Dragon/FBX/Fantasy Dragon.FBX";
        const string FireBreathVfxPath = "Assets/ArtRes/Forest Creatures Pack/Fantasy Dragon/Prefabs/VFX-Fire Breath.prefab";
        const string RingFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Ring.prefab";
        const string GlowCubeFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Glow_Cube.prefab";
        const string SparklesFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Sparkles.prefab";
        const string DustFxPath = "Assets/ArtRes/PolygonDungeon/Prefabs/FX/FX_Dust_Large_Soft.prefab";
        const string WaveFxPath = "Assets/ArtRes/Forest Creatures Pack/Treant Guard/Prefabs/Shockwave-Yellow.prefab";

        const string ControllerPath = "Assets/ArtRes/Animator/DragonBoss.controller";
        const string StatsAssetPath = "Assets/GameData/Definitions/Characters/DragonBoss/CharacterStats_DragonBoss.asset";
        const string EnemyConfigAssetPath = "Assets/GameData/Definitions/Characters/DragonBoss/Enemy_DragonBoss.asset";
        const string AttackDefinitionAssetPath = "Assets/GameData/Definitions/Characters/DragonBoss/DragonBossAttack_DragonBoss.asset";
        const string PrefabPath = "Assets/Resources/Prefabs/Enemy/DragonBoss.prefab";

        [MenuItem("Tools/Combat/3. Build Dragon Boss (All-in-One)")]
        public static void BuildDragonBoss()
        {
            // 创建资产前确保目标目录存在（CreateAsset 不会自动建目录）。
            string definitionsFolder = "Assets/GameData/Definitions/Characters/DragonBoss";
            if (!System.IO.Directory.Exists(definitionsFolder))
                System.IO.Directory.CreateDirectory(definitionsFolder);

            AnimatorController controller = BuildController();
            CharacterStatsDefinition stats = CreateStatsAsset();
            DragonBossAttackDefinition attack = CreateAttackAsset();
            EnemyConfig config = CreateEnemyConfig(stats);
            GameObject prefab = BuildPrefab(controller, attack, config);
            PlaceInScene(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Combat] 龙 BOSS 装配完成：控制器 / 资产 / 预制体 / 场景入场 / NavMesh 重建。");
        }

        // ---- 控制器 ----
        static AnimatorController BuildController()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorController controller = existing;
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorControllerParameterType trigger = AnimatorControllerParameterType.Trigger;
            AnimatorControllerParameterType boolean = AnimatorControllerParameterType.Bool;
            AnimatorControllerParameterType floatType = AnimatorControllerParameterType.Float;

            EnsureParameter(controller, "WalkForward", boolean);
            EnsureParameter(controller, "StrafeLeft", boolean);
            EnsureParameter(controller, "StrafeRight", boolean);
            EnsureParameter(controller, "MoveSpeed", floatType);
            EnsureParameter(controller, "Bite", trigger);
            EnsureParameter(controller, "Fireball", trigger);
            EnsureParameter(controller, "SpellAoE", trigger);
            EnsureParameter(controller, "FireBreath", trigger);
            EnsureParameter(controller, "TakeDamage", trigger);
            EnsureParameter(controller, "Die", trigger);

            AnimatorState idle = EnsureState(machine, "Idle", "Ground Idle");
            AnimatorState walkForward = EnsureState(machine, "WalkForward", "Walk Forward In Place");
            // 走路动画用 MoveSpeed 参数驱动播放速度（奔跑时 1.6 倍速近似小跑）。
            if (walkForward != null)
                walkForward.speedParameter = "MoveSpeed";
            AnimatorState strafeLeft = EnsureState(machine, "StrafeLeft", "Strafe Left In Place");
            AnimatorState strafeRight = EnsureState(machine, "StrafeRight", "Strafe Right In Place");
            AnimatorState bite = EnsureState(machine, "Bite", "Ground Bite Attack");
            AnimatorState fireball = EnsureState(machine, "Fireball", "Ground Projectile Attack");
            AnimatorState spellAoE = EnsureState(machine, "SpellAoE", "Ground Spell Cast");
            AnimatorState fireBreath = EnsureState(machine, "FireBreath", "FireBreathOnce");
            AnimatorState hurt = EnsureState(machine, "Hurt", "Ground Take Damage");
            AnimatorState die = EnsureState(machine, "Die", "Ground Die");

            if (machine.defaultState == null)
                machine.defaultState = idle;

            // 移动树：Idle <-> 移动状态。
            EnsureBoolTransition(idle, walkForward, "WalkForward");
            EnsureBoolTransition(walkForward, idle, "WalkForward", expected: false);
            EnsureBoolTransition(idle, strafeLeft, "StrafeLeft");
            EnsureBoolTransition(strafeLeft, idle, "StrafeLeft", expected: false);
            EnsureBoolTransition(idle, strafeRight, "StrafeRight");
            EnsureBoolTransition(strafeRight, idle, "StrafeRight", expected: false);

            // 攻击：Idle/移动 -> 攻击状态 -> 回 Idle。
            AnimatorState[] locomotionStates = { idle, walkForward, strafeLeft, strafeRight };
            foreach (AnimatorState state in locomotionStates)
            {
                EnsureTriggerTransition(state, bite, "Bite", 0.12f);
                EnsureTriggerTransition(state, fireball, "Fireball", 0.12f);
                EnsureTriggerTransition(state, spellAoE, "SpellAoE", 0.12f);
                EnsureTriggerTransition(state, fireBreath, "FireBreath", 0.15f);
            }

            EnsureExitTransition(bite, idle, 0.95f, 0.2f);
            EnsureExitTransition(fireball, idle, 0.95f, 0.2f);
            EnsureExitTransition(spellAoE, idle, 0.95f, 0.2f);
            EnsureExitTransition(fireBreath, idle, 0.98f, 0.25f);

            // 受击与死亡。
            foreach (AnimatorState state in locomotionStates.Concat(new[] { bite, fireball, spellAoE, fireBreath }))
            {
                EnsureTriggerTransition(state, hurt, "TakeDamage", 0.08f);
            }
            EnsureExitTransition(hurt, idle, 0.95f, 0.15f);
            EnsureTriggerTransition(idle, die, "Die", 0.15f);
            EnsureTriggerTransition(hurt, die, "Die", 0.1f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ---- 配置资产 ----
        static CharacterStatsDefinition CreateStatsAsset()
        {
            CharacterStatsDefinition stats =
                AssetDatabase.LoadAssetAtPath<CharacterStatsDefinition>(StatsAssetPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
                AssetDatabase.CreateAsset(stats, StatsAssetPath);
            }

            SerializedObject so = new SerializedObject(stats);
            SetInt(so, "id", 2001);
            SetString(so, "displayName", "深渊守卫龙");
            SetInt(so, "maxHealth", 320);
            SetInt(so, "attack", 34);
            SetInt(so, "defense", 12);
            SetFloat(so, "maxStamina", 200f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stats);
            return stats;
        }

        static DragonBossAttackDefinition CreateAttackAsset()
        {
            DragonBossAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<DragonBossAttackDefinition>(AttackDefinitionAssetPath);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<DragonBossAttackDefinition>();
                AssetDatabase.CreateAsset(attack, AttackDefinitionAssetPath);
            }

            SerializedObject so = new SerializedObject(attack);
            SerializedProperty fireballVisual = so.FindProperty("fireballVisualPrefab");
            if (fireballVisual != null && fireballVisual.objectReferenceValue == null)
            {
                GameObject glow = AssetDatabase.LoadAssetAtPath<GameObject>(GlowCubeFxPath);
                if (glow != null)
                    fireballVisual.objectReferenceValue = glow;
            }

            // 火球命中爆花与地火落地表现（预警强化）。
            BindIfEmpty(so, "fireballImpactPrefab", SparklesFxPath);
            BindIfEmpty(so, "spellAoEWavePrefab", WaveFxPath);
            BindIfEmpty(so, "spellAoEDustPrefab", DustFxPath);
            BindIfEmpty(so, "fireBreathVfxPrefab", FireBreathVfxPath);

            // 可受击目标层沿用石头人配置，保证命中一致。
            StoneGolemAttackDefinition golemDefinition =
                AssetDatabase.LoadAssetAtPath<StoneGolemAttackDefinition>(
                    "Assets/GameData/Definitions/Characters/StoneGolem/StoneGolemAttack_StoneGolem.asset");
            if (golemDefinition != null)
            {
                SerializedProperty mask = so.FindProperty("damageableMask");
                if (mask != null)
                    mask.intValue = golemDefinition.DamageableMask;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            return attack;
        }

        // 仅在属性为空时绑定资产。
        static void BindIfEmpty(SerializedObject so, string propertyName, string assetPath)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.objectReferenceValue != null)
                return;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset != null)
                prop.objectReferenceValue = asset;
        }

        static EnemyConfig CreateEnemyConfig(CharacterStatsDefinition stats)
        {
            EnemyConfig config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(EnemyConfigAssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnemyConfig>();
                AssetDatabase.CreateAsset(config, EnemyConfigAssetPath);
            }

            SerializedObject so = new SerializedObject(config);
            SerializedProperty statsProp = so.FindProperty("statsDefinition");
            if (statsProp != null && statsProp.objectReferenceValue == null)
                statsProp.objectReferenceValue = stats;

            SetFloat(so, "detectionRange", 14f);
            SetFloat(so, "loseTargetRange", 24f);
            SetFloat(so, "chaseSpeed", 3.8f);
            SetFloat(so, "returnSpeed", 3f);
            SetFloat(so, "acceleration", 12f);
            SetFloat(so, "angularSpeed", 180f);
            SetFloat(so, "returnArrivalDistance", 0.5f);
            SetFloat(so, "hurtFallbackDuration", 0.35f);
            SetFloat(so, "deathDespawnTimeout", 8f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        // ---- 预制体 ----
        static GameObject BuildPrefab(
            AnimatorController controller,
            DragonBossAttackDefinition attack,
            EnemyConfig config)
        {
            GameObject root;
            bool isNew = false;

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
            }
            else
            {
                root = new GameObject("DragonBoss");
                isNew = true;
            }

            try
            {
                // 模型：首次创建时以 FBX 为子物体挂入；再次执行时跳过。
                if (root.transform.Find("Model") == null)
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DragonModelFbx);
                    if (model == null)
                    {
                        Debug.LogError("[Combat] 找不到龙的模型 FBX。");
                        return null;
                    }

                    GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    modelInstance.name = "Model";
                }

                // 数值与宿主组件。
                EnemyStats enemyStats = GetOrAdd<EnemyStats>(root);
                DragonBoss boss = GetOrAdd<DragonBoss>(root);
                EnemyAI enemyAi = GetOrAdd<EnemyAI>(root);
                AssignEnemyConfig(root, config);

                // 导航。
                NavMeshAgent agent = GetOrAdd<NavMeshAgent>(root);
                agent.radius = Mathf.Max(1.4f, agent.radius);
                agent.height = 2.4f;
                agent.baseOffset = 0f;

                // 身体碰撞（受击判定由玩家的武器命中层过滤）。
                if (root.GetComponent<CapsuleCollider>() == null)
                {
                    CapsuleCollider body = root.AddComponent<CapsuleCollider>();
                    body.center = new Vector3(0f, 1.4f, 0f);
                    body.radius = 1.2f;
                    body.height = 2.8f;
                }

                // 攻击组件与引用。
                DragonBossAttack bossAttack = GetOrAdd<DragonBossAttack>(root);
                AttackTelegraph telegraph = GetOrAdd<AttackTelegraph>(root);
                GameObject ring = AssetDatabase.LoadAssetAtPath<GameObject>(RingFxPath);
                if (telegraph != null && ring != null)
                {
                    SerializedObject so = new SerializedObject(telegraph);
                    SerializedProperty ringProp = so.FindProperty("groundRingPrefab");
                    if (ringProp != null && ringProp.objectReferenceValue == null)
                        ringProp.objectReferenceValue = ring;

                    // 脉冲地面冲击波（预警强化，运行时按招式颜色染色）。
                    SerializedProperty waveProp = so.FindProperty("groundWavePrefab");
                    GameObject wave = AssetDatabase.LoadAssetAtPath<GameObject>(WaveFxPath);
                    if (waveProp != null && wave != null && waveProp.objectReferenceValue == null)
                        waveProp.objectReferenceValue = wave;

                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                SerializedObject attackSo = new SerializedObject(bossAttack);
                SerializedProperty definitionProp = attackSo.FindProperty("definition");
                if (definitionProp != null)
                    definitionProp.objectReferenceValue = attack;
                attackSo.ApplyModifiedPropertiesWithoutUndo();

                // 动画：复用模型自带的 Animator（FBX 实例自带），指向 BOSS 控制器。
                Transform modelTransform = root.transform.Find("Model");
                Animator modelAnimator = modelTransform != null
                    ? modelTransform.GetComponentInChildren<Animator>(true)
                    : null;
                if (modelAnimator == null)
                    modelAnimator = GetOrAdd<Animator>(root);
                modelAnimator.runtimeAnimatorController = controller;
                modelAnimator.applyRootMotion = false;

                GameObject animatorHost = modelAnimator.gameObject;
                DragonBossAnimation animationBehaviour = animatorHost.GetComponent<DragonBossAnimation>();
                if (animationBehaviour == null)
                    animationBehaviour = animatorHost.AddComponent<DragonBossAnimation>();

                // 层级对齐石头人（玩家武器命中层过滤依赖敌方层级）。
                StoneGolemAttackDefinition golemDefinition =
                    AssetDatabase.LoadAssetAtPath<StoneGolemAttackDefinition>(
                        "Assets/GameData/Definitions/Characters/StoneGolem/StoneGolemAttack_StoneGolem.asset");
                GameObject golemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Enemy/StoneGolem.prefab");
                if (golemPrefab != null)
                {
                    int enemyLayer = golemPrefab.layer;
                    SetLayerRecursive(root, enemyLayer);
                }

                if (isNew)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    UnityEngine.Object.DestroyImmediate(root);
                    Debug.Log("[Combat] 龙 BOSS 预制体已创建。");
                    return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Combat] 龙 BOSS 预制体已更新。");
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            finally
            {
                if (!isNew)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // EnemyBase.config 序列化字段赋值。
        static void AssignEnemyConfig(GameObject root, EnemyConfig config)
        {
            EnemyBase boss = root.GetComponent<EnemyBase>();
            if (boss == null)
                return;

            SerializedObject so = new SerializedObject(boss);
            SerializedProperty prop = so.FindProperty("config");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- 场景入场 ----
        static void PlaceInScene(GameObject prefab)
        {
            if (prefab == null)
                return;

            // 清理历史遗留的未配置 DragonBoss（如装配中途失败留下的半成品）。
            DragonBoss existing = UnityEngine.Object.FindFirstObjectByType<DragonBoss>();
            if (existing != null)
            {
                if (existing.Config == null)
                {
                    Debug.LogWarning("[Combat] 发现未配置的遗留龙 BOSS 对象，已删除重建。");
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }
                else
                {
                    Debug.Log("[Combat] 场景中已存在配置完整的龙 BOSS，跳过摆放。");
                    return;
                }
            }

            // 摆放策略：以场景中的石头人为锚点向斜后方偏移 30 米，并吸附到 NavMesh。
            Vector3 anchor = Vector3.zero;
            StoneGolem golem = UnityEngine.Object.FindFirstObjectByType<StoneGolem>();
            if (golem != null)
                anchor = golem.transform.position;

            Vector3 candidate = anchor + new Vector3(24f, 0f, -24f);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                candidate = hit.position;

            GameObject dragon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            dragon.transform.position = candidate;
            Undo.RegisterCreatedObjectUndo(dragon, "Place Dragon Boss");

            // 重建 NavMesh 覆盖 BOSS 巡逻范围。
            NavMeshSurface surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null)
            {
                surface.BuildNavMesh();
                Debug.Log("[Combat] NavMesh 已重建。");
            }
            else
            {
                Debug.LogWarning("[Combat] 场景中没有 NavMeshSurface，请手动烘焙导航。");
            }

            Debug.Log($"[Combat] 龙 BOSS 已放置于 {candidate}（锚点：石头人位置向西北 24 米）。");
        }

        // ---- 工具 ----
        static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        // 递归设置对象及其子物体的层级。
        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name && p.type == type))
                return;

            AnimatorControllerParameter existing = Array.Find(controller.parameters, p => p.name == name);
            if (existing != null)
            {
                existing.type = type;
                return;
            }

            controller.AddParameter(name, type);
        }

        // 从资源包控制器或独立动画 FBX 中解析指定名称的片段。
        static AnimationClip ResolveClip(string clipName)
        {
            AnimatorController packController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PackControllerPath);
            if (packController != null)
            {
                foreach (AnimatorControllerLayer layer in packController.layers)
                {
                    foreach (ChildAnimatorState child in layer.stateMachine.states)
                    {
                        if (child.state != null && child.state.name == clipName &&
                            child.state.motion is AnimationClip clip)
                            return clip;
                    }
                }
            }

            string fbxPath = $"{GetFbxFolder()}/Fantasy Dragon@{clipName}.FBX";
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault();
        }

        static string fbxFolder;
        static string GetFbxFolder()
        {
            if (string.IsNullOrEmpty(fbxFolder))
                fbxFolder = System.IO.Path.GetDirectoryName(DragonModelFbx)?.Replace('\\', '/');
            return fbxFolder;
        }

        static AnimatorState EnsureState(AnimatorStateMachine machine, string stateName, string clipName)
        {
            AnimatorState state = machine.states
                .Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name == stateName);
            if (state != null)
                return state;

            AnimationClip clip = ResolveClip(clipName);
            if (clip == null)
            {
                Debug.LogError($"[Combat] 无法解析龙的动画片段：{clipName}");
                return null;
            }

            state = machine.AddState(stateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            state.speed = 1f;
            return state;
        }

        static void EnsureTriggerTransition(
            AnimatorState source,
            AnimatorState destination,
            string condition,
            float duration)
        {
            if (source == null || destination == null)
                return;

            bool exists = source.transitions.Any(t =>
                t.destinationState == destination &&
                t.conditions.Any(c => c.parameter == condition && c.mode == AnimatorConditionMode.If));
            if (exists)
                return;

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(AnimatorConditionMode.If, 0f, condition);
        }

        static void EnsureBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string condition,
            bool expected = true)
        {
            if (source == null || destination == null)
                return;

            AnimatorConditionMode mode = expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
            bool exists = source.transitions.Any(t =>
                t.destinationState == destination &&
                t.conditions.Any(c => c.parameter == condition && c.mode == mode));
            if (exists)
                return;

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(mode, 0f, condition);
        }

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
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.floatValue = value;
        }

        static void SetString(SerializedObject so, string name, string value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.stringValue = value;
        }
    }
}
