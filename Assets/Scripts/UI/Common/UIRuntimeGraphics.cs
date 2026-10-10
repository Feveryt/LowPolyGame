using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时 UI 图形与布局工具，集中解决两件事：
/// 1）程序生成圆角九宫格贴图——素材包 GUI Kit The Stone 的内嵌面板贴图（popup_bg）九宫格边界高达 164/153 像素，
///    在 200 像素以下的小卡片上会被 Unity 强制压缩导致边框错位；这里用程序生成的九宫格圆角贴图替代，
///    可任意尺寸拉伸且与主题内既有的深色玻璃容器（Scroll View / Item Detail Panel）观感一致。
///    实心版本用于容器背板与勾选框，中空描边版本用于给槽位加一圈边框。
/// 2）面板画布自适应——把固定设计尺寸的面板等比缩放并约束进画布安全区，
///    保证在超宽屏（21:9 / 32:9）等短画布分辨率下面板既不溢出屏幕、内部对齐也不被破坏。
/// </summary>
public static class UIRuntimeGraphics
{
    // 实心圆角贴图：边长与圆角半径（像素），九宫格边界取半径值，中心留出可拉伸区域。
    private const int PanelTextureSize = 28;
    private const int PanelRadius = 9;
    // 中空描边贴图：边长、圆角半径与描边厚度。
    private const int FrameTextureSize = 34;
    private const int FrameRadius = 11;
    private const int FrameThickness = 4;
    // 边缘抗锯齿的超采样倍率。
    private const int EdgeSamples = 3;

    // 程序生成的精灵缓存。
    private static Sprite roundedPanelSprite;
    private static Sprite roundedFrameSprite;

    /// <summary>实心圆角矩形九宫格精灵（首次访问时生成并缓存，可安全用于 Image.Type.Sliced）。</summary>
    public static Sprite RoundedPanel
    {
        get
        {
            if (roundedPanelSprite == null)
                roundedPanelSprite = CreateRoundedSprite(PanelTextureSize, PanelRadius, 0, "UI_RoundedPanel");

            return roundedPanelSprite;
        }
    }

    /// <summary>中空圆角矩形描边九宫格精灵，用于给槽位／卡片加一圈边框。</summary>
    public static Sprite RoundedFrame
    {
        get
        {
            if (roundedFrameSprite == null)
                roundedFrameSprite = CreateRoundedSprite(FrameTextureSize, FrameRadius, FrameThickness, "UI_RoundedFrame");

            return roundedFrameSprite;
        }
    }

    /// <summary>
    /// 把固定设计尺寸的面板等比缩放进画布安全区，避免在矮画布（超宽屏）分辨率下面板溢出屏幕。
    /// 只做等比缩放，不改变内部锚点关系，因此所有相对对齐关系都会被保留。
    /// </summary>
    /// <param name="panel">要约束的面板根节点。</param>
    /// <param name="designSize">面板的设计尺寸（参考分辨率下的像素值）。</param>
    /// <param name="margin">面板与画布边缘之间保留的最小空白。</param>
    public static void FitPanelToCanvas(RectTransform panel, Vector2 designSize, float margin = 36f)
    {
        if (panel == null)
            return;

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            panel.localScale = Vector3.one;
            return;
        }

        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x <= 1f || canvasSize.y <= 1f)
        {
            panel.localScale = Vector3.one;
            return;
        }

        float usableWidth = Mathf.Max(1f, canvasSize.x - margin * 2f);
        float usableHeight = Mathf.Max(1f, canvasSize.y - margin * 2f);
        float scale = Mathf.Min(1f, usableWidth / Mathf.Max(1f, designSize.x), usableHeight / Mathf.Max(1f, designSize.y));
        // 过小的缩放会让文字不可读，这里保留一个下限，宁可轻微溢出也要保证可读性。
        scale = Mathf.Clamp(scale, 0.5f, 1f);

        panel.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// 创建一个使用圆角九宫格精灵的 Image 节点，用于 UI 分组或槽位边框。
    /// </summary>
    /// <param name="parent">父节点。</param>
    /// <param name="name">节点名。</param>
    /// <param name="sprite">九宫格精灵（一般传 RoundedPanel 或 RoundedFrame）。</param>
    /// <param name="color">颜色。</param>
    public static RectTransform CreateRoundedElement(RectTransform parent, string name, Sprite sprite, Color color)
    {
        GameObject node = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = node.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    /// <summary>创建一个铺满父级、带圆角半透明背板的节点，用于 UI 分组（声音卡片 / 装备区块）。</summary>
    public static RectTransform CreateRoundedBackdrop(RectTransform parent, string name, Color fill)
    {
        return CreateRoundedElement(parent, name, RoundedPanel, fill);
    }

    // 生成圆角矩形九宫格贴图：对每个像素做超采样，得到平滑的圆角边缘。
    // thickness 为 0 时生成实心圆角矩形，大于 0 时生成对应厚度的中空描边。
    private static Sprite CreateRoundedSprite(int size, int radius, int thickness, string spriteName)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float outerRadius = radius;
        float innerSize = size - thickness * 2f;
        float innerRadius = Mathf.Max(0f, radius - thickness);
        float sampleStep = 1f / EdgeSamples;
        int totalSamples = EdgeSamples * EdgeSamples;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < EdgeSamples; sy++)
                {
                    for (int sx = 0; sx < EdgeSamples; sx++)
                    {
                        float px = x + (sx + 0.5f) * sampleStep;
                        float py = y + (sy + 0.5f) * sampleStep;
                        if (!IsInsideRoundedRect(px, py, size, outerRadius))
                            continue;

                        // 中空描边：落在内圈范围内的采样点不计入。
                        if (thickness > 0 &&
                            IsInsideRoundedRect(px - thickness, py - thickness, innerSize, innerRadius))
                            continue;

                        covered++;
                    }
                }

                byte alpha = (byte)Mathf.RoundToInt(255f * covered / totalSamples);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        // 九宫格边界取"圆角半径 + 描边厚度"，保证四角图案不被拉伸。
        float border = radius + thickness;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0u, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // 判断采样点是否落在圆角矩形内部：仅在四角区域需要做圆判定，其余区域直接为内部。
    private static bool IsInsideRoundedRect(float px, float py, float size, float radius)
    {
        float distanceToHorizontalEdge = Mathf.Min(px, size - px);
        float distanceToVerticalEdge = Mathf.Min(py, size - py);
        if (distanceToHorizontalEdge >= radius || distanceToVerticalEdge >= radius)
            return true;

        float dx = radius - distanceToHorizontalEdge;
        float dy = radius - distanceToVerticalEdge;
        return dx * dx + dy * dy <= radius * radius;
    }
}
