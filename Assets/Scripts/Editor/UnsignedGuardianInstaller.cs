using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>创建剧情任务资产、对话资产与最小场景对象，供作品集 Demo 一键安装。</summary>
public static class UnsignedGuardianInstaller
{
    private const string QuestFolder = "Assets/Resources/Quests";
    private const string DialoguePath = "Assets/Dialogue/Dialogue_UnsignedGuardian.asset";
    private const string ScenePath = "Assets/Scenes/GameScene/Demo 1.unity";

    [MenuItem("工具/未署名的守卫/安装剧情内容")]
    public static void Install()
    {
        Directory.CreateDirectory(QuestFolder);
        CreateQuest(UnsignedGuardianQuestIds.LostPage, "遗失的页码", "找回记录员失落的无署名记录页。", "查找无署名记录页", QuestObjectiveType.Interaction, UnsignedGuardianQuestIds.RecordPage);
        CreateQuest(UnsignedGuardianQuestIds.GuardianTestimony, "守卫的证词", "取得石头人留下的封印铭片。", "击败石头守卫", QuestObjectiveType.EnemyKilled, UnsignedGuardianQuestIds.StoneGolem, "取得封印铭片", QuestObjectiveType.Interaction, UnsignedGuardianQuestIds.SealingInscription);
        DialogueAsset dialogue = CreateDialogue();
        InstallScene(dialogue);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[UnsignedGuardian] 剧情资产与 Demo 1 场景绑定已安装。", dialogue);
    }

    // 创建或覆盖本剧情所需的顺序任务定义。
    private static void CreateQuest(string id, string title, string description, string firstText, QuestObjectiveType firstType, string firstTarget, string secondText = null, QuestObjectiveType secondType = QuestObjectiveType.Interaction, string secondTarget = null)
    {
        string path = QuestFolder + "/Quest_" + id.Replace('.', '_') + ".asset";
        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }
        SerializedObject serialized = new SerializedObject(quest);
        serialized.FindProperty("questId").stringValue = id;
        serialized.FindProperty("title").stringValue = title;
        serialized.FindProperty("description").stringValue = description;
        SerializedProperty objectives = serialized.FindProperty("objectives");
        objectives.arraySize = string.IsNullOrWhiteSpace(secondText) ? 1 : 2;
        ConfigureObjective(objectives.GetArrayElementAtIndex(0), id + ".objective_1", firstType, firstText, firstTarget);
        if (objectives.arraySize > 1)
            ConfigureObjective(objectives.GetArrayElementAtIndex(1), id + ".objective_2", secondType, secondText, secondTarget);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    // 写入单条顺序目标的稳定 ID、事件类型、提示文本和目标 ID。
    private static void ConfigureObjective(SerializedProperty objective, string id, QuestObjectiveType type, string text, string target)
    {
        objective.FindPropertyRelative("objectiveId").stringValue = id;
        objective.FindPropertyRelative("type").enumValueIndex = (int)type;
        objective.FindPropertyRelative("text").stringValue = text;
        objective.FindPropertyRelative("targetId").stringValue = target;
        objective.FindPropertyRelative("requiredCount").intValue = 1;
    }

