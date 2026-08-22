using QFramework;
using UnityEngine;

/// <summary>
/// 战斗锁定目标控制器
///
/// 借鉴动作游戏（如 Devil May Cry / Erbium）的锁定系统：
/// - Tab 键锁定/解锁：自动选中相机前方角度最优、距离最近的敌人
/// - 锁定后广播 LockOnTargetChangedEvent，驱动单相机锁定构图与玩家朝向
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
    // 可搜索锁定目标的最大水平距离，单位为米。
    [SerializeField] private float lockRange = 12f;
    // 相对相机前方允许选中的最大夹角，单位为度。
    [SerializeField] private float lockAngle = 70f;
    // 用于搜索候选敌人的物理层。
    [SerializeField] private LayerMask enemyMask = ~0;

    // 接收锁定按键的输入组件。
    private InputManager input;
    // 用于搜索和评分目标的玩家 Transform。
    private Transform player;
    // 当前已锁定的敌人目标，空值表示未锁定。
    private Transform currentTarget;
    // 当前是否持武器，未持武器时禁止锁定。
    private bool isEquipped;

    // 返回本控制器所属的 QFramework 游戏架构。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>当前锁定目标（null 表示未锁定）</summary>
    public Transform CurrentTarget => currentTarget;

    // 解析玩家引用并监听装备状态变化。
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

    // 启用时查找输入组件并订阅锁定按键事件。
    private void OnEnable()
    {
        if (input == null)
            input = GetComponent<InputManager>();
        if (input == null)
            input = FindFirstObjectByType<InputManager>();
        if (input != null)
            input.LockOnPressed += OnLockOnPressed;
    }

    // 禁用时取消订阅锁定按键事件。
    private void OnDisable()
    {
        if (input != null)
            input.LockOnPressed -= OnLockOnPressed;
    }

    // 每帧检查当前目标是否已经死亡或失效。
    private void Update()
    {
        // 目标死亡/被销毁/被禁用 → 自动解锁
        if (currentTarget != null && (currentTarget.gameObject == null || !currentTarget.gameObject.activeInHierarchy))
        {
            Unlock();
        }
    }

    // 持武器状态变化时更新锁定可用性，并在收武器时解除锁定。
    private void OnEquipmentChanged(EquipmentChangedEvent e)
    {
        isEquipped = e.Equipped;

        if (!e.Equipped && currentTarget != null)
        {
            Unlock();
        }
    }

    // 响应锁定输入，在获取目标与解除目标之间切换。
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

    // 保存当前目标并广播锁定目标变化事件。
    private void SetTarget(Transform target)
    {
        currentTarget = target;
        this.SendEvent(new LockOnTargetChangedEvent(target));
    }

    // 清空当前锁定目标。
    private void Unlock()
    {
        SetTarget(null);
    }
}
