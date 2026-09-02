using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 顺序列表式对话编辑窗口。
/// 提供角色资产创建、台词添加、头像资料配置和发布前数据校验。
/// </summary>
public sealed class DialogueEditorWindow : EditorWindow
{
    // 当前编辑的 NPC 对话资产。
    private DialogueAsset dialogue;
    // 最近一次校验的结果。
    private string validationReport;
    // 校验结果是否包含错误。
    private bool hasErrors;
    // 编辑器内容区域的垂直滚动位置。
    private Vector2 scrollPosition;

    [MenuItem("工具/对话编辑器")]
    private static void Open()
    {
        GetWindow<DialogueEditorWindow>("对话编辑器");
    }

    [MenuItem("工具/对话/清除已完成进度")]
    private static void ClearCompletedProgress()
    {
        DialogueProgressStore.ClearAll();
        Debug.Log("[Dialogue] 已清除全部对话完成进度。");
    }

    // 绘制资产入口、玩家头像资料、NPC 对话内容和校验结果。
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("对话编辑器", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        dialogue = (DialogueAsset)EditorGUILayout.ObjectField("对话资产", dialogue, typeof(DialogueAsset), false);
        if (GUILayout.Button("新建 NPC 对话", GUILayout.Width(120f)))
            CreateDialogueAsset();
        using (new EditorGUI.DisabledScope(dialogue == null))
        {
            if (GUILayout.Button("删除当前对话", GUILayout.Width(120f)))
                DeleteDialogueAsset();
        }
        EditorGUILayout.EndHorizontal();

        DrawPresentationSettings();
        if (dialogue == null)
        {
            EditorGUILayout.HelpBox("请新建或选择一个对话资产后开始编辑。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawDialogue();
        EditorGUILayout.EndScrollView();
    }

    // 显示全局玩家头像资产的创建与选中入口。
    private static void DrawPresentationSettings()
    {
        DialoguePresentationSettings settings = FindPresentationSettings();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("玩家头像资料", EditorStyles.boldLabel);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("请创建玩家展示设置，并在 Inspector 中绑定玩家头像及左右位置。", MessageType.Warning);
            if (GUILayout.Button("创建展示设置"))
                CreatePresentationSettings();
        }
        else
        {
            EditorGUILayout.ObjectField("展示设置", settings, typeof(DialoguePresentationSettings), false);
            if (GUILayout.Button("选中展示设置"))
                Selection.activeObject = settings;
        }
        EditorGUILayout.EndVertical();
    }

    // 使用序列化字段绘制 NPC 资料、节点和完成栏内容。
    private void DrawDialogue()
    {
        var serialized = new SerializedObject(dialogue);
        serialized.Update();
        SerializedProperty npcName = serialized.FindProperty("npcName");
        SerializedProperty npcPortrait = serialized.FindProperty("npcPortrait");
        SerializedProperty npcPortraitSide = serialized.FindProperty("npcPortraitSide");
        SerializedProperty entryNodeId = serialized.FindProperty("entryNodeId");
        SerializedProperty nodes = serialized.FindProperty("nodes");
        SerializedProperty completionText = serialized.FindProperty("completionText");
        SerializedProperty completionEventId = serialized.FindProperty("completionEventId");
        SerializedProperty repeatable = serialized.FindProperty("repeatable");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("NPC 资料", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(npcName, new GUIContent("NPC 名称"));
        EditorGUILayout.PropertyField(npcPortrait, new GUIContent("NPC 头像"));
        EditorGUILayout.PropertyField(npcPortraitSide, new GUIContent("NPC 头像位置"));
        EditorGUILayout.PropertyField(completionText, new GUIContent("完成后的重复台词"));
        EditorGUILayout.PropertyField(completionEventId, new GUIContent("完成事件 ID"));
        EditorGUILayout.PropertyField(repeatable, new GUIContent("允许重复进入分支"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加 NPC 台词"))
            AddLine(serialized, DialogueSpeaker.Npc);
        if (GUILayout.Button("添加玩家台词"))
            AddLine(serialized, DialogueSpeaker.Player);
        if (GUILayout.Button("校验对话"))
            Validate(serialized);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "节点按列表顺序添加。每个节点的“下一节点 ID”和 NPC 节点选项的“目标节点 ID”都填写目标节点编号；-1 表示结束。为 NPC 节点添加玩家选项即可显示回答按钮。",
            MessageType.None);
        EditorGUILayout.PropertyField(entryNodeId, new GUIContent("入口节点 ID"));
        if (DrawNodes(serialized, nodes))
            return;

        if (!string.IsNullOrWhiteSpace(validationReport))
            EditorGUILayout.HelpBox(validationReport, hasErrors ? MessageType.Error : MessageType.Info);

        if (serialized.ApplyModifiedProperties())
        {
            dialogue.EnsureDialogueId();
            EditorUtility.SetDirty(dialogue);
            validationReport = string.Empty;
        }
    }

    // 将节点逐项展示，选项列表仅对 NPC 节点开放。
    private bool DrawNodes(SerializedObject serialized, SerializedProperty nodes)
    {
        int nodeToDelete = -1;
        for (int index = 0; index < nodes.arraySize; index++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(index);
            SerializedProperty id = node.FindPropertyRelative("nodeId");
            SerializedProperty speaker = node.FindPropertyRelative("speaker");
            SerializedProperty text = node.FindPropertyRelative("text");
            SerializedProperty eventId = node.FindPropertyRelative("eventId");
            SerializedProperty requiredQuestId = node.FindPropertyRelative("requiredQuestId");
            SerializedProperty requiredQuestState = node.FindPropertyRelative("requiredQuestState");
            SerializedProperty questActions = node.FindPropertyRelative("questActions");
            SerializedProperty next = node.FindPropertyRelative("nextNodeId");
            SerializedProperty choices = node.FindPropertyRelative("choices");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"节点 #{id.intValue}", EditorStyles.boldLabel);
            if (GUILayout.Button("删除节点", GUILayout.Width(80f)))
                nodeToDelete = index;
            EditorGUILayout.EndHorizontal();
            speaker.enumValueIndex = EditorGUILayout.Popup(
                "发言者",
                speaker.enumValueIndex,
                new[] { "玩家", "NPC" });
            EditorGUILayout.PropertyField(text, new GUIContent("台词内容"));
            EditorGUILayout.PropertyField(eventId, new GUIContent("事件 ID"));
            DrawQuestCondition(requiredQuestId, requiredQuestState);
            DrawQuestActions(questActions);
            if ((DialogueSpeaker)speaker.enumValueIndex == DialogueSpeaker.Npc)
                DrawChoices(choices);
            else if (choices.arraySize > 0)
                EditorGUILayout.HelpBox("玩家节点的“玩家选项”不会在运行时使用。", MessageType.Warning);
            EditorGUILayout.PropertyField(next, new GUIContent("下一节点 ID"));
            EditorGUILayout.EndVertical();
        }

        if (nodeToDelete < 0)
            return false;

        DeleteNode(serialized, nodeToDelete);
        return true;
    }

    // 绘制 NPC 节点的玩家回答选项，并支持独立添加和删除。
    private static void DrawChoices(SerializedProperty choices)
    {
        choices.isExpanded = EditorGUILayout.Foldout(
            choices.isExpanded,
            $"玩家选项（{choices.arraySize}）",
            true);
        if (!choices.isExpanded)
            return;

        int choiceToDelete = -1;
        EditorGUI.indentLevel++;
        for (int index = 0; index < choices.arraySize; index++)
        {
            SerializedProperty choice = choices.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"选项 #{index + 1}", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("删除选项", GUILayout.Width(80f)))
                choiceToDelete = index;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("text"), new GUIContent("回答文本"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("targetNodeId"), new GUIContent("目标节点 ID"));
            DrawQuestCondition(choice.FindPropertyRelative("requiredQuestId"), choice.FindPropertyRelative("requiredQuestState"));
            DrawQuestActions(choice.FindPropertyRelative("questActions"));
            EditorGUILayout.EndVertical();
        }

        if (choiceToDelete >= 0)
            choices.DeleteArrayElementAtIndex(choiceToDelete);

        if (GUILayout.Button("添加玩家选项"))
        {
            choices.InsertArrayElementAtIndex(choices.arraySize);
            SerializedProperty choice = choices.GetArrayElementAtIndex(choices.arraySize - 1);
            choice.FindPropertyRelative("text").stringValue = "新的玩家回答";
            choice.FindPropertyRelative("targetNodeId").intValue = -1;
        }
        EditorGUI.indentLevel--;
    }

    // 绘制节点或选项的可选任务状态门槛。
    private static void DrawQuestCondition(SerializedProperty questId, SerializedProperty questState)
    {
        EditorGUILayout.PropertyField(questId, new GUIContent("所需任务 ID"));
        if (!string.IsNullOrWhiteSpace(questId.stringValue))
            EditorGUILayout.PropertyField(questState, new GUIContent("所需任务状态"));
    }

    // 绘制进入节点或选择回答时按顺序执行的任务动作。
    private static void DrawQuestActions(SerializedProperty actions)
    {
        actions.isExpanded = EditorGUILayout.Foldout(actions.isExpanded, $"任务动作（{actions.arraySize}）", true);
        if (!actions.isExpanded)
            return;
        for (int index = 0; index < actions.arraySize; index++)
        {
            SerializedProperty action = actions.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(action.FindPropertyRelative("actionType"), new GUIContent("动作"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("questId"), new GUIContent("任务 ID"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("objectiveId"), new GUIContent("目标 ID"));
            if (GUILayout.Button("删除任务动作"))
            {
                actions.DeleteArrayElementAtIndex(index);
                break;
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("添加任务动作"))
            actions.InsertArrayElementAtIndex(actions.arraySize);
    }

    // 删除指定节点，并清理入口和其他节点中指向它的跳转引用。
    private void DeleteNode(SerializedObject serialized, int index)
    {
        SerializedProperty nodes = serialized.FindProperty("nodes");
        if (index < 0 || index >= nodes.arraySize)
            return;

        int removedId = nodes.GetArrayElementAtIndex(index).FindPropertyRelative("nodeId").intValue;
        if (!EditorUtility.DisplayDialog(
                "删除对话节点",
                $"确定删除节点 #{removedId} 吗？所有指向该节点的跳转都会改为结束对话。",
                "删除",
                "取消"))
            return;

        Undo.RecordObject(dialogue, "删除对话节点");
        SerializedProperty entry = serialized.FindProperty("entryNodeId");
        for (int nodeIndex = 0; nodeIndex < nodes.arraySize; nodeIndex++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(nodeIndex);
            SerializedProperty next = node.FindPropertyRelative("nextNodeId");
            if (next.intValue == removedId)
                next.intValue = -1;

            SerializedProperty choices = node.FindPropertyRelative("choices");
            for (int choiceIndex = 0; choiceIndex < choices.arraySize; choiceIndex++)
            {
                SerializedProperty target = choices.GetArrayElementAtIndex(choiceIndex)
                    .FindPropertyRelative("targetNodeId");
                if (target.intValue == removedId)
                    target.intValue = -1;
            }
        }

        if (entry.intValue == removedId)
        {
            entry.intValue = nodes.arraySize > 1
                ? nodes.GetArrayElementAtIndex(index == 0 ? 1 : 0).FindPropertyRelative("nodeId").intValue
                : -1;
        }

        int sizeBeforeDelete = nodes.arraySize;
        nodes.DeleteArrayElementAtIndex(index);
        if (nodes.arraySize == sizeBeforeDelete)
            nodes.DeleteArrayElementAtIndex(index);

        serialized.ApplyModifiedProperties();
        dialogue.EnsureDialogueId();
        EditorUtility.SetDirty(dialogue);
        validationReport = string.Empty;
    }

    // 新增一条线性台词，自动连到此前无选项的末尾节点。
    private void AddLine(SerializedObject serialized, DialogueSpeaker speaker)
    {
        SerializedProperty nodes = serialized.FindProperty("nodes");
        SerializedProperty entry = serialized.FindProperty("entryNodeId");
        Undo.RecordObject(dialogue, "添加对话台词");
        int newId = GetNextId(nodes);
        int previousIndex = nodes.arraySize - 1;
        nodes.InsertArrayElementAtIndex(nodes.arraySize);
        SerializedProperty node = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
        node.FindPropertyRelative("nodeId").intValue = newId;
        node.FindPropertyRelative("speaker").enumValueIndex = (int)speaker;
        node.FindPropertyRelative("text").stringValue = speaker == DialogueSpeaker.Npc ? "新的 NPC 台词" : "新的玩家台词";
        node.FindPropertyRelative("eventId").stringValue = string.Empty;
        node.FindPropertyRelative("nextNodeId").intValue = -1;
        node.FindPropertyRelative("choices").ClearArray();
        if (previousIndex >= 0)
        {
            SerializedProperty previous = nodes.GetArrayElementAtIndex(previousIndex);
            if (previous.FindPropertyRelative("nextNodeId").intValue < 0 &&
                previous.FindPropertyRelative("choices").arraySize == 0)
                previous.FindPropertyRelative("nextNodeId").intValue = newId;
        }
        if (entry.intValue < 0)
            entry.intValue = newId;
        serialized.ApplyModifiedProperties();
        dialogue.EnsureDialogueId();
        EditorUtility.SetDirty(dialogue);
    }

    // 校验节点标识、文本、入口、链接、可达性和循环。
    private void Validate(SerializedObject serialized)
    {
        serialized.ApplyModifiedProperties();
        var messages = new List<string>();
        var nodesById = new Dictionary<int, DialogueNode>();
        foreach (DialogueNode node in dialogue.Nodes)
        {
            if (!nodesById.TryAdd(node.NodeId, node))
                messages.Add($"重复节点 ID: {node.NodeId}");
            if (string.IsNullOrWhiteSpace(node.Text))
                messages.Add($"节点 #{node.NodeId} 的文本为空。");
            ValidateQuestReferences(node.RequiredQuestId, node.QuestActions, $"节点 #{node.NodeId}", messages);
            foreach (DialogueChoice choice in node.Choices)
                ValidateQuestReferences(choice.RequiredQuestId, choice.QuestActions, $"节点 #{node.NodeId} 的选项", messages);
        }

        if (dialogue.NpcPortrait == null)
            messages.Add("提示：NPC 头像未绑定。");
        if (!nodesById.ContainsKey(dialogue.EntryNodeId))
            messages.Add("入口节点不存在。");
        else
        {
            var reachable = new HashSet<int>();
            var visiting = new HashSet<int>();
            ValidateNode(dialogue.EntryNodeId, nodesById, reachable, visiting, messages);
            foreach (int id in nodesById.Keys)
            {
                if (!reachable.Contains(id))
                    messages.Add($"节点 #{id} 无法从入口到达。");
            }
        }

        hasErrors = messages.Exists(message => !message.StartsWith("提示："));
        validationReport = messages.Count == 0 ? "校验通过。" : string.Join(System.Environment.NewLine, messages);
    }

    // 检查条件和动作的必要稳定标识，避免运行时静默跳过剧情操作。
    private static void ValidateQuestReferences(string requiredQuestId, IReadOnlyList<DialogueQuestAction> actions, string owner, ICollection<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(requiredQuestId) && !QuestExists(requiredQuestId))
            messages.Add($"{owner} 引用了不存在的任务 ID: {requiredQuestId}");
        if (actions == null)
            return;
        foreach (DialogueQuestAction action in actions)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.QuestId))
            {
                messages.Add($"{owner} 存在缺少任务 ID 的任务动作。");
                continue;
            }
            if (!QuestExists(action.QuestId))
                messages.Add($"{owner} 的任务动作引用不存在的任务 ID: {action.QuestId}");
            if (action.ActionType == DialogueQuestActionType.AdvanceObjective && string.IsNullOrWhiteSpace(action.ObjectiveId))
                messages.Add($"{owner} 的推进任务动作缺少目标 ID。");
        }
    }

