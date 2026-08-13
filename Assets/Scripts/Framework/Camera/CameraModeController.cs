using Cinemachine;
using QFramework;
using UnityEngine;

/// <summary>
/// 相机工作模式
/// </summary>
public enum CameraMode
{
    /// <summary>探索：FreeLook 自由环绕视角（未持武器）</summary>
    Exploration,

    /// <summary>战斗：锁定环绕视角（持武器）</summary>
    Combat,
}

/// <summary>
/// 双相机模式控制器
///
/// 设计（借鉴开源项目，MIT 协议）：
/// - 探索模式：沿用场景中的 CinemachineFreeLook，并借鉴
///   dbrizov/Unity-CharacterController 的 SpringArm 思想——
///   从玩家向相机做 SphereCast 检测遮挡，按比例收缩三环半径（防穿墙）。
/// - 战斗模式：借鉴 mishanyaqq/Erbium 的动作游戏相机思路，
///   持武器后切换到锁定环绕相机（运行时自动创建虚拟相机）：
///   · 有锁定目标：相机位于"玩家→目标"连线的玩家后方，视线看向二者中点
///   · 无锁定目标：标准越肩相机（玩家正后方）
///   同样带 SphereCast 防穿墙。
///
/// 模式切换依据：EquipmentChangedEvent（R 键切换装备 → 相机模式跟随切换）。
/// 本组件建议挂在 Player 预制体上，所有引用自动查找，无需手动接线。
/// </summary>
[RequireComponent(typeof(InputManager))]
public class CameraModeController : MonoBehaviour, IController, AxisState.IInputAxisProvider
{
    [Header("相机引用（留空自动查找/创建）")]
    [SerializeField] private CinemachineFreeLook explorationCamera;
    [SerializeField] private CinemachineVirtualCamera combatCamera;

    [Header("视角输入")]
    [SerializeField] private InputManager input;
    [SerializeField, Min(0.001f)] private float mouseLookScale = 0.03f;
    [SerializeField, Min(0.01f)] private float gamepadLookScale = 1f;
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("探索模式 - 防穿墙（SpringArm 思路）")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float minScale = 0.3f;
    [SerializeField] private float shrinkSpeed = 8f;
    [SerializeField] private float recoverSpeed = 3f;

    [Header("战斗模式 - 相机位置")]
    [SerializeField] private float combatDistance = 2.8f;
    [SerializeField] private float combatHeight = 1.5f;
    [SerializeField] private float combatLookHeight = 1.4f;
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.12f;

    private Transform player;
    private Transform lockTarget;
    private Transform lookAtDummy;

    private Vector3 cameraVelocity;
    private Vector3 lookAtVelocity;

    // FreeLook 三环基础半径与当前缩放系数
    private float[] baseOrbitRadii;
    private float orbitScale = 1f;

    private bool isCombatMode;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        ResolveReferences();

        // 记录 FreeLook 三环基础半径（之后碰撞收缩以此为基准）
        if (explorationCamera != null)
        {
            baseOrbitRadii = new float[explorationCamera.m_Orbits.Length];
            for (int i = 0; i < baseOrbitRadii.Length; i++)
            {
                baseOrbitRadii[i] = explorationCamera.m_Orbits[i].m_Radius;
            }
        }

        // 初始状态：探索模式
        ApplyMode(CameraMode.Exploration, snap: true);

