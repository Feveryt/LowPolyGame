using TMPro;
using UnityEngine;

/// <summary>
/// 集中保存石质 UI 的字体与常用精灵，供场景 UI 和运行时生成的加载/对话选项复用。
/// </summary>
[CreateAssetMenu(fileName = "StoneUiTheme", menuName = "RPG/UI/Stone UI Theme")]
public sealed class StoneUiTheme : ScriptableObject
{
    // 中文与通用界面文字使用的 TMP 字体。
    [Header("Typography")]
    [SerializeField] private TMP_FontAsset chineseFont;
    // 英文标题和数字可使用的石质资源包字体。
    [SerializeField] private TMP_FontAsset displayFont;

    // 弹窗和对话框使用的石质九宫格背景。
    [Header("Surfaces")]
    [SerializeField] private Sprite panelSprite;
    // 面板标题使用的石质飘带。
    [SerializeField] private Sprite titleSprite;
    // 常规确认按钮的石质背景。
    [SerializeField] private Sprite primaryButtonSprite;
    // 危险操作按钮的石质背景。
    [SerializeField] private Sprite dangerButtonSprite;
    // 物品槽位使用的石质边框。
    [SerializeField] private Sprite itemSlotSprite;
    // 当前选中槽位使用的高亮边框。
    [SerializeField] private Sprite selectionSprite;

    // 资源条的通用底图。
    [Header("Status")]
    [SerializeField] private Sprite resourceBackgroundSprite;
    // 生命资源条填充图片。
    [SerializeField] private Sprite healthFillSprite;
    // 体力资源条填充图片。
    [SerializeField] private Sprite staminaFillSprite;
    // 蓝量资源条填充图片。
    [SerializeField] private Sprite manaFillSprite;
    // 金币状态图标。
    [SerializeField] private Sprite coinSprite;

    /// <summary>中文与通用界面文字使用的字体。</summary>
    public TMP_FontAsset ChineseFont => chineseFont;
    /// <summary>英文标题和数字使用的展示字体。</summary>
    public TMP_FontAsset DisplayFont => displayFont != null ? displayFont : chineseFont;
    /// <summary>石质弹窗背景。</summary>
    public Sprite PanelSprite => panelSprite;
    /// <summary>石质标题飘带。</summary>
    public Sprite TitleSprite => titleSprite;
    /// <summary>常规石质按钮背景。</summary>
    public Sprite PrimaryButtonSprite => primaryButtonSprite;
    /// <summary>危险操作按钮背景。</summary>
    public Sprite DangerButtonSprite => dangerButtonSprite;
    /// <summary>物品格背景。</summary>
    public Sprite ItemSlotSprite => itemSlotSprite;
    /// <summary>物品格选中边框。</summary>
    public Sprite SelectionSprite => selectionSprite;
    /// <summary>资源条背景。</summary>
    public Sprite ResourceBackgroundSprite => resourceBackgroundSprite;
    /// <summary>生命条填充图片。</summary>
    public Sprite HealthFillSprite => healthFillSprite;
    /// <summary>体力条填充图片。</summary>
    public Sprite StaminaFillSprite => staminaFillSprite;
    /// <summary>蓝量条填充图片。</summary>
    public Sprite ManaFillSprite => manaFillSprite;
    /// <summary>金币图标。</summary>
    public Sprite CoinSprite => coinSprite;
}