    // 创建记录员唯一使用的条件分支对话资产。
    private static DialogueAsset CreateDialogue()
    {
        Directory.CreateDirectory("Assets/Dialogue");
        DialogueAsset dialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DialoguePath);
        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogue, DialoguePath);
        }
        dialogue.EnsureDialogueId();
        SerializedObject serialized = new SerializedObject(dialogue);
        serialized.FindProperty("npcName").stringValue = "记录员";
        serialized.FindProperty("completionText").stringValue = "它守着的不是门，是一个不该被留下的名字。";
        serialized.FindProperty("repeatable").boolValue = true;
        SerializedProperty nodes = serialized.FindProperty("nodes");
        nodes.arraySize = 4;
        ConfigureNode(nodes.GetArrayElementAtIndex(0), 1, DialogueSpeaker.Npc, "我只找到一页残稿。深处的石头守卫不让任何人靠近。", -1);
        SerializedProperty choices = nodes.GetArrayElementAtIndex(0).FindPropertyRelative("choices");
        choices.arraySize = 3;
        ConfigureChoice(choices.GetArrayElementAtIndex(0), "我去取回记录。", 2, UnsignedGuardianQuestIds.LostPage, QuestState.Available, DialogueQuestActionType.StartQuest, UnsignedGuardianQuestIds.LostPage, null);
        ConfigureChoice(choices.GetArrayElementAtIndex(1), "记录页提到仪式间。", 3, UnsignedGuardianQuestIds.LostPage, QuestState.ReadyToTurnIn, DialogueQuestActionType.SubmitQuest, UnsignedGuardianQuestIds.LostPage, null);
        AddQuestAction(choices.GetArrayElementAtIndex(1), DialogueQuestActionType.StartQuest, UnsignedGuardianQuestIds.GuardianTestimony, null);
        ConfigureChoice(choices.GetArrayElementAtIndex(2), "交出封印铭片。", 4, UnsignedGuardianQuestIds.GuardianTestimony, QuestState.ReadyToTurnIn, DialogueQuestActionType.SubmitQuest, UnsignedGuardianQuestIds.GuardianTestimony, null);
        ConfigureNode(nodes.GetArrayElementAtIndex(1), 2, DialogueSpeaker.Npc, "不要相信墙上的判词。它们比灰尘新。", -1);
        ConfigureNode(nodes.GetArrayElementAtIndex(2), 3, DialogueSpeaker.Npc, "去仪式间。石头守卫醒着，你得先让它停下。", -1);
        ConfigureNode(nodes.GetArrayElementAtIndex(3), 4, DialogueSpeaker.Npc, "它守着的不是门，是一个不该被留下的名字。", -1);
        serialized.FindProperty("entryNodeId").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
        return dialogue;
    }

    // 向已配置的选择追加第二条任务动作。
    private static void AddQuestAction(SerializedProperty choice, DialogueQuestActionType actionType, string questId, string objectiveId)
    {
        SerializedProperty actions = choice.FindPropertyRelative("questActions");
        int index = actions.arraySize;
        actions.arraySize++;
        SerializedProperty action = actions.GetArrayElementAtIndex(index);
        action.FindPropertyRelative("actionType").enumValueIndex = (int)actionType;
        action.FindPropertyRelative("questId").stringValue = questId;
        action.FindPropertyRelative("objectiveId").stringValue = objectiveId ?? string.Empty;
    }

    // 配置一条台词与其可选任务状态门槛。
    private static void ConfigureNode(SerializedProperty node, int id, DialogueSpeaker speaker, string text, int next, string requiredQuestId = null, QuestState requiredState = QuestState.Available)
    {
        node.FindPropertyRelative("nodeId").intValue = id;
        node.FindPropertyRelative("speaker").enumValueIndex = (int)speaker;
        node.FindPropertyRelative("text").stringValue = text;
        node.FindPropertyRelative("eventId").stringValue = string.Empty;
        node.FindPropertyRelative("nextNodeId").intValue = next;
        node.FindPropertyRelative("requiredQuestId").stringValue = requiredQuestId ?? string.Empty;
        node.FindPropertyRelative("requiredQuestState").enumValueIndex = (int)requiredState;
        node.FindPropertyRelative("questActions").arraySize = 0;
        node.FindPropertyRelative("choices").arraySize = 0;
    }

    // 配置玩家回答、状态条件和单条任务动作。
    private static void ConfigureChoice(SerializedProperty choice, string text, int target, string requiredQuestId, QuestState requiredState, DialogueQuestActionType actionType, string actionQuestId, string objectiveId)
    {
        choice.FindPropertyRelative("text").stringValue = text;
        choice.FindPropertyRelative("targetNodeId").intValue = target;
        choice.FindPropertyRelative("requiredQuestId").stringValue = requiredQuestId ?? string.Empty;
        choice.FindPropertyRelative("requiredQuestState").enumValueIndex = (int)requiredState;
        SerializedProperty actions = choice.FindPropertyRelative("questActions");
        actions.arraySize = 1;
        SerializedProperty action = actions.GetArrayElementAtIndex(0);
        action.FindPropertyRelative("actionType").enumValueIndex = (int)actionType;
        action.FindPropertyRelative("questId").stringValue = actionQuestId;
        action.FindPropertyRelative("objectiveId").stringValue = objectiveId ?? string.Empty;
    }

    // 打开 Demo 1，绑定记录员，并建立可放置的交互物和任务上报器。
    private static void InstallScene(DialogueAsset dialogue)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject npc = GameObject.Find("NPC_1");
        if (npc == null)
            throw new System.InvalidOperationException("Demo 1 缺少 NPC_1，无法安装记录员对话。");
        NpcDialogueInteractor interactor = npc.GetComponent<NpcDialogueInteractor>() ?? npc.AddComponent<NpcDialogueInteractor>();
        SerializedObject interactorSerialized = new SerializedObject(interactor);
        interactorSerialized.FindProperty("dialogue").objectReferenceValue = dialogue;
        interactorSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interactor);
        CreateReporterObject("无署名记录页", npc.transform.position + npc.transform.forward * 3f, QuestObjectiveType.Interaction, UnsignedGuardianQuestIds.RecordPage);
        GameObject golem = FindOrCreateGolem(npc);
        ConfigureReporter(golem, QuestObjectiveType.EnemyKilled, UnsignedGuardianQuestIds.StoneGolem);
        CreateReporterObject("封印铭片", golem.transform.position + golem.transform.right * 2f, QuestObjectiveType.Interaction, UnsignedGuardianQuestIds.SealingInscription);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 创建或复用带碰撞体的任务交互物，便于后续美术直接替换模型。
    private static void CreateReporterObject(string name, Vector3 position, QuestObjectiveType type, string targetId)
    {
        GameObject target = GameObject.Find(name);
        if (target == null)
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            target.transform.localScale = Vector3.one * 0.45f;
        }
        ConfigureReporter(target, type, targetId);
    }

    // 将目标 ID 和事件类型写入可复用世界上报器。
    private static void ConfigureReporter(GameObject target, QuestObjectiveType type, string targetId)
    {
        QuestWorldReporter reporter = target.GetComponent<QuestWorldReporter>() ?? target.AddComponent<QuestWorldReporter>();
        SerializedObject serialized = new SerializedObject(reporter);
        serialized.FindProperty("targetId").stringValue = targetId;
        serialized.FindProperty("eventType").enumValueIndex = (int)type;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(reporter);
    }

    // 从场景中优先查找具体石头人组件。
    private static GameObject FindGolem()
    {
        StoneGolem golem = Object.FindFirstObjectByType<StoneGolem>(FindObjectsInactive.Include);
        return golem != null ? golem.gameObject : null;
    }

    // 优先复用场景石头人；缺失时在仪式间实例化项目现有预制体。
    private static GameObject FindOrCreateGolem(GameObject fallback)
    {
        GameObject existing = FindGolem();
        if (existing != null)
            return existing;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Enemy/StoneGolem.prefab");
        if (prefab == null)
            throw new System.InvalidOperationException("缺少 StoneGolem.prefab，无法配置第二任务。");
        GameObject golem = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        GameObject ritual = GameObject.Find("Ritual");
        golem.name = "未署名的守卫";
        golem.transform.position = ritual != null ? ritual.transform.position : fallback.transform.position + fallback.transform.forward * 8f;
        return golem;
    }

    // 将剧情场景登记为构建入口之后的可玩场景。
    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == ScenePath)
                return;
        var expanded = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(expanded, 0);
        expanded[expanded.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = expanded;
    }
}
