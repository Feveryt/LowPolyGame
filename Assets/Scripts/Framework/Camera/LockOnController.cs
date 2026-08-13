using QFramework;
using UnityEngine;

/// <summary>
/// 战斗锁定目标控制器
///
/// 借鉴动作游戏（如 Devil May Cry / Erbium）的锁定系统：
/// - Tab 键锁定/解锁：自动选中相机前方角度最优、距离最近的敌人
/// - 锁定后广播 LockOnTargetChangedEvent，驱动战斗相机与玩家朝向
/// - 目标死亡、摘武器时自动解锁
///
/// 敌人判定优先级：挂载 EnemyBase / EnemyStats 组件 > "Enemy" 标签
///
/// 架构说明：IController 默认无 ICanSendEvent 能力（QFramework 分层规则：
/// 表现层只监听事件，事件应由 Model/System 产生）。本控制器广播的
/// LockOnTargetChangedEvent 属于"实时玩法表现事件"，与业务数据无关，
/// 故显式声明 ICanSendEvent 作为工程折衷。业务数据变更仍须走 Model/Command。
/// </summary>
public class LockOnController : MonoBehaviour, IController, ICanSendEvent
{
    [Header("锁定参数")]
    [SerializeField] private float lockRange = 12f;
    [SerializeField] private float lockAngle = 70f;
    [SerializeField] private LayerMask enemyMask = ~0;

    private InputManager input;
    private Transform player;
    private Transform currentTarget;
    private bool isEquipped;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>当前锁定目标（null 表示未锁定）</summary>
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        var playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            player = playerController.transform;

        // 摘武器退出战斗模式时，自动解除锁定
        this.RegisterEvent<EquipmentChangedEvent>(OnEquipmentChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnEnable()
    {
        if (input == null)
            input = GetComponent<InputManager>();
        if (input == null)
            input = FindFirstObjectByType<InputManager>();
        if (input != null)
            input.LockOnPressed += OnLockOnPressed;
    }

    private void OnDisable()
    {
        if (input != null)
            input.LockOnPressed -= OnLockOnPressed;
    }

    private void Update()
    {
        // 目标死亡/被销毁/被禁用 → 自动解锁
        if (currentTarget != null && (currentTarget.gameObject == null || !currentTarget.gameObject.activeInHierarchy))
        {
            Unlock();
        }
    }

    private void OnEquipmentChanged(EquipmentChangedEvent e)
    {
        isEquipped = e.Equipped;

        if (!e.Equipped && currentTarget != null)
        {
            Unlock();
        }
    }

    private void OnLockOnPressed()
    {
        if (!isEquipped)
            return;

        if (currentTarget != null)
        {
            Unlock();
        }
        else
        {
            AcquireNearestTarget();
        }
    }

    /// <summary>
    /// 搜索相机前方、角度最优、距离最近的敌人
    /// </summary>
    private void AcquireNearestTarget()
    {
        if (player == null)
            return;

        Camera cam = Camera.main;
        Vector3 camForward = cam != null ? cam.transform.forward : player.forward;
        Vector3 flatForward = Vector3.Scale(camForward, new Vector3(1, 0, 1)).normalized;

        Collider[] colliders = Physics.OverlapSphere(player.position, lockRange, enemyMask);
        Transform best = null;
        float bestScore = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.transform.root == player.root)
                continue;
            if (!IsEnemy(col))
                continue;

            Vector3 toTarget = Vector3.Scale(col.transform.position - player.position, new Vector3(1, 0, 1));
            if (toTarget.sqrMagnitude < 0.001f)
                continue;

            float angle = Vector3.Angle(flatForward, toTarget.normalized);
            if (angle > lockAngle)
                continue;

            // 角度优先、距离加权
            float score = angle + toTarget.magnitude * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = col.transform;
            }
        }

        SetTarget(best);
    }

    /// <summary>
    /// 组件优先（不依赖标签配置），标签兜底
    /// </summary>
    private static bool IsEnemy(Collider col)
    {
        if (col.GetComponentInParent<EnemyBase>() != null)
            return true;
        if (col.GetComponentInParent<EnemyStats>() != null)
            return true;

        // 用字符串比较而非 CompareTag，避免标签未定义时抛异常
        return col.gameObject.tag == "Enemy";
    }

    private void SetTarget(Transform target)
    {
        currentTarget = target;
        this.SendEvent(new LockOnTargetChangedEvent(target));
    }

    private void Unlock()
    {
        SetTarget(null);
    }
}