    // 从 Resources 任务定义中验证编辑器填写的稳定任务 ID。
    private static bool QuestExists(string questId)
    {
        foreach (QuestDefinition definition in Resources.LoadAll<QuestDefinition>("Quests"))
            if (definition != null && definition.QuestId == questId)
                return true;
        return false;
    }

    // 深度遍历链接并识别循环或不存在的目标。
    private static void ValidateNode(
        int id,
        IReadOnlyDictionary<int, DialogueNode> nodesById,
        ISet<int> reachable,
        ISet<int> visiting,
        ICollection<string> messages)
    {
        if (visiting.Contains(id))
        {
            messages.Add($"检测到包含节点 #{id} 的循环引用。");
            return;
        }
        if (reachable.Contains(id))
            return;

        reachable.Add(id);
        visiting.Add(id);
        DialogueNode node = nodesById[id];
        if (node.Speaker == DialogueSpeaker.Npc && node.Choices.Count > 0)
        {
            foreach (DialogueChoice choice in node.Choices)
                ValidateTarget(id, choice.TargetNodeId, nodesById, reachable, visiting, messages);
        }
        else
        {
            ValidateTarget(id, node.NextNodeId, nodesById, reachable, visiting, messages);
        }
        visiting.Remove(id);
    }

    // 检查单条跳转目标并继续遍历合法节点。
    private static void ValidateTarget(
        int source,
        int target,
        IReadOnlyDictionary<int, DialogueNode> nodesById,
        ISet<int> reachable,
        ISet<int> visiting,
        ICollection<string> messages)
    {
        if (target < 0)
            return;
        if (!nodesById.ContainsKey(target))
        {
            messages.Add($"节点 #{source} 指向不存在的节点 #{target}。");
            return;
        }
        ValidateNode(target, nodesById, reachable, visiting, messages);
    }

