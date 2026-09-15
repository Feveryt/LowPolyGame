using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 校验商店商品行预制体的资源与引用完整性。
/// 防止后续在编辑器里删除子节点或漏绑引用后，商店打开时静默丢失商品行内容。
/// </summary>
public sealed class ShopItemRowPrefabEditModeTests
{
    // 商品行预制体在 Resources 下的固定路径。
    private const string PrefabPath = "Prefabs/UI/ShopItemRow";
    // ShopItemRow 必须绑定的全部序列化引用。
    private static readonly string[] RequiredFields =
    {
        "icon",
        "nameText",
        "ownedText",
        "priceText",
        "amountText",
        "decreaseButton",
        "increaseButton",
        "buyButton",
        "sellButton",
    };

    // 行预制体必须存在并可加载。
    [Test]
    public void RowPrefab_ExistsInResources()
    {
        Assert.IsNotNull(Resources.Load<GameObject>(PrefabPath), $"未找到 Resources/{PrefabPath}.prefab");
    }

    // 行预制体根节点必须挂有 ShopItemRow 组件。
    [Test]
    public void RowPrefab_HasShopItemRowComponent()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, $"未找到 Resources/{PrefabPath}.prefab");
        Assert.IsNotNull(prefab.GetComponent<ShopItemRow>(), "商品行预制体缺少 ShopItemRow 组件。");
    }

    // 行预制体的全部序列化引用都必须绑定到子节点。
    [Test]
    public void RowPrefab_HasAllReferencesBound()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, $"未找到 Resources/{PrefabPath}.prefab");

        ShopItemRow row = prefab.GetComponent<ShopItemRow>();
        Assert.IsNotNull(row, "商品行预制体缺少 ShopItemRow 组件。");

        SerializedObject serialized = new SerializedObject(row);
        foreach (string field in RequiredFields)
        {
            SerializedProperty property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"ShopItemRow 缺少序列化字段 {field}。");
            Assert.IsNotNull(property.objectReferenceValue, $"ShopItemRow 的 {field} 未绑定引用。");
        }
    }
}
