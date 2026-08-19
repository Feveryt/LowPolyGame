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
    [SerializeField] private CinemachineFreeLook explorationCamera;

    [Header("Look Input")]
    [SerializeField] private InputManager input;
    [SerializeField, Min(0.0001f)] private float mouseLookScale = 0.001f;
    [SerializeField, Min(0.01f)] private float gamepadLookScale = 1f;
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("Camera Collision")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float minScale = 0.3f;
    [SerializeField, Min(0f)] private float shrinkSpeed = 8f;
    [SerializeField, Min(0f)] private float recoverSpeed = 3f;

    [Header("Lock-on Composition")]
    [SerializeField] private float lockLookHeight = 1.4f;
    [SerializeField, Min(0.01f)] private float lockHeadingSpeed = 540f;
    [SerializeField, Min(0f)] private float lockDistancePadding = 1.5f;
    [SerializeField, Min(0f)] private float maxLockExtraRadius = 4f;
    [SerializeField, Min(0f)] private float lockDistanceSmoothSpeed = 6f;

    private Transform player;
    private PlayerController playerController;
    private Transform lockTarget;
    private Transform lockLookAtTarget;
    private float[] baseOrbitRadii;
    private float orbitScale = 1f;
    private float currentLockExtraRadius;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        ResolveReferences();
        CacheBaseOrbitRadii();
        AlignExplorationHeading();

        this.RegisterEvent<LockOnTargetChangedEvent>(OnLockOnTargetChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void Update()
    {
        if (explorationCamera == null || player == null)
            return;

        if (lockTarget != null)
            UpdateLockedHeading();
        else
            UpdateFreeHeading();

        // Vertical look remains available in both free and lock-on views.
        explorationCamera.m_YAxis.m_InputAxisValue = GetAxisValue(1);
    }

    private void LateUpdate()
    {
        if (explorationCamera == null || player == null)
            return;

        UpdateLockLookAtTarget();
        UpdateOrbitDistancesAndCollision();
    }

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

    private static CinemachineFreeLook FindFreeLookInScene()
    {
        foreach (CinemachineFreeLook camera in Resources.FindObjectsOfTypeAll<CinemachineFreeLook>())
        {
            if (camera.gameObject.scene.IsValid())
                return camera;
        }

        return null;
    }

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

    private void CacheBaseOrbitRadii()
    {
        if (explorationCamera == null)
            return;

        baseOrbitRadii = new float[explorationCamera.m_Orbits.Length];
        for (int i = 0; i < baseOrbitRadii.Length; i++)
            baseOrbitRadii[i] = explorationCamera.m_Orbits[i].m_Radius;
    }

    private void UpdateFreeHeading()
    {
        explorationCamera.m_XAxis.m_InputAxisValue = GetAxisValue(0);
        explorationCamera.m_XAxis.Update(Time.deltaTime);
    }

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

    private void AlignExplorationHeading()
    {
        if (explorationCamera == null || player == null)
            return;

        explorationCamera.m_XAxis.Reset();
        explorationCamera.m_XAxis.Value = WrapHeading(player.eulerAngles.y);
    }

    private static float WrapHeading(float heading)
    {
        return Mathf.Repeat(heading + 180f, 360f) - 180f;
    }

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
