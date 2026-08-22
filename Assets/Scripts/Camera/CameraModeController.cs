using Cinemachine;
using QFramework;
using UnityEngine;

/// <summary>
/// Controls the project's single FreeLook camera. Lock-on changes composition,
/// but never switches to a second virtual camera.
/// </summary>
[RequireComponent(typeof(InputManager))]
[DefaultExecutionOrder(-100)]
public class CameraModeController : MonoBehaviour, IController
{
    [Header("Camera Reference")]
    // 全程使用的唯一 Cinemachine FreeLook 虚拟相机。
    [SerializeField] private CinemachineFreeLook explorationCamera;

    [Header("Look Input")]
    // 提供鼠标与右摇杆视角输入的组件。
    [SerializeField] private InputManager input;
    // 鼠标 Delta 转换为 FreeLook 轴输入的缩放系数。
    [SerializeField, Min(0.0001f)] private float mouseLookScale = 0.001f;
    // 手柄右摇杆转换为 FreeLook 轴输入的缩放系数。
    [SerializeField, Min(0.01f)] private float gamepadLookScale = 1f;
    // 是否反转水平视角输入。
    [SerializeField] private bool invertHorizontal;
    // 是否反转垂直视角输入。
    [SerializeField] private bool invertVertical;

    [Header("Camera Collision")]
    // 是否启用基于球形投射的相机防穿墙处理。
    [SerializeField] private bool enableCollision = true;
    // 相机碰撞检测可命中的物理层。
    [SerializeField] private LayerMask collisionMask = ~0;
    // 相机碰撞球形投射的半径，单位为米。
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    // 被障碍遮挡时允许保留的最小轨道半径比例。
    [SerializeField, Range(0.1f, 1f)] private float minScale = 0.3f;
    // 相机遇障时缩短轨道的速度，单位为比例每秒。
    [SerializeField, Min(0f)] private float shrinkSpeed = 8f;
    // 相机离开障碍后恢复轨道的速度，单位为比例每秒。
    [SerializeField, Min(0f)] private float recoverSpeed = 3f;

    [Header("Lock-on Composition")]
    // 锁定构图的基础观察高度，单位为米。
    [SerializeField] private float lockLookHeight = 1.4f;
    // 锁定时相机水平朝向目标的速度，单位为度每秒。
    [SerializeField, Min(0.01f)] private float lockHeadingSpeed = 540f;
    // 锁定目标距离带来的额外相机半径安全余量，单位为米。
    [SerializeField, Min(0f)] private float lockDistancePadding = 1.5f;
    // 锁定时可增加的最大相机轨道半径，单位为米。
    [SerializeField, Min(0f)] private float maxLockExtraRadius = 4f;
    // 锁定额外轨道半径平滑变化速度，单位为米每秒。
    [SerializeField, Min(0f)] private float lockDistanceSmoothSpeed = 6f;

    // 被相机跟随的玩家 Transform。
    private Transform player;
    // 玩家控制器缓存，用于解析跟随目标。
    private PlayerController playerController;
    // 当前锁定目标，非空时启用锁定构图。
    private Transform lockTarget;
    // 锁定时作为 FreeLook LookAt 的动态观察点。
    private Transform lockLookAtTarget;
    // 初始化时记录的三条 FreeLook 轨道基础半径。
    private float[] baseOrbitRadii;
    // 当前碰撞检测后的轨道缩放比例。
    private float orbitScale = 1f;
    // 当前锁定构图增加到轨道上的额外半径。
    private float currentLockExtraRadius;