    // 返回当前资产中尚未使用的下一个节点编号。
    private static int GetNextId(SerializedProperty nodes)
    {
        int max = 0;
        for (int index = 0; index < nodes.arraySize; index++)
            max = Mathf.Max(max, nodes.GetArrayElementAtIndex(index).FindPropertyRelative("nodeId").intValue);
        return max + 1;
    }

    // 创建新的 NPC 独立对话资产。
    private void CreateDialogueAsset()
    {
        const string folder = "Assets/Dialogue";
        Directory.CreateDirectory(folder);
        dialogue = CreateInstance<DialogueAsset>();
        dialogue.EnsureDialogueId();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Dialogue_NPC.asset");
        AssetDatabase.CreateAsset(dialogue, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = dialogue;
    }

    // 经确认后删除当前对话资产及其对应的 meta 文件。
    private void DeleteDialogueAsset()
    {
        string assetPath = AssetDatabase.GetAssetPath(dialogue);
        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        if (!EditorUtility.DisplayDialog(
                "删除当前对话",
                $"确定永久删除对话资产“{dialogue.name}”吗？此操作无法撤销。",
                "删除",
                "取消"))
            return;

        dialogue = null;
        validationReport = string.Empty;
        hasErrors = false;
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.SaveAssets();
    }

    // 查找项目中已有的玩家展示设置。
    private static DialoguePresentationSettings FindPresentationSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialoguePresentationSettings");
        return guids.Length == 0
            ? null
            : AssetDatabase.LoadAssetAtPath<DialoguePresentationSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // 在运行时可自动加载的 Resources 路径创建玩家展示设置。
    private static void CreatePresentationSettings()
    {
        const string folder = "Assets/Resources/Dialogue";
        Directory.CreateDirectory(folder);
        var settings = CreateInstance<DialoguePresentationSettings>();
        AssetDatabase.CreateAsset(settings, folder + "/DialoguePresentationSettings.asset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
    }
}
