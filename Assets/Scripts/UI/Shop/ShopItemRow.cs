using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 商店列表中的单条商品视图，负责显示商品资料并维护数量、购买和出售操作。
/// 外观由 ShopItemRow 预制体决定，本脚本只写入运行时数据与交互回调。
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopItemRow : MonoBehaviour
{
    // 商品图标。
    [SerializeField] private Image icon;
    // 商品名称、持有数量和买卖价格文本。
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text ownedText;
    [SerializeField] private TMP_Text priceText;
    // 当前交易件数文本。
    [SerializeField] private TMP_Text amountText;
    // 数量加减与买卖按钮。
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;

    // 驱动交易操作并广播刷新结果的所属商店面板。
    private ShopPanel panel;

    /// <summary>此行代表的可交易商品配置。</summary>
    public ShopItemEntry Entry { get; private set; }
    /// <summary>玩家本次准备交易的件数。</summary>
    public int Amount { get; private set; } = 1;
    /// <summary>首个可交易控件，供面板打开时设置 UI 焦点。</summary>
    public Button BuyButton => buyButton;

    /// <summary>写入商品资料、复位交易数量并绑定交互回调。</summary>
    public void Configure(ShopItemEntry entry, ShopPanel owner)
    {
        Entry = entry;
        panel = owner;
        Amount = 1;

        if (nameText != null)
            nameText.text = entry.Item.DisplayName;
        if (priceText != null)
            priceText.text = $"买入：{entry.BuyPrice}    出售：{entry.SellPrice}";
        if (icon != null)
        {
            icon.sprite = entry.Item.Icon;
            icon.enabled = entry.Item.Icon != null;
        }

        Bind(decreaseButton, Decrease);
        Bind(increaseButton, Increase);
        Bind(buyButton, () => panel.Buy(this));
        Bind(sellButton, () => panel.Sell(this));
        UpdateAmount();
    }

    /// <summary>按当前背包刷新持有数量与购买、出售按钮状态。</summary>
    public void Refresh(PlayerInventory inventory)
    {
        int owned = 0;
        if (inventory != null)
        {
            foreach (InventorySlot slot in inventory.Slots)
                if (!slot.IsEmpty && slot.Item == Entry.Item)
                    owned += slot.Quantity;
        }

        if (ownedText != null)
            ownedText.text = $"拥有: {owned}";
        if (buyButton != null)
            buyButton.interactable = Entry.CanBuy;
        if (sellButton != null)
            sellButton.interactable = Entry.CanSell && owned >= Amount;
        UpdateAmount();
    }

    // 将交易数量上调到单次交互允许的最大值。
    private void Increase()
    {
        Amount = Mathf.Min(99, Amount + 1);
        UpdateAmount();
    }

    // 将交易数量下调并保留至少一件。
    private void Decrease()
    {
        Amount = Mathf.Max(1, Amount - 1);
        UpdateAmount();
    }

    // 将内存中的数量写入数量标签。
    private void UpdateAmount()
    {
        if (amountText != null)
            amountText.text = Amount.ToString();
    }

    // 清空旧回调后绑定新的按钮行为，避免预制体实例复用时重复触发。
    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