        this.RegisterEvent<EquipmentChangedEvent>(OnEquipmentChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<LockOnTargetChangedEvent>(OnLockOnTargetChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        if (isCombatMode)
        {
            UpdateCombatCamera();
        }
        else
        {
            UpdateExplorationCollision();
        }
    }

    // ---------------------------------------------------------------
    // 引用解析与模式切换
    // ---------------------------------------------------------------

    private void ResolveReferences()
    {
        input = input != null ? input : GetComponent<InputManager>();

        if (player == null)
        {
            var playerController = GetComponent<PlayerController>();
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
                player = playerController.transform;
        }

        EnsureCinemachineBrain();

        if (explorationCamera == null)
        {
            // 必须用 FindObjectsOfTypeAll 才能找到"被禁用"的 FreeLook
            // （FindObjectOfType 只返回活跃对象，这也是之前镜头不跟随的原因之一）
            explorationCamera = FindFreeLookInScene();

            // 场景中遗留的演示相机可能处于禁用状态：找到后强制激活，
            // 否则 CinemachineBrain 没有任何活跃相机，镜头不会跟随。
            if (explorationCamera != null && !explorationCamera.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[CameraModeController] 场景中的 FreeLook 处于禁用状态，已自动激活。", explorationCamera);
                explorationCamera.gameObject.SetActive(true);
            }
        }

        // 空白测试场景中也创建可旋转的 FreeLook，而不是只能跟随的固定相机。
        if (explorationCamera == null)
            explorationCamera = CreateExplorationCamera();

        ConfigureExplorationCamera();

        if (lookAtDummy == null)
        {
            var dummyGo = new GameObject("Combat LookAt Target");
            dummyGo.transform.SetParent(transform, false);
            lookAtDummy = dummyGo.transform;
        }

        if (combatCamera == null)
            combatCamera = CreateCombatCamera();

        combatCamera.Follow = null;
        combatCamera.LookAt = lookAtDummy;
    }

    /// <summary>
    /// 查找场景中的 FreeLook（含被禁用的实例，排除资产文件中的对象）
    /// </summary>
    private static CinemachineFreeLook FindFreeLookInScene()
    {
        CinemachineFreeLook[] all = Resources.FindObjectsOfTypeAll<CinemachineFreeLook>();
        foreach (CinemachineFreeLook cam in all)
        {
            if (cam.gameObject.scene.IsValid())
                return cam;
        }

        return null;
    }

    private void EnsureCinemachineBrain()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[CameraModeController] 场景中没有标记为 MainCamera 的相机。", this);
            return;
        }

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
        {
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
            Debug.LogWarning("[CameraModeController] Main Camera 缺少 CinemachineBrain，已在运行时自动添加。", mainCamera);
        }
    }

    private CinemachineFreeLook CreateExplorationCamera()
    {
        var cameraGo = new GameObject("Player Exploration Camera (Auto)");
        var freeLook = cameraGo.AddComponent<CinemachineFreeLook>();
        freeLook.Follow = player;
        freeLook.LookAt = player;
        freeLook.Priority = 20;
        freeLook.m_Orbits = new[]
        {
            new CinemachineFreeLook.Orbit(2.5f, 3.5f),
            new CinemachineFreeLook.Orbit(1.5f, 5f),
            new CinemachineFreeLook.Orbit(0.5f, 3.5f),
        };

        Debug.Log("[CameraModeController] 场景中没有 FreeLook，已创建可旋转的探索相机。");
        return freeLook;
    }

    private void ConfigureExplorationCamera()
    {
        if (explorationCamera == null)
            return;

        explorationCamera.Follow = player;
        explorationCamera.LookAt = player;
        explorationCamera.m_XAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_YAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_XAxis.m_InvertInput = invertHorizontal;
        explorationCamera.m_YAxis.m_InvertInput = invertVertical;
        explorationCamera.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_XAxis.m_MaxSpeed = 180f;
        explorationCamera.m_YAxis.m_MaxSpeed = 0.7f;
        explorationCamera.m_XAxis.SetInputAxisProvider(0, this);
        explorationCamera.m_YAxis.SetInputAxisProvider(1, this);

        for (int i = 0; i < 3; i++)
        {
            CinemachineVirtualCamera rig = explorationCamera.GetRig(i);
            CinemachineComposer composer = rig != null
                ? rig.GetCinemachineComponent<CinemachineComposer>()
                : null;
            if (composer != null)
                composer.m_TrackedObjectOffset = Vector3.up * combatLookHeight;
        }
    }

    /// <summary>
    /// 运行时自动创建战斗虚拟相机：
    /// 不用 Body（位置由本脚本控制），用 Composer 负责朝向
    /// </summary>
    private CinemachineVirtualCamera CreateCombatCamera()
    {
        var cameraGo = new GameObject("Combat Camera (Auto)");
        cameraGo.transform.SetParent(transform, false);

        var vcam = cameraGo.AddComponent<CinemachineVirtualCamera>();
        vcam.AddCinemachineComponent<CinemachineComposer>();
        vcam.LookAt = lookAtDummy;
        vcam.Priority = 0;
        return vcam;
    }

    private void OnEquipmentChanged(EquipmentChangedEvent e)
    {
        ApplyMode(e.Equipped ? CameraMode.Combat : CameraMode.Exploration);
    }

    private void OnLockOnTargetChanged(LockOnTargetChangedEvent e)
    {
        lockTarget = e.Target;

        // 进入战斗瞬间锁定目标时，让相机快速就位
        if (isCombatMode)
        {
            cameraVelocity = Vector3.zero;
        }
    }

    private void ApplyMode(CameraMode mode, bool snap = false)
    {
        bool wasCombatMode = isCombatMode;
        isCombatMode = mode == CameraMode.Combat;

        if (explorationCamera != null)
            explorationCamera.Priority = isCombatMode ? 0 : 20;
        if (combatCamera != null)
            combatCamera.Priority = isCombatMode ? 20 : 0;

        if ((snap || !wasCombatMode) && isCombatMode && combatCamera != null && player != null)
        {
            SnapCombatCamera();
        }
    }

    // ---------------------------------------------------------------
    // 探索模式：FreeLook 防穿墙（SpringArm 思路）
    // ---------------------------------------------------------------

    private void UpdateExplorationCollision()
    {
        if (!enableCollision || explorationCamera == null || baseOrbitRadii == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 followPos = explorationCamera.Follow != null
            ? explorationCamera.Follow.position
            : explorationCamera.transform.position;
        followPos += Vector3.up * combatLookHeight;
        Vector3 camPos = cam.transform.position;

        Vector3 toCamera = camPos - followPos;
        float distance = toCamera.magnitude;
        if (distance < 0.01f)
            return;

        float maxRadius = 0f;
        for (int i = 0; i < baseOrbitRadii.Length; i++)
            maxRadius = Mathf.Max(maxRadius, baseOrbitRadii[i]);

        // 从玩家向相机方向做球形射线：命中说明有遮挡，按命中距离收缩
        float desiredScale = 1f;
        if (TryGetCameraObstruction(followPos, toCamera / distance, maxRadius, out RaycastHit hit))
        {
            float allowedDistance = Mathf.Max(hit.distance - collisionRadius, 0.2f);
            desiredScale = Mathf.Clamp(allowedDistance / maxRadius, minScale, 1f);
        }

        // 被遮挡时快速收缩，无遮挡时缓慢恢复（防止抖动）
        float speed = desiredScale < orbitScale ? shrinkSpeed : recoverSpeed;
        orbitScale = Mathf.MoveTowards(orbitScale, desiredScale, speed * Time.deltaTime);

        for (int i = 0; i < baseOrbitRadii.Length; i++)
        {
            CinemachineFreeLook.Orbit orbit = explorationCamera.m_Orbits[i];
            orbit.m_Radius = baseOrbitRadii[i] * orbitScale;
            explorationCamera.m_Orbits[i] = orbit;
        }
    }

    // ---------------------------------------------------------------
    // 战斗模式：锁定环绕相机
    // ---------------------------------------------------------------

    private void SnapCombatCamera()
    {
        cameraVelocity = Vector3.zero;
        lookAtVelocity = Vector3.zero;

        ComputeCombatDesired(out Vector3 desiredPosition, out Vector3 desiredLookAt);
        combatCamera.transform.position = desiredPosition;
        lookAtDummy.position = desiredLookAt;
    }

    private void UpdateCombatCamera()
    {
        if (combatCamera == null || lookAtDummy == null)
            return;

        ComputeCombatDesired(out Vector3 desiredPosition, out Vector3 desiredLookAt);

        // 防穿墙：从玩家胸部向期望相机位置做 SphereCast
        if (enableCollision)
        {
            Vector3 castOrigin = player.position + Vector3.up * 0.5f;
            Vector3 toCamera = desiredPosition - castOrigin;
            float distance = toCamera.magnitude;

            if (distance > 0.01f &&
                TryGetCameraObstruction(castOrigin, toCamera / distance, distance, out RaycastHit hit))
            {
                float allowedDistance = Mathf.Max(hit.distance - collisionRadius, 0.2f);
                desiredPosition = castOrigin + toCamera / distance * allowedDistance;
            }
        }

        // 位置手动控制（该虚拟相机无 Body，不会与本脚本打架），朝向由 Composer 负责
        combatCamera.transform.position = Vector3.SmoothDamp(
            combatCamera.transform.position, desiredPosition, ref cameraVelocity, positionSmoothTime);
        lookAtDummy.position = Vector3.SmoothDamp(
            lookAtDummy.position, desiredLookAt, ref lookAtVelocity, positionSmoothTime);
    }

    /// <summary>
    /// 计算战斗相机期望位置与视线落点
    /// </summary>
    private void ComputeCombatDesired(out Vector3 desiredPosition, out Vector3 desiredLookAt)
    {
        if (lockTarget != null)
        {
            // 锁定：相机位于"玩家 → 目标"连线的玩家后方，视线看向二者中点
            Vector3 playerToTarget = lockTarget.position - player.position;
            playerToTarget.y = 0f;
            if (playerToTarget.sqrMagnitude < 0.001f)
                playerToTarget = -player.forward;

            Vector3 behindPlayer = -playerToTarget.normalized;
            desiredPosition = player.position + behindPlayer * combatDistance + Vector3.up * combatHeight;
            desiredLookAt = (player.position + lockTarget.position) * 0.5f + Vector3.up * combatLookHeight;
        }
        else
        {
            // 未锁定：标准越肩相机（玩家正后方）
            desiredPosition = player.position - player.forward * combatDistance + Vector3.up * combatHeight;
            desiredLookAt = player.position + Vector3.up * combatLookHeight;
        }
    }

    private bool TryGetCameraObstruction(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit nearestHit)
    {
        nearestHit = default;
        float nearestDistance = float.MaxValue;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionRadius,
            direction,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (player != null && hit.transform.root == player.root)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        return nearestDistance < float.MaxValue;
    }

    public float GetAxisValue(int axis)
    {
        if (input == null)
            return 0f;

        Vector2 look = input.Look;
        float scale = input.UsingGamepad ? gamepadLookScale : mouseLookScale;
        float value = axis == 1 ? look.y : look.x;
        return value * scale;
    }
}
