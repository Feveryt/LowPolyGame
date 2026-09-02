using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 Scene View 采样关键帧并编辑 CameraSequenceAsset 的录制窗口。
/// 第一版采用手动关键帧：策划摆好 Scene 视图后以指定时间写入镜头位姿。
/// </summary>
public sealed class CameraSequenceRecorderWindow : EditorWindow
{
    // 当前正在录制或编辑的镜头资产。
    private CameraSequenceAsset sequence;
    // 下次添加关键帧的时间，单位秒。
    private float nextKeyframeTime;
    // 手动新增关键帧后的默认时间增量，单位秒。
    private float defaultKeyframeInterval = 2f;
    // 资产 Inspector 的序列化访问器。
    private SerializedObject serializedSequence;

    /// <summary>打开镜头序列录制工具。</summary>
    [MenuItem("Tools/Camera Sequence Recorder")]
    public static void Open()
    {
        GetWindow<CameraSequenceRecorderWindow>("Camera Recorder");
    }

    // 将当前选中资产同步到窗口，方便从 Project 面板直接开始编辑。
    private void OnSelectionChange()
    {
        if (Selection.activeObject is CameraSequenceAsset selected)
            SetSequence(selected);
        Repaint();
    }

    // 绘制资产选择、新建、关键帧采样和基础参数编辑界面。
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Camera Sequence Recorder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("在 Scene 视图摆好镜头后点击“记录当前 Scene 视图”。静态序列使用手动关键帧；对话双人镜头使用下方构图参数。", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        CameraSequenceAsset selected = (CameraSequenceAsset)EditorGUILayout.ObjectField("镜头资产", sequence, typeof(CameraSequenceAsset), false);
        if (EditorGUI.EndChangeCheck())
            SetSequence(selected);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("新建镜头资产"))
                CreateSequenceAsset();
            using (new EditorGUI.DisabledScope(sequence == null))
            {
                if (GUILayout.Button("定位资产"))
                {
                    Selection.activeObject = sequence;
                    EditorGUIUtility.PingObject(sequence);
                }
            }
        }

        if (sequence == null)
            return;

        serializedSequence.Update();
        EditorGUILayout.Space();
        DrawSequenceSettings();
        EditorGUILayout.Space();

        CameraSequenceType type = (CameraSequenceType)serializedSequence.FindProperty("sequenceType").enumValueIndex;
        if (type == CameraSequenceType.StaticSequence)
            DrawStaticKeyframes();
        else
            EditorGUILayout.HelpBox("对话双人镜头无需记录场景关键帧。运行时会把玩家和当前交互 NPC 放入动态目标组，保持两人都在画面内。", MessageType.None);

        if (serializedSequence.ApplyModifiedProperties())
            EditorUtility.SetDirty(sequence);
    }

    // 绘制共用序列设置以及对话模板的构图设置。
    private void DrawSequenceSettings()
    {
        DrawProperty("sequenceType");
        DrawProperty("blendInDuration");
        DrawProperty("blendOutDuration");
        DrawProperty("autoComplete");
        DrawProperty("endHoldDuration");

        if ((CameraSequenceType)serializedSequence.FindProperty("sequenceType").enumValueIndex != CameraSequenceType.DialogueTwoShot)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("双人构图", EditorStyles.boldLabel);
        DrawProperty("dialogueCameraOffset");
        DrawProperty("dialogueFramingSize");
        DrawProperty("dialogueFieldOfView");
        DrawProperty("dialogueTargetRadius");
    }

    // 绘制静态镜头的录制按钮和可直接微调的关键帧数组。
    private void DrawStaticKeyframes()
    {
        EditorGUILayout.LabelField("静态关键帧", EditorStyles.boldLabel);
        nextKeyframeTime = EditorGUILayout.FloatField("本次关键帧时间（秒）", Mathf.Max(0f, nextKeyframeTime));
        defaultKeyframeInterval = EditorGUILayout.FloatField("默认时间增量（秒）", Mathf.Max(0.01f, defaultKeyframeInterval));

        using (new EditorGUI.DisabledScope(SceneView.lastActiveSceneView == null))
        {
            if (GUILayout.Button("记录当前 Scene 视图"))
                RecordSceneViewKeyframe();
        }

        if (SceneView.lastActiveSceneView == null)
            EditorGUILayout.HelpBox("请先打开并激活 Scene 视图。", MessageType.Warning);

        SerializedProperty keyframes = serializedSequence.FindProperty("keyframes");
        EditorGUILayout.PropertyField(keyframes, new GUIContent("已记录关键帧"), true);
    }

    // 在指定默认路径创建镜头资产并立即选中。
    private void CreateSequenceAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("新建镜头序列", "CameraSequence_New", "asset", "选择镜头资产保存位置");
        if (string.IsNullOrWhiteSpace(path))
            return;

        CameraSequenceAsset created = CreateInstance<CameraSequenceAsset>();
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();
        SetSequence(created);
        Selection.activeObject = created;
    }

    // 读取 Scene View 相机的位姿和视角，并写入当前镜头资产。
    private void RecordSceneViewKeyframe()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null || sceneView.camera == null)
            return;

        Undo.RecordObject(sequence, "Record Camera Sequence Keyframe");
        Camera sceneCamera = sceneView.camera;
        sequence.AddKeyframe(new CameraShotKeyframe(nextKeyframeTime, sceneCamera.transform.position, sceneCamera.transform.rotation, sceneCamera.fieldOfView));
        nextKeyframeTime += defaultKeyframeInterval;
        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();
        serializedSequence.Update();
    }

    // 更新当前资产对应的序列化对象与下一帧默认时间。
    private void SetSequence(CameraSequenceAsset value)
    {
        sequence = value;
        serializedSequence = sequence != null ? new SerializedObject(sequence) : null;
        nextKeyframeTime = sequence != null ? sequence.GetStaticDuration() : 0f;
    }

    // 用字段名直接绘制序列化配置，避免自定义字段与资产脱节。
    private void DrawProperty(string propertyName)
    {
        EditorGUILayout.PropertyField(serializedSequence.FindProperty(propertyName), true);
    }
}
