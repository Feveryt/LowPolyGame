using UnityEngine;

/// <summary>
/// 玩家换装组件：管理武器/盾牌的外观模型挂载，并负责武器在两个挂点间的搬运。
/// 战斗态（拔剑后）武器挂在 Hand_R 上跟随手部动画，收剑态挂在背部收纳位随身体摆动。
/// 按装备键时由 PlayerController 触发一次过渡：动画播放到切换点后把武器实例搬到目标挂点，
/// 因此拔剑动作前段玩家是空手绕到背后、后段才带着武器出鞘。
/// 挂点为角色骨骼 Hand_R（武器）与 Hand_L（盾牌）；
/// 装备时销毁旧模型、按 ItemDefinition 的偏移参数实例化新模型。
/// 武器命中盒常驻于武器挂点下的 WeaponHitboxZone（战斗态按手上真实实例的包围盒自适应尺寸），
/// 保证 PlayerCombat 的命中判定引用不因换装失效；初始武器在 Start 时接管进同一套管理。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerOutfit : MonoBehaviour
{
    // 武器默认挂点骨骼名。
    private const string WeaponSocketName = "Hand_R";
    // 盾牌默认挂点骨骼名。
    private const string ShieldSocketName = "Hand_L";
    // 常驻武器命中盒的子物体名。
    private const string HitboxZoneName = "WeaponHitboxZone";
    // 装备模型实例的命名前缀（PolygonDungeon 武器统一为 SM_Wep_*）。
    private const string EquipmentPrefix = "SM_Wep";
    // 背部收纳挂点的默认子物体名（Player 预制体里原本就背着的那把剑）。
    private const string DefaultSheathName = "SM_Item_Sword";

    // 武器命中盒组件（PlayerCombat 的判定依赖它）。
    [SerializeField] private WeaponHitbox weaponHitbox;
    // 初始武器对应的物品定义（卸下武器时恢复外观）。
    [SerializeField] private ItemDefinition defaultWeaponItem;

    [Header("Sheath (收剑态)")]
    // 背部武器收纳挂点；留空时自动在角色骨骼里查找名为 SM_Item_Sword 的节点。
    [SerializeField] private Transform weaponSheathSocket;
    // 武器挂在背部时相对收纳挂点的位置偏移。
    [SerializeField] private Vector3 sheathLocalPosition = Vector3.zero;
    // 武器挂在背部时相对收纳挂点的欧拉旋转。
    // 收纳挂点是预制体里原本背着的那把装饰剑节点，其自身旋转由骨骼继承；
    // 这里额外偏转是为了让不同武器模型的网格长轴与原本的斜挂姿态一致
    // （素材包的剑在本地空间里是竖直的，装饰剑的网格则自带倾斜）。
    [SerializeField] private Vector3 sheathLocalEuler = new Vector3(0f, 0f, 135f);
    // 收剑态把武器的包围盒中心对齐到背上装饰剑的位置，
    // 不同武器模型的 pivot 差异极大（有的在剑柄、有的在剑身中心），只靠固定偏移无法通用。
    [SerializeField] private bool autoAlignSheathToDecoration = true;

    [Header("Draw / Sheathe Transition")]
    // 拔剑动画（控制器 Draw Weapon 状态）的时长，单位为秒。
    [SerializeField, Min(0.01f)] private float drawTransitionDuration = 0.8f;
    // 拔剑动画中"武器从背部到手部"的归一化时刻。
    [SerializeField, Range(0f, 1f)] private float drawSwitchPoint = 0.35f;
    // 收剑动画（控制器 Eqip_To_Uneqip 状态）的时长，单位为秒。
    [SerializeField, Min(0.01f)] private float sheatheTransitionDuration = 0.867f;
    // 收剑动画中"武器从手部回背部"的归一化时刻。
    [SerializeField, Range(0f, 1f)] private float sheatheSwitchPoint = 0.45f;

    // 当前已装备武器实例。
    private GameObject currentWeaponInstance;
    // 当前已装备盾牌实例。
    private GameObject currentShieldInstance;
    // 当前武器对应的物品定义（初始武器为 null）。
    private ItemDefinition equippedWeaponItem;
    // 当前盾牌对应的物品定义。
    private ItemDefinition equippedShieldItem;
    // 常驻命中盒碰撞体。
    private BoxCollider hitboxZoneCollider;
    // 背部装饰剑的渲染器（装备武器后隐藏，避免与实体武器重叠）。
    private Renderer sheathDecorationRenderer;
    // 背上装饰剑的包围盒中心（相对收纳挂点的局部坐标），作为武器自动对齐的参考点。
    private Vector3 sheathReferenceLocalCenter;
    // 是否成功取得装饰剑参考点。
    private bool hasSheathReference;
    // 当前是否处于战斗态（武器在手）。
    private bool combatStance;
    // 待执行的挂点切换时刻；为负表示没有待处理切换。
    private float pendingSwitchTime = -1f;
    // 待执行的挂点切换目标是否为手部。
    private bool pendingSwitchToHand;

    /// <summary>当前已装备武器实例名；手空时为空。</summary>
    public string CurrentWeaponName => currentWeaponInstance != null ? currentWeaponInstance.name : string.Empty;

    /// <summary>已装备武器的物品 ID；未通过物品装备时返回 0。</summary>
    public int EquippedWeaponItemId => equippedWeaponItem != null ? equippedWeaponItem.Id : 0;

    /// <summary>已装备盾牌的物品 ID；未装备时返回 0。</summary>
    public int EquippedShieldItemId => equippedShieldItem != null ? equippedShieldItem.Id : 0;

    /// <summary>当前已装备武器的物品定义（装备格 UI 显示用）；初始武器为 null。</summary>
    public ItemDefinition EquippedWeaponItem => equippedWeaponItem;

    /// <summary>当前已装备盾牌的物品定义（装备格 UI 显示用）。</summary>
    public ItemDefinition EquippedShieldItem => equippedShieldItem;

    /// <summary>当前武器是否握在手上（战斗态）。</summary>
    public bool IsWeaponDrawn => combatStance;

    /// <summary>是否正处于拔剑/收剑过渡中（此期间不接受新的切换输入）。</summary>
    public bool IsStanceTransitioning => pendingSwitchTime >= 0f;

    /// <summary>按存档中的物品 ID 重新装备武器与盾牌（读档流程调用）。</summary>
    public void RestoreEquipped(int weaponItemId, int shieldItemId)
    {
        ItemDefinitionCatalog catalog = Resources.Load<ItemDefinitionCatalog>("ItemDefinitionCatalog");
        if (catalog == null)
        {
            Debug.LogWarning("[Outfit] 找不到 ItemDefinitionCatalog，无法恢复装备外观。", this);
            return;
        }

        if (weaponItemId > 0 && catalog.TryGetItem(weaponItemId, out ItemDefinition weapon) && weapon.IsEquippable)
            Equip(weapon);

        if (shieldItemId > 0 && catalog.TryGetItem(shieldItemId, out ItemDefinition shield) && shield.IsEquippable)
            Equip(shield);
    }

    /// <summary>
    /// 开始一次拔剑/收剑过渡：动画播放到切换点后，武器实例才在两个挂点之间搬运。
    /// 切换点取动画中"手最贴近另一侧挂点"的时刻，避免武器凭空跳跃。
    /// </summary>
    public void BeginStanceTransition(bool toCombat)
    {
        combatStance = toCombat;
        float duration = toCombat ? drawTransitionDuration : sheatheTransitionDuration;
        float ratio = toCombat ? drawSwitchPoint : sheatheSwitchPoint;
        pendingSwitchToHand = toCombat;
        pendingSwitchTime = Time.time + Mathf.Max(0f, duration * ratio);
    }

    /// <summary>不做过渡，立即把武器放到目标挂点（读档、受击打断等场合）。</summary>
    public void ApplyStanceImmediate(bool toCombat)
    {
        combatStance = toCombat;
        pendingSwitchTime = -1f;
        pendingSwitchToHand = toCombat;
        AttachWeaponToSocket(currentWeaponInstance, toCombat);
    }

    // 初始化：解析命中盒与背部收纳挂点引用，并建立常驻命中盒。
    private void Awake()
    {
        weaponHitbox = weaponHitbox != null ? weaponHitbox : GetComponentInChildren<WeaponHitbox>(true);
        ResolveSheathSocket();
        EnsureHitboxZone();
    }

    // 在所有 Awake 完成后接管初始武器，并把开局状态设为收剑（武器背在背上）。
    private void Start()
    {
        // 接管初始武器：记录实例并清除其自带的旧命中盒（改由常驻 Zone 承担判定）。
        Transform socket = GetWeaponSocket();
        currentWeaponInstance = FindWeaponInstance(socket);
        if (currentWeaponInstance != null)
        {
            foreach (WeaponHitbox legacy in currentWeaponInstance.GetComponentsInChildren<WeaponHitbox>(true))
                Destroy(legacy);
        }

        // 开局不持武器：初始武器直接收纳到背部。
        combatStance = false;
        pendingSwitchTime = -1f;
        AttachWeaponToSocket(currentWeaponInstance, false);
    }

    // 过渡计时：到点后把武器搬到目标挂点。
    private void Update()
    {
        if (pendingSwitchTime < 0f || Time.time < pendingSwitchTime)
            return;

        pendingSwitchTime = -1f;
        AttachWeaponToSocket(currentWeaponInstance, pendingSwitchToHand);
    }

    /// <summary>按物品配置装备到对应槽位；返回是否成功。</summary>
    public bool Equip(ItemDefinition item)
    {
        if (item == null || !item.IsEquippable || item.EquipmentPrefab == null)
            return false;

        switch (item.EquipmentSlot)
        {
            case EquipmentSlotType.Weapon:
                EquipAt(GetTargetWeaponSocket(), item, ref currentWeaponInstance, combatStance);
                equippedWeaponItem = item;
                RefreshHitboxZoneFromInstance(combatStance ? currentWeaponInstance : null);
                RefreshWeaponHitboxReference();
                RefreshSheathDecoration();
                return true;
            case EquipmentSlotType.Shield:
                EquipAt(GetShieldSocket(), item, ref currentShieldInstance, true);
                equippedShieldItem = item;
                return true;
            default:
                Debug.Log($"[Outfit] 槽位 {item.EquipmentSlot} 的装备系统尚未开放：{item.DisplayName}");
                return false;
        }
    }

    /// <summary>卸下指定槽位并恢复默认外观；返回卸下的物品（由调用方放回背包）。</summary>
    public ItemDefinition Unequip(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon:
                ItemDefinition weaponItem = equippedWeaponItem;
                ClearSlot(GetWeaponSocket());
                ClearSlot(GetSheathSocket());
                currentWeaponInstance = null;
                equippedWeaponItem = null;
                EquipDefaultWeapon();
                return weaponItem;
            case EquipmentSlotType.Shield:
                ItemDefinition shieldItem = equippedShieldItem;
                ClearSlot(GetShieldSocket());
                currentShieldInstance = null;
                equippedShieldItem = null;
                return shieldItem;
            default:
                return null;
        }
    }

    // 恢复默认武器外观（无默认物品定义时保留或找回初始实例）。
    private void EquipDefaultWeapon()
    {
        if (defaultWeaponItem != null && defaultWeaponItem.EquipmentPrefab != null)
        {
            EquipAt(GetTargetWeaponSocket(), defaultWeaponItem, ref currentWeaponInstance, combatStance);
            equippedWeaponItem = null;
            RefreshHitboxZoneFromInstance(combatStance ? currentWeaponInstance : null);
            RefreshWeaponHitboxReference();
            RefreshSheathDecoration();
            return;
        }

        Transform socket = GetWeaponSocket();
        currentWeaponInstance = FindWeaponInstance(socket);
        if (currentWeaponInstance == null)
            currentWeaponInstance = FindWeaponInstance(GetSheathSocket());
        AttachWeaponToSocket(currentWeaponInstance, combatStance);
    }

    // 在指定挂点按物品参数装备外观。
    private void EquipAt(Transform socket, ItemDefinition item, ref GameObject instanceField, bool toHand)
    {
        if (item.EquipmentSlot == EquipmentSlotType.Weapon)
        {
            // 武器会在手部与背部之间搬运，两个挂点都要先清空旧模型。
            ClearSlot(GetWeaponSocket());
            ClearSlot(GetSheathSocket());
        }
        else
        {
            ClearSlot(socket);
        }

        instanceField = Instantiate(item.EquipmentPrefab, socket);
        instanceField.name = item.EquipmentPrefab.name;
        ApplySocketTransform(instanceField.transform, item, toHand);
        SetLayerRecursive(instanceField, 6);
    }

    // 把武器实例搬到目标挂点，并按当前挂点重新计算命中盒。
    private void AttachWeaponToSocket(GameObject instance, bool toHand)
    {
        if (instance != null)
        {
            Transform target = toHand ? GetWeaponSocket() : GetSheathSocket();
            ItemDefinition item = equippedWeaponItem != null ? equippedWeaponItem : defaultWeaponItem;
            instance.transform.SetParent(target, false);
            ApplySocketTransform(instance.transform, item, toHand);
            SetLayerRecursive(instance, 6);
        }

        // 命中盒只在武器到手时按真实包围盒适配，收剑态收紧避免残留判定。
        if (toHand)
            RefreshHitboxZoneFromInstance(instance);
        else
            CollapseHitboxZone();

        RefreshSheathDecoration();
        RefreshWeaponHitboxReference();
    }

    // 按挂点应用武器实例的局部变换：手部用物品配置的偏移，背部用收纳偏移并对齐装饰剑位置。
    private void ApplySocketTransform(Transform instance, ItemDefinition item, bool toHand)
    {
        instance.localScale = Vector3.one * (item != null ? item.EquipmentScale : 100f);

        if (toHand)
        {
            if (item != null)
            {
                instance.localPosition = item.EquipmentPosition;
                instance.localRotation = Quaternion.Euler(item.EquipmentEuler);
            }
            else
            {
                instance.localPosition = Vector3.zero;
                instance.localRotation = Quaternion.identity;
            }
            return;
        }

        instance.localPosition = Vector3.zero;
        instance.localRotation = Quaternion.Euler(sheathLocalEuler);

        // 包围盒中心对齐到装饰剑位置，抵消不同武器模型 pivot 的差异，再叠加手动偏移。
        if (autoAlignSheathToDecoration && hasSheathReference)
        {
            Bounds bounds = GetWorldBounds(instance.gameObject);
            Vector3 targetWorld = GetSheathSocket().TransformPoint(sheathReferenceLocalCenter);
            instance.position += targetWorld - bounds.center;
        }

        instance.localPosition += sheathLocalPosition;
    }

    // 累加某个对象下所有渲染器的世界包围盒。
    // 必须用 Renderer.bounds（Unity 已正确处理旋转与骨骼缩放），
    // 用 localBounds 乘 lossyScale 的写法在挂点带大角度旋转时会明显偏大，
    // 曾把武器对齐点算高半米。
    private static Bounds GetWorldBounds(GameObject root)
    {
        bool hasBounds = false;
        Bounds total = new Bounds();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                total = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                total.Encapsulate(renderer.bounds);
            }
        }

        return total;
    }

    // 清空挂点下的装备实例（保留手指骨骼与常驻命中盒）。
    // 只销毁 SM_Wep_* 装备模型；WeaponHitboxZone 不属于该前缀，必须保留。
    private void ClearSlot(Transform socket)
    {
        if (socket == null)
            return;

        for (int i = socket.childCount - 1; i >= 0; i--)
        {
            Transform child = socket.GetChild(i);
            if (child.name.StartsWith(EquipmentPrefix))
                Destroy(child.gameObject);
        }
    }

    // 查找挂点下现存的装备实例。
    private GameObject FindWeaponInstance(Transform socket)
    {
        if (socket == null)
            return null;

        foreach (Transform child in socket)
        {
            if (child.name.StartsWith(EquipmentPrefix))
                return child.gameObject;
        }

        return null;
    }

    // 获取武器挂点骨骼。
    private Transform GetWeaponSocket() => GetOrCreateSocket(WeaponSocketName);

    // 获取盾牌挂点骨骼。
    private Transform GetShieldSocket() => GetOrCreateSocket(ShieldSocketName);

    // 获取当前战斗态对应武器挂点。
    private Transform GetTargetWeaponSocket() => combatStance ? GetWeaponSocket() : GetSheathSocket();

    // 获取背部收纳挂点；缺失时退回手部挂点（退化为"始终持在手上"）。
    private Transform GetSheathSocket() => weaponSheathSocket != null ? weaponSheathSocket : GetWeaponSocket();

    // 按骨骼名查找挂点；缺失时回退到根节点并报错。
    private Transform GetOrCreateSocket(string boneName)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name == boneName)
                return child;
        }

        Debug.LogError($"[Outfit] 角色骨骼中找不到挂点 {boneName}，使用根节点代替。", this);
        return transform;
    }

    // 解析背部收纳挂点：优先用 Inspector 指定，否则在骨骼里找默认命名的装饰剑节点。
    private void ResolveSheathSocket()
    {
        if (weaponSheathSocket == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name != DefaultSheathName)
                    continue;

                weaponSheathSocket = child;
                break;
            }
        }

        if (weaponSheathSocket == null)
        {
            Debug.LogWarning($"[Outfit] 找不到背部收纳挂点 {DefaultSheathName}，收剑态武器仍会留在手上。", this);
            return;
        }

        sheathDecorationRenderer = weaponSheathSocket.GetComponentInChildren<Renderer>(true);
        CacheSheathReference();
    }

    // 记录背上装饰剑的包围盒中心（换算到收纳挂点局部空间），供武器自动对齐使用。
    private void CacheSheathReference()
    {
        if (weaponSheathSocket == null)
            return;

        Bounds worldBounds = GetWorldBounds(weaponSheathSocket.gameObject);
        if (worldBounds.size == Vector3.zero)
        {
            hasSheathReference = false;
            return;
        }

        sheathReferenceLocalCenter = weaponSheathSocket.InverseTransformPoint(worldBounds.center);
        hasSheathReference = true;
    }

    // 装备了实体武器时隐藏背上的默认装饰剑，避免两层模型重叠。
    private void RefreshSheathDecoration()
    {
        if (sheathDecorationRenderer != null)
            sheathDecorationRenderer.enabled = currentWeaponInstance == null;
    }

    // 装备实例及子物体放到玩家受击层（layer 6），与初始武器一致。
    private static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // 确保常驻武器命中盒存在。
    private void EnsureHitboxZone()
    {
        Transform socket = GetWeaponSocket();
        Transform zone = socket.Find(HitboxZoneName);

        if (zone == null)
        {
            GameObject zoneObject = new GameObject(HitboxZoneName, typeof(BoxCollider), typeof(WeaponHitbox));
            zoneObject.transform.SetParent(socket, false);
            zone = zoneObject.transform;
        }

        hitboxZoneCollider = zone.GetComponent<BoxCollider>();
        hitboxZoneCollider.isTrigger = true;
    }

    // 收剑态把命中盒收缩到极小，避免背着武器时误触发判定。
    private void CollapseHitboxZone()
    {
        if (hitboxZoneCollider == null)
            return;

        hitboxZoneCollider.center = Vector3.zero;
        hitboxZoneCollider.size = Vector3.one * 0.01f;
    }

    // 按手上真实武器实例的渲染包围盒自适应命中盒尺寸与中心。
    // 注意：必须测量已挂载实例（经历骨骼 lossyScale 与物品缩放后的真实世界尺寸），
    // 用固定缩放实例化采样会在骨骼缩放环境下把尺寸算错上百倍（曾致全图判定）。
    private void RefreshHitboxZoneFromInstance(GameObject weaponInstance)
    {
        if (hitboxZoneCollider == null)
            return;

        Transform zone = hitboxZoneCollider.transform;

        if (weaponInstance == null)
        {
            // 手空：收缩到极小尺寸，避免残留判定。
            CollapseHitboxZone();
            return;
        }

        bool hasBounds = false;
        Bounds totalBounds = new Bounds();
        foreach (Renderer renderer in weaponInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                totalBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        // 世界包围盒 -> Zone 本地空间（Zone 局部缩放为 1，直接除以 lossyScale 得到本地尺寸）。
        Vector3 lossy = zone.lossyScale;
        hitboxZoneCollider.center = zone.InverseTransformPoint(totalBounds.center);
        hitboxZoneCollider.size = new Vector3(
            Mathf.Max(0.01f, totalBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x))),
            Mathf.Max(0.01f, totalBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y))),
            Mathf.Max(0.01f, totalBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z))));

        RefreshWeaponHitboxReference();
    }

    // 重新缓存 PlayerCombat 的命中盒引用（换装后必须调用）。
    private void RefreshWeaponHitboxReference()
    {
        weaponHitbox = GetComponentInChildren<WeaponHitbox>(true);
        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null)
            combat.RefreshWeaponHitbox(weaponHitbox);
    }
}
