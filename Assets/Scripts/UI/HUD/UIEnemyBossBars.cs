using System.Collections.Generic;
using UnityEngine;

/// <summary>屏幕中下方的敌人血条容器：自动跟踪索敌对象，多目标按完整的血条/名称行向上堆叠。</summary>
[DisallowMultipleComponent]
public sealed class UIEnemyBossBars : MonoBehaviour
{
    // 在 Inspector 中可独立编辑的单行血条预制体。
    [SerializeField] private UIEnemyBossBar barPrefab;
    // 依照设备安全区域约束的根容器。
    [SerializeField] private RectTransform safeAreaRoot;
    // 血条堆叠的父节点，左右留白由锚点决定。
    [SerializeField] private RectTransform rowsRoot;
    // 最底一行距离屏幕安全区底边的设计像素数。
    [SerializeField, Min(0f)] private float bottomOffset = 104f;
    // 相邻整行的垂直距离，包含血槽、名称和额外留白。
    [SerializeField, Min(70f)] private float rowSpacing = 88f;
    // 敌人死亡后保留空血槽的时间，单位秒。
    [SerializeField, Min(0f)] private float deathHold = 0.8f;
    // 每条血条的绑定与过渡状态。
    private readonly List<Row> rows = new List<Row>();
    // 上次应用的设备安全区域。
    private Rect lastSafeArea;
    // 上次用于安全区域计算的屏幕大小。
    private Vector2Int lastScreenSize;

    // 单个目标对应的持久行，其他目标加入时不会交换身份。
    private sealed class Row
    {
        // 该行绑定的敌人实例。
        public EnemyBossHealthTarget Target;
        // 单行血槽与名称的表现。
        public UIEnemyBossBar View;
        // 当前淡入淡出透明度。
        public float Alpha;
        // 首次观察到死亡的时间；负数表示尚未死亡。
        public float DiedAt = -1f;
    }

    /// <summary>当前容器中的行数，包括淡出中的行。</summary>
    public int VisibleBarCount => rows.Count;

    // 每帧复用已登记目标，无需查找场景对象；同步战斗状态、显隐及排列。
    private void Update()
    {
        UpdateSafeArea();
        foreach (EnemyBossHealthTarget target in EnemyBossHealthTarget.ActiveTargets)
        {
            if (target == null || !target.IsEngagedWithPlayer || HasRow(target))
                continue;
            UIEnemyBossBar view = Instantiate(barPrefab, rowsRoot, false);
            view.name = "BossBar_" + target.name;
            view.Bind(target);
            rows.Add(new Row { Target = target, View = view });
        }

        for (int index = rows.Count - 1; index >= 0; index--)
        {
            Row row = rows[index];
            bool wanted = row.Target != null && row.Target.IsEngagedWithPlayer;
            if (row.Target != null && row.Target.isActiveAndEnabled && row.Target.Stats != null && !row.Target.Stats.IsAlive)
            {
                if (row.DiedAt < 0f)
                    row.DiedAt = Time.unscaledTime;
                wanted = Time.unscaledTime - row.DiedAt < deathHold;
            }
            else
                row.DiedAt = -1f;
            row.Alpha = Mathf.MoveTowards(row.Alpha, wanted ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            row.View.SetVisibility(row.Alpha);
            if (!wanted && row.Alpha <= 0f)
            {
                Destroy(row.View.gameObject);
                rows.RemoveAt(index);
            }
        }

        for (int index = 0; index < rows.Count; index++)
        {
            RectTransform rect = (RectTransform)rows[index].View.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 70f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset + index * rowSpacing);
        }
    }

    // 查找目标是否已有独立血条，防止每帧重复实例化。
    private bool HasRow(EnemyBossHealthTarget target)
    {
        foreach (Row row in rows)
            if (row.Target == target)
                return true;
        return false;
    }

    // 屏幕尺寸或安全区域变化时调整根锚点，兼容窗口缩放、超宽屏和刘海屏。
    private void UpdateSafeArea()
    {
        Rect safe = Screen.safeArea;
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (safe == lastSafeArea && size == lastScreenSize || size.x <= 0 || size.y <= 0)
            return;
        lastSafeArea = safe;
        lastScreenSize = size;
        safeAreaRoot.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        safeAreaRoot.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
        safeAreaRoot.offsetMin = safeAreaRoot.offsetMax = Vector2.zero;
    }

    // 关闭面板时销毁血条，重新启用后按当前敌人战斗状态重建。
    private void OnDisable()
    {
        foreach (Row row in rows)
            if (row.View != null)
                Destroy(row.View.gameObject);
        rows.Clear();
    }
}