    // 返回本控制器所属的 QFramework 游戏架构。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    // 解析依赖、配置 FreeLook，并注册锁定目标事件。
    private void Awake()
    {
        ResolveReferences();
        CacheBaseOrbitRadii();
        AlignExplorationHeading();

        this.RegisterEvent<LockOnTargetChangedEvent>(OnLockOnTargetChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    // 订阅视角输入开关，确保暂停 UI 打开当帧停止 FreeLook 轴输入。
    private void OnEnable()
    {
        if (input != null)
            input.LookInputEnabledChanged += OnLookInputEnabledChanged;
    }

    // 取消视角输入开关订阅，避免控制器销毁后残留回调。
    private void OnDisable()
    {
        if (input != null)
            input.LookInputEnabledChanged -= OnLookInputEnabledChanged;
    }

    // 每帧处理水平和垂直视角输入或锁定朝向。
    private void Update()
    {
        if (explorationCamera == null || player == null)
            return;

        if (!input.LookInputEnabled)
        {
            ClearLookAxisValues();
            return;
        }

        if (lockTarget != null)
            UpdateLockedHeading();
        else
            UpdateFreeHeading();

        // Vertical look remains available in both free and lock-on views.
        explorationCamera.m_YAxis.m_InputAxisValue = GetAxisValue(1);
    }

    // 响应背包等 UI 对视角输入的禁用或恢复请求。
    private void OnLookInputEnabledChanged(bool enabled)
    {
        if (!enabled)
            ClearLookAxisValues();
    }

    // 重置 FreeLook 轴的输入和内部速度，同时保留当前镜头角度。
    private void ClearLookAxisValues()
    {
        if (explorationCamera == null)
            return;

        explorationCamera.m_XAxis.Reset();
        explorationCamera.m_YAxis.Reset();
    }

    // 在相机跟随完成后更新锁定观察点与轨道碰撞距离。
    private void LateUpdate()
    {
        if (explorationCamera == null || player == null)
            return;

        UpdateLockLookAtTarget();
        UpdateOrbitDistancesAndCollision();
    }

    // 查找或创建单相机所需的组件与锁定观察点。
    private void ResolveReferences()
    {
        input = input != null ? input : GetComponent<InputManager>();
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        player = playerController != null ? playerController.transform : transform;

        EnsureCinemachineBrain();
        if (explorationCamera == null)
            explorationCamera = FindFreeLookInScene();
        if (explorationCamera == null)
            explorationCamera = CreateExplorationCamera();

        ConfigureExplorationCamera();

        var targetObject = new GameObject("Lock LookAt Target");
        targetObject.transform.SetParent(transform, false);
        lockLookAtTarget = targetObject.transform;
        lockLookAtTarget.position = player.position + Vector3.up * lockLookHeight;
    }

    // 在已加载场景中查找可用的 FreeLook 虚拟相机。
    private static CinemachineFreeLook FindFreeLookInScene()
    {
        foreach (CinemachineFreeLook camera in Resources.FindObjectsOfTypeAll<CinemachineFreeLook>())
        {
            if (camera.gameObject.scene.IsValid())
                return camera;
        }

        return null;
    }

    // 确保主相机挂载用于驱动虚拟相机的 CinemachineBrain。
    private void EnsureCinemachineBrain()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

        if (mainCamera == null)
        {
            Debug.LogError("[CameraModeController] No Main Camera was found.", this);
            return;
        }

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
    }

    // 在场景缺少配置时创建默认的探索 FreeLook 相机。
    private CinemachineFreeLook CreateExplorationCamera()
    {
        var cameraObject = new GameObject("Player Exploration Camera (Auto)");
        var freeLook = cameraObject.AddComponent<CinemachineFreeLook>();
        freeLook.Follow = player;
        freeLook.LookAt = player;
        freeLook.Priority = 20;
        freeLook.m_Orbits = new[]
        {
            new CinemachineFreeLook.Orbit(2.5f, 3.5f),
            new CinemachineFreeLook.Orbit(1.5f, 5f),
            new CinemachineFreeLook.Orbit(0.5f, 3.5f),
        };
        return freeLook;
    }

    // 统一设置 FreeLook 跟随、轴输入、世界空间绑定和观察高度。
    private void ConfigureExplorationCamera()
    {
        explorationCamera.Follow = player;
        explorationCamera.LookAt = player;
        explorationCamera.Priority = 20;
        explorationCamera.m_XAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_YAxis.m_InputAxisName = string.Empty;
        explorationCamera.m_XAxis.m_InvertInput = false;
        explorationCamera.m_YAxis.m_InvertInput = false;
        explorationCamera.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
        explorationCamera.m_XAxis.m_MaxSpeed = 180f;
        explorationCamera.m_YAxis.m_MaxSpeed = 0.7f;
        explorationCamera.m_RecenterToTargetHeading.m_enabled = false;
        explorationCamera.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        explorationCamera.UpdateInputAxisProvider();

        for (int i = 0; i < 3; i++)
        {
            CinemachineVirtualCamera rig = explorationCamera.GetRig(i);
            CinemachineComposer composer = rig != null
                ? rig.GetCinemachineComponent<CinemachineComposer>()
                : null;
            if (composer != null)
                composer.m_TrackedObjectOffset = Vector3.up * lockLookHeight;
        }
    }

    // 缓存设计时轨道半径，供锁定与防穿墙逻辑叠加使用。
    private void CacheBaseOrbitRadii()
    {
        if (explorationCamera == null)
            return;

        baseOrbitRadii = new float[explorationCamera.m_Orbits.Length];
        for (int i = 0; i < baseOrbitRadii.Length; i++)
            baseOrbitRadii[i] = explorationCamera.m_Orbits[i].m_Radius;
    }

    // 将自由状态的水平输入交给 FreeLook 水平轴更新。
    private void UpdateFreeHeading()
    {
        explorationCamera.m_XAxis.m_InputAxisValue = GetAxisValue(0);
        explorationCamera.m_XAxis.Update(Time.deltaTime);
    }

    // 锁定时关闭自由水平环绕并平滑朝向锁定目标。
    private void UpdateLockedHeading()
    {
        Vector3 playerToTarget = lockTarget.position - player.position;
        playerToTarget.y = 0f;
        if (playerToTarget.sqrMagnitude < 0.001f)
            return;

        float targetHeading = Quaternion.LookRotation(playerToTarget, Vector3.up).eulerAngles.y;
        explorationCamera.m_XAxis.m_InputAxisValue = 0f;
        explorationCamera.m_XAxis.Value = Mathf.MoveTowardsAngle(
            explorationCamera.m_XAxis.Value,
            targetHeading,
            lockHeadingSpeed * Time.deltaTime);
    }

    // 响应锁定变化，在玩家观察点和锁定观察点之间切换。
    private void OnLockOnTargetChanged(LockOnTargetChangedEvent e)
    {
        lockTarget = e.Target;
        explorationCamera.m_XAxis.m_InputAxisValue = 0f;

        if (lockTarget != null)
        {
            UpdateLockLookAtTarget(snap: true);
            explorationCamera.LookAt = lockLookAtTarget;
        }
        else
        {
            explorationCamera.LookAt = player;
        }
    }

    // 更新玩家与锁定目标中点的动态观察位置。
    private void UpdateLockLookAtTarget(bool snap = false)
    {
        if (lockLookAtTarget == null || player == null)
            return;

        Vector3 desired = player.position + Vector3.up * lockLookHeight;
        if (lockTarget != null)
        {
            desired = (player.position + lockTarget.position) * 0.5f;
            desired.y = player.position.y;
        }

        lockLookAtTarget.position = snap
            ? desired
            : Vector3.Lerp(lockLookAtTarget.position, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }

    // 叠加锁定距离与碰撞缩放，更新三条 FreeLook 轨道半径。
    private void UpdateOrbitDistancesAndCollision()
    {
        if (baseOrbitRadii == null || baseOrbitRadii.Length == 0)
            return;

        float desiredLockExtra = 0f;
        if (lockTarget != null)
        {
            Vector3 offset = lockTarget.position - player.position;
            offset.y = 0f;
            desiredLockExtra = Mathf.Clamp(
                offset.magnitude * 0.5f + lockDistancePadding,
                0f,
                maxLockExtraRadius);
        }

        currentLockExtraRadius = Mathf.MoveTowards(
            currentLockExtraRadius,
            desiredLockExtra,
            lockDistanceSmoothSpeed * Time.deltaTime);

        float maxRadius = 0f;
        for (int i = 0; i < baseOrbitRadii.Length; i++)
            maxRadius = Mathf.Max(maxRadius, baseOrbitRadii[i] + currentLockExtraRadius);

        float desiredScale = GetCollisionScale(maxRadius);
        float scaleSpeed = desiredScale < orbitScale ? shrinkSpeed : recoverSpeed;
        orbitScale = Mathf.MoveTowards(orbitScale, desiredScale, scaleSpeed * Time.deltaTime);

        for (int i = 0; i < baseOrbitRadii.Length; i++)
        {
            CinemachineFreeLook.Orbit orbit = explorationCamera.m_Orbits[i];
            orbit.m_Radius = (baseOrbitRadii[i] + currentLockExtraRadius) * orbitScale;
            explorationCamera.m_Orbits[i] = orbit;
        }
    }

    // 根据主相机与玩家之间的遮挡距离计算轨道缩放比例。
    private float GetCollisionScale(float maxRadius)
    {
        if (!enableCollision || maxRadius <= 0f)
            return 1f;

        Camera camera = Camera.main;
        if (camera == null)
            return 1f;

        Vector3 origin = player.position + Vector3.up * lockLookHeight;
        Vector3 toCamera = camera.transform.position - origin;
        float distance = toCamera.magnitude;
        if (distance < 0.01f || !TryGetCameraObstruction(origin, toCamera / distance, maxRadius, out RaycastHit hit))
            return 1f;

        float allowedDistance = Mathf.Max(hit.distance - collisionRadius, 0.2f);
        return Mathf.Clamp(allowedDistance / maxRadius, minScale, 1f);
    }

    // 使用球形投射查找最近障碍，并忽略玩家自身碰撞体。
    private bool TryGetCameraObstruction(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
    {
        nearestHit = default;
        float nearestDistance = float.MaxValue;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin, collisionRadius, direction, distance, collisionMask, QueryTriggerInteraction.Ignore);

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

    // 将 FreeLook 初始水平角与玩家当前朝向对齐。
    private void AlignExplorationHeading()
    {
        if (explorationCamera == null || player == null)
            return;

        explorationCamera.m_XAxis.Reset();
        explorationCamera.m_XAxis.Value = WrapHeading(player.eulerAngles.y);
    }

    // 将角度归一化到 -180 至 180 度范围。
    private static float WrapHeading(float heading)
    {
        return Mathf.Repeat(heading + 180f, 360f) - 180f;
    }

    // 读取指定相机轴的输入，并应用设备缩放与反转设置。
    public float GetAxisValue(int axis)
    {
        if (input == null)
            return 0f;

        Vector2 look = input.Look;
        float scale = input.UsingGamepad ? gamepadLookScale : mouseLookScale;
        float value = axis == 1 ? look.y : look.x;
        bool inverted = axis == 1 ? invertVertical : invertHorizontal;
        return value * scale * (inverted ? -1f : 1f);
    }
}
