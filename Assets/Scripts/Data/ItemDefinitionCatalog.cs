using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按稳定 ID 集中索引物品配置资产的目录。
/// 存档、商店和掉落系统通过它将持久化 ID 解析为 ItemDefinition。
/// </summary>
[CreateAssetMenu(fileName = "ItemDefinitionCatalog", menuName = "RPG/Items/Item Definition Catalog")]
public sealed class ItemDefinitionCatalog : ScriptableObject
{
    // 需要被运行时 ID 查询的所有物品配置资产。
    [SerializeField] private ItemDefinition[] items;

    // 延迟建立的物品 ID 查找表，不参与资源序列化。
    private Dictionary<int, ItemDefinition> itemLookup;

    /// <summary>按稳定 ID 查询对应的物品定义。</summary>
    public bool TryGetItem(int itemId, out ItemDefinition item)
    {
        EnsureLookup();
        return itemLookup.TryGetValue(itemId, out item);
    }

    // 在首次查询时建立目录索引，并报告可能破坏存档的重复 ID。
    private void EnsureLookup()
    {
        if (itemLookup != null)
            return;

        itemLookup = new Dictionary<int, ItemDefinition>();
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemDefinition item = items[i];
            if (item == null)
                continue;

            if (item.Id <= 0 || !itemLookup.TryAdd(item.Id, item))
                Debug.LogError($"[{nameof(ItemDefinitionCatalog)}] Invalid or duplicate item ID on '{item.name}'.", this);
        }
    }
}
