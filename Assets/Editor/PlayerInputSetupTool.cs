using System.IO;
using Cinemachine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class PlayerInputSetupTool
{
    private const string InputAssetPath = "Assets/InputSystem/PlayerAction.inputactions";
    private const string PlayerPrefabPath = "Assets/Resource/Player.prefab";
    private const string AnimatorControllerPath = "Assets/ArtRes/Animator/Player.controller";
    private const string AttackFolderPath = "Assets/ArtRes/Dynamic Sword Animset/Animations/InPlace";
    private const string SetupVersionKey = "LowPolyGame.PlayerInputSetup.Version";
    private const int SetupVersion = 5;

    [DidReloadScripts]
    private static void SetupOnceAfterCompile()
    {
        if (SessionState.GetInt(SetupVersionKey, 0) >= SetupVersion)
            return;

        EditorApplication.delayCall += SetupPlayerInputAutomatically;
    }

    private static void SetupPlayerInputAutomatically()
    {
        SetupPlayerInput();
        SessionState.SetInt(SetupVersionKey, SetupVersion);
    }

    [MenuItem("Tools/Low Poly Game/Setup Player Input")]
    public static void SetupPlayerInput()
    {
        InputActionAsset asset = BuildInputAsset();
        ConfigureHeavyAttackAnimator();
        ConfigurePlayerPrefab(asset);
        ConfigureCameraInActiveScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Player input configured: WASD/mouse and gamepad bindings are ready.");
    }

    private static InputActionAsset BuildInputAsset()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InputAssetPath));
        InputActionAsset generatedAsset = ScriptableObject.CreateInstance<InputActionAsset>();

        InputActionMap player = generatedAsset.AddActionMap("Player");

        InputAction move = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        InputActionSetupExtensions.CompositeSyntax keyboardMove = move.AddCompositeBinding("2DVector");
        int keyboardMoveBindingIndex = keyboardMove.bindingIndex;
        keyboardMove
            .With("Up", "<Keyboard>/w", groups: "Keyboard&Mouse")
            .With("Down", "<Keyboard>/s", groups: "Keyboard&Mouse")
            .With("Left", "<Keyboard>/a", groups: "Keyboard&Mouse")
            .With("Right", "<Keyboard>/d", groups: "Keyboard&Mouse");
        move.ChangeBinding(keyboardMoveBindingIndex).WithGroup("Keyboard&Mouse");
        move.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.15,max=0.95)", groups: "Gamepad");

        InputAction look = player.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
        look.AddBinding("<Mouse>/delta", groups: "Keyboard&Mouse");
        look.AddBinding("<Gamepad>/rightStick", processors: "stickDeadzone(min=0.15,max=0.95)", groups: "Gamepad");

        InputAction sprint = player.AddAction("Sprint", InputActionType.Button, expectedControlLayout: "Button");
        sprint.AddBinding("<Keyboard>/leftShift", groups: "Keyboard&Mouse");
        sprint.AddBinding("<Gamepad>/leftShoulder", groups: "Gamepad");

        InputAction equip = player.AddAction("Equip", InputActionType.Button, expectedControlLayout: "Button");
        equip.AddBinding("<Keyboard>/r", interactions: "Press", groups: "Keyboard&Mouse");
        equip.AddBinding("<Gamepad>/buttonNorth", interactions: "Press", groups: "Gamepad");

        InputAction heavyAttack = player.AddAction("HeavyAttack", InputActionType.Button, expectedControlLayout: "Button");
        heavyAttack.AddBinding("<Mouse>/rightButton", interactions: "Press", groups: "Keyboard&Mouse");
        heavyAttack.AddBinding("<Gamepad>/rightTrigger", interactions: "Press(pressPoint=0.4)", groups: "Gamepad");

        InputAction lockOn = player.AddAction("LockOn", InputActionType.Button, expectedControlLayout: "Button");
        lockOn.AddBinding("<Keyboard>/tab", interactions: "Press", groups: "Keyboard&Mouse");
        lockOn.AddBinding("<Gamepad>/rightStickPress", interactions: "Press", groups: "Gamepad");

        generatedAsset.AddControlScheme("Keyboard&Mouse")
            .WithBindingGroup("Keyboard&Mouse")
            .WithRequiredDevice("<Keyboard>")
            .WithRequiredDevice("<Mouse>");
        generatedAsset.AddControlScheme("Gamepad")
            .WithBindingGroup("Gamepad")
            .WithRequiredDevice("<Gamepad>");

        File.WriteAllText(InputAssetPath, generatedAsset.ToJson());
        Object.DestroyImmediate(generatedAsset);
        AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceUpdate);

        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
    }

    private static void ConfigurePlayerPrefab(InputActionAsset asset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogWarning($"Player prefab was not found at {PlayerPrefabPath}.");
            return;
        }

        try
        {
            CharacterController characterController = GetOrAdd<CharacterController>(root);
            InputManager inputManager = GetOrAdd<InputManager>(root);
            PlayerAnimation playerAnimation = GetOrAdd<PlayerAnimation>(root);
            PlayerController playerController = GetOrAdd<PlayerController>(root);
            PlayerCombat playerCombat = GetOrAdd<PlayerCombat>(root);
            LockOnController lockOnController = GetOrAdd<LockOnController>(root);
            CameraModeController cameraModeController = GetOrAdd<CameraModeController>(root);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            Rigidbody rigidbody = root.GetComponent<Rigidbody>();

            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (animatorController != null)
                    animator.runtimeAnimatorController = animatorController;
                else
                    Debug.LogError($"Animator Controller was not found at {AnimatorControllerPath}.");
            }
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }

            SetReference(inputManager, "actions", asset);
            SetReference(playerAnimation, "animator", animator);
            SetReference(playerController, "controller", characterController);
            SetReference(playerController, "input", inputManager);
            SetReference(playerController, "playerAnimation", playerAnimation);
            SetReference(playerController, "playerCombat", playerCombat);
            SetReference(playerCombat, "input", inputManager);
            SetReference(playerCombat, "playerAnimation", playerAnimation);
            SetReference(playerCombat, "animator", animator);
            SetReference(cameraModeController, "input", inputManager);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Low Poly Game/Setup Camera In Active Scene")]
    public static void ConfigureCameraInActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || scene.path.StartsWith("Temp/"))
        {
            Debug.LogWarning("Save and open a gameplay scene before setting up Cinemachine.");
            return;
        }

        Camera mainCamera = FindInScene<Camera>(scene, camera => camera.CompareTag("MainCamera"));
        PlayerController playerController = FindInScene<PlayerController>(scene);
        CameraModeController modeController = FindInScene<CameraModeController>(scene);
        if (mainCamera == null || playerController == null || modeController == null)
        {
            Debug.LogWarning("Camera setup requires a Main Camera and a Player instance in the active scene.");
            return;
        }

        CinemachineBrain brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);

        CinemachineFreeLook explorationCamera = FindInScene<CinemachineFreeLook>(scene);
        if (explorationCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Exploration Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create exploration camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            explorationCamera = Undo.AddComponent<CinemachineFreeLook>(cameraObject);
        }

        explorationCamera.Follow = playerController.transform;
        explorationCamera.LookAt = playerController.transform;
        explorationCamera.Priority = 20;
        explorationCamera.m_Orbits = new[]
        {
            new CinemachineFreeLook.Orbit(2.5f, 3.5f),
            new CinemachineFreeLook.Orbit(1.5f, 5f),
            new CinemachineFreeLook.Orbit(0.5f, 3.5f),
        };
        explorationCamera.m_XAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_XAxis.m_InvertInput = false;
        explorationCamera.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_XAxis.m_MaxSpeed = 180f;
        explorationCamera.m_YAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_YAxis.m_InvertInput = false;
        explorationCamera.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_YAxis.m_MaxSpeed = 0.7f;

        for (int i = 0; i < 3; i++)
        {
            CinemachineVirtualCamera rig = explorationCamera.GetRig(i);
            CinemachineComposer composer = rig != null
                ? rig.GetCinemachineComponent<CinemachineComposer>()
                : null;
            if (composer != null)
                composer.m_TrackedObjectOffset = Vector3.up * 1.4f;
        }

        CinemachineVirtualCamera combatCamera = FindInScene<CinemachineVirtualCamera>(
            scene,
            camera => camera != explorationCamera && camera.name == "Player Combat Camera");
        if (combatCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Combat Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create combat camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            combatCamera = Undo.AddComponent<CinemachineVirtualCamera>(cameraObject);
        }

        if (combatCamera.GetCinemachineComponent<CinemachineComposer>() == null)
            combatCamera.AddCinemachineComponent<CinemachineComposer>();
        combatCamera.Follow = null;
        combatCamera.LookAt = playerController.transform;
        combatCamera.Priority = 0;

        SetReference(modeController, "explorationCamera", explorationCamera);
        SetReference(modeController, "combatCamera", combatCamera);

        EditorUtility.SetDirty(brain);
        EditorUtility.SetDirty(explorationCamera);
        EditorUtility.SetDirty(combatCamera);
        EditorUtility.SetDirty(modeController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Cinemachine configured in {scene.path}: Brain, FreeLook, and combat camera are ready.");
    }

    private static T FindInScene<T>(Scene scene, System.Predicate<T> predicate = null) where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component.gameObject.scene != scene)
                continue;
            if (predicate == null || predicate(component))
                return component;
        }

        return null;
    }

    [MenuItem("Tools/Low Poly Game/Setup Heavy Attack Combo")]
    public static void ConfigureHeavyAttackAnimator()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Animator Controller was not found at {AnimatorControllerPath}.");
            return;
        }

        EnsureTrigger(controller, "HeavyAttack");
        EnsureTrigger(controller, "HeavyAttackCombo");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(stateMachine, "Equipped Locomotion");
        AnimatorState attack17 = GetOrCreateState(stateMachine, "Attack_17", new Vector3(480f, 330f), LoadClip("Attack_17.FBX"));
        AnimatorState attack18 = GetOrCreateState(stateMachine, "Attack_18", new Vector3(700f, 330f), LoadClip("Attack_18.FBX"));
        AnimatorState attack14 = GetOrCreateState(stateMachine, "Attack_14", new Vector3(920f, 330f), LoadClip("Attack_14.FBX"));

        if (locomotion == null || attack17.motion == null || attack18.motion == null || attack14.motion == null)
        {
            Debug.LogError("Heavy attack setup stopped because a locomotion state or attack clip is missing.");
            return;
        }

        ClearTransitions(attack17);
        ClearTransitions(attack18);
        ClearTransitions(attack14);
        RemoveAnyStateTransitionsTo(stateMachine, attack17);

        AnimatorStateTransition entry = stateMachine.AddAnyStateTransition(attack17);
        entry.name = "Heavy Attack Start";
        entry.hasExitTime = false;
        entry.duration = 0.08f;
        entry.canTransitionToSelf = false;
        entry.AddCondition(AnimatorConditionMode.If, 0f, "HeavyAttack");
        entry.AddCondition(AnimatorConditionMode.If, 0f, "IsEquipped");

        AddComboTransition(attack17, attack18);
        AddComboTransition(attack18, attack14);
        AddReturnTransition(attack17, locomotion);
        AddReturnTransition(attack18, locomotion);
        AddReturnTransition(attack14, locomotion);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Heavy attack combo configured: Attack_17 -> Attack_18 -> Attack_14.");
    }

    private static void EnsureTrigger(AnimatorController controller, string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
                return;
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
                return childState.state;
        }

        return null;
    }

    private static AnimatorState GetOrCreateState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Vector3 position,
        AnimationClip clip)
    {
        AnimatorState state = FindState(stateMachine, stateName) ?? stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimationClip LoadClip(string fileName)
    {
        string path = $"{AttackFolderPath}/{fileName}";
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        Debug.LogError($"Animation clip was not found in {path}.");
        return null;
    }

    private static void ClearTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        foreach (AnimatorStateTransition transition in transitions)
            state.RemoveTransition(transition);
    }

    private static void RemoveAnyStateTransitionsTo(AnimatorStateMachine stateMachine, AnimatorState destination)
    {
        AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
        foreach (AnimatorStateTransition transition in transitions)
        {
            if (transition.destinationState == destination)
                stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void AddComboTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.name = $"{source.name} Combo";
        transition.hasExitTime = true;
        transition.exitTime = 0.55f;
        transition.hasFixedDuration = true;
        transition.duration = 0.08f;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "HeavyAttackCombo");
    }

    private static void AddReturnTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.name = $"{source.name} Complete";
        transition.hasExitTime = true;
        transition.exitTime = 0.92f;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        transition.interruptionSource = TransitionInterruptionSource.None;
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
