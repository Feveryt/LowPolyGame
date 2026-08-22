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

    [MenuItem("Tools/Dialogue Editor")]
    private static void Open()
    {
        GetWindow<DialogueEditorWindow>("Dialogue Editor");
    }

    [MenuItem("Tools/Dialogue/Clear Completed Progress")]
    private static void ClearCompletedProgress()
    {
        DialogueProgressStore.ClearAll();
        Debug.Log("[Dialogue] 已清除全部对话完成进度。");
    }

    // 绘制资产入口、玩家头像资料、NPC 对话内容和校验结果。
    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Dialogue Editor", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        dialogue = (DialogueAsset)EditorGUILayout.ObjectField("Dialogue Asset", dialogue, typeof(DialogueAsset), false);
        if (GUILayout.Button("Create NPC Dialogue", GUILayout.Width(150f)))
            CreateDialogueAsset();
        EditorGUILayout.EndHorizontal();

        DrawPresentationSettings();
        if (dialogue == null)
        {
            EditorGUILayout.HelpBox("创建或选择一个 Dialogue Asset 后开始编辑。", MessageType.Info);
            return;
        }

        DrawDialogue();
    }

    // 显示全局玩家头像资产的创建与选中入口。
    private static void DrawPresentationSettings()
    {
        DialoguePresentationSettings settings = FindPresentationSettings();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Player Portrait", EditorStyles.boldLabel);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("请创建玩家展示设置并在 Inspector 绑定玩家头像及左右位置。", MessageType.Warning);
            if (GUILayout.Button("Create Presentation Settings"))
                CreatePresentationSettings();
        }
        else
        {
            EditorGUILayout.ObjectField("Settings", settings, typeof(DialoguePresentationSettings), false);
            if (GUILayout.Button("Select Settings"))
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

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("NPC Portrait", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(npcName, new GUIContent("NPC Name"));
        EditorGUILayout.PropertyField(npcPortrait, new GUIContent("NPC Image"));
        EditorGUILayout.PropertyField(npcPortraitSide, new GUIContent("NPC Image Side"));
        EditorGUILayout.PropertyField(completionText, new GUIContent("Completion Text"));
        EditorGUILayout.PropertyField(completionEventId, new GUIContent("Completion Event ID"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add NPC Line"))
            AddLine(serialized, DialogueSpeaker.Npc);
        if (GUILayout.Button("Add Player Line"))
            AddLine(serialized, DialogueSpeaker.Player);
        if (GUILayout.Button("Validate"))
            Validate(serialized);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "节点按列表顺序添加。每个节点的 Next Node Id 和 NPC 节点 Choices.Target Node Id 都填写目标节点编号；-1 表示结束。为 NPC 节点添加 Choices 即可显示玩家回答按钮。",
            MessageType.None);
        EditorGUILayout.PropertyField(entryNodeId, new GUIContent("Entry Node ID"));
        DrawNodes(nodes);

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
    private static void DrawNodes(SerializedProperty nodes)
    {
        for (int index = 0; index < nodes.arraySize; index++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(index);
            SerializedProperty id = node.FindPropertyRelative("nodeId");
            SerializedProperty speaker = node.FindPropertyRelative("speaker");
            SerializedProperty text = node.FindPropertyRelative("text");
            SerializedProperty eventId = node.FindPropertyRelative("eventId");
            SerializedProperty next = node.FindPropertyRelative("nextNodeId");
            SerializedProperty choices = node.FindPropertyRelative("choices");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Node #{id.intValue}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(speaker);
            EditorGUILayout.PropertyField(text);
            EditorGUILayout.PropertyField(eventId, new GUIContent("Event ID"));
            if ((DialogueSpeaker)speaker.enumValueIndex == DialogueSpeaker.Npc)
                EditorGUILayout.PropertyField(choices, new GUIContent("Player Choices"), true);
            else if (choices.arraySize > 0)
                EditorGUILayout.HelpBox("玩家节点的 Choices 不会在运行时使用。", MessageType.Warning);
            EditorGUILayout.PropertyField(next, new GUIContent("Next Node ID"));
            EditorGUILayout.EndVertical();
        }
    }

    // 新增一条线性台词，自动连到此前无选项的末尾节点。
    private void AddLine(SerializedObject serialized, DialogueSpeaker speaker)
    {
        SerializedProperty nodes = serialized.FindProperty("nodes");
        SerializedProperty entry = serialized.FindProperty("entryNodeId");
        Undo.RecordObject(dialogue, "Add Dialogue Line");
        int newId = GetNextId(nodes);
        int previousIndex = nodes.arraySize - 1;
        nodes.InsertArrayElementAtIndex(nodes.arraySize);
        SerializedProperty node = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
        node.FindPropertyRelative("nodeId").intValue = newId;
        node.FindPropertyRelative("speaker").enumValueIndex = (int)speaker;
        node.FindPropertyRelative("text").stringValue = speaker == DialogueSpeaker.Npc ? "New NPC line" : "New player line";
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
