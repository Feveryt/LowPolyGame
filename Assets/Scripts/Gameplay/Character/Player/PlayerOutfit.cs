using UnityEngine;

/// <summary>
/// 玩家换装组件：管理武器/盾牌的外观模型挂载。
/// 挂点为角色骨骼 Hand_R（武器）与 Hand_L（盾牌）；
/// 装备时销毁旧模型、按 ItemDefinition 的偏移参数实例化新模型。
/// 武器命中盒常驻于武器挂点下的 WeaponHitboxZone（换装时按手上真实实例的包围盒自适应尺寸），
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

    // 武器命中盒组件（PlayerCombat 的判定依赖它）。
    [SerializeField] private WeaponHitbox weaponHitbox;
    // 初始武器对应的物品定义（卸下武器时恢复外观）。
    [SerializeField] private ItemDefinition defaultWeaponItem;

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

    // 初始化：解析命中盒引用并建立常驻命中盒。
    private void Awake()
    {
        weaponHitbox = weaponHitbox != null ? weaponHitbox : GetComponentInChildren<WeaponHitbox>(true);
        EnsureHitboxZone();
    }

    // 在所有 Awake 完成后接管初始武器并重新缓存 PlayerCombat 的命中盒引用。
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

        RefreshHitboxZoneFromInstance(currentWeaponInstance);
        RefreshWeaponHitboxReference();
    }

    /// <summary>按物品配置装备到对应槽位；返回是否成功。</summary>
    public bool Equip(ItemDefinition item)
    {
        if (item == null || !item.IsEquippable || item.EquipmentPrefab == null)
            return false;

        switch (item.EquipmentSlot)
        {
            case EquipmentSlotType.Weapon:
                EquipAt(GetWeaponSocket(), item, ref currentWeaponInstance);
                equippedWeaponItem = item;
                RefreshHitboxZoneFromInstance(currentWeaponInstance);
                return true;
            case EquipmentSlotType.Shield:
                EquipAt(GetShieldSocket(), item, ref currentShieldInstance);
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
            EquipAt(GetWeaponSocket(), defaultWeaponItem, ref currentWeaponInstance);
            equippedWeaponItem = null;
            RefreshHitboxZoneFromInstance(currentWeaponInstance);
            return;
        }

        Transform socket = GetWeaponSocket();
        currentWeaponInstance = FindWeaponInstance(socket);
        RefreshHitboxZoneFromInstance(currentWeaponInstance);
    }

    // 在指定挂点按物品参数装备外观。
    private void EquipAt(Transform socket, ItemDefinition item, ref GameObject instanceField)
    {
        ClearSlot(socket);

        instanceField = Instantiate(item.EquipmentPrefab, socket);
        instanceField.name = item.EquipmentPrefab.name;
        instanceField.transform.localPosition = item.EquipmentPosition;
        instanceField.transform.localRotation = Quaternion.Euler(item.EquipmentEuler);
        instanceField.transform.localScale = Vector3.one * item.EquipmentScale;
        SetLayerRecursive(instanceField, 6);
    }

    // 清空挂点下的装备实例（保留手指骨骼与常驻命中盒）。
    // 只销毁 SM_Wep_* 装备模型；WeaponHitboxZone 不属于该前缀，必须保留。
    private void ClearSlot(Transform socket)
    {
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
            hitboxZoneCollider.center = Vector3.zero;
            hitboxZoneCollider.size = Vector3.one * 0.01f;
            RefreshWeaponHitboxReference();
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
