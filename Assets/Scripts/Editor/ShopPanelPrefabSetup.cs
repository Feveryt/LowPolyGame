using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>生成可交由 UIManager 实例化的商店面板预制体，并绑定 ShopPanel 的静态界面引用。</summary>
[InitializeOnLoad]
public static class ShopPanelPrefabSetup
{
    // UIManager 面板条目引用的商店预制体路径。
    private const string PrefabPath = "Assets/Resources/Prefabs/UI/ShopPanel.prefab";
    // 防止一次域重载中重复排入构建任务。
    private static bool createScheduled;

    // 脚本导入完成后自动补齐缺失的商店预制体。
    static ShopPanelPrefabSetup()
    {
        ScheduleCreateIfMissing();
    }

    /// <summary>从 Unity 菜单重新生成商店预制体，用于后续修改基础视觉层级。</summary>
    [MenuItem("RPG/Setup/Create Shop Panel")]
    public static void CreateShopPanel()
    {
        CreateOrReplacePrefab();
    }

    // 延迟到脚本域初始化结束后再创建，避免在导入过程写入资产。
    private static void ScheduleCreateIfMissing()
    {
        if (createScheduled || IsPrefabReady())
            return;

        createScheduled = true;
        EditorApplication.delayCall += () =>
        {
            createScheduled = false;
            if (!IsPrefabReady())
                CreateOrReplacePrefab();
        };
    }

    // 检查资产是否已经拥有商店运行时需要的静态内容根节点。
    private static bool IsPrefabReady()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return prefab != null && prefab.transform.Find("Shop Window/Items Frame/Items") != null;
    }

    // 创建无 Canvas 的全屏面板，供 UIManager 挂入场景已有画布。
    private static void CreateOrReplacePrefab()
    {
        string directory = Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            Directory.CreateDirectory(directory);

        GameObject root = new GameObject("ShopPanel", typeof(RectTransform));
        ShopPanel panel = root.AddComponent<ShopPanel>();
        panel.BuildPrefabLayout();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShopPanelPrefabSetup] Shop panel prefab created at " + PrefabPath);
    }
}
