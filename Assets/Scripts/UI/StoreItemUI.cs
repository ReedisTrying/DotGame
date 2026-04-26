using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店中单个道具卡片UI
/// </summary>
public class StoreItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;
    [SerializeField] private Image borderImage;

    private ItemConfigEntry itemConfig;
    private System.Action<ItemConfigEntry> onBuyCallback;

    public void Setup(ItemConfigEntry config, System.Action<ItemConfigEntry> onBuy)
    {
        itemConfig = config;
        onBuyCallback = onBuy;

        if (nameText != null)
        {
            nameText.text = config.DisplayName;
            nameText.color = ItemCatalog.GetRarityColor(config.Rarity);
        }

        if (descriptionText != null)
            descriptionText.text = config.Description;

        if (priceText != null)
            priceText.text = $"{config.Price} 金粉";

        if (rarityText != null)
        {
            string rarityName = config.Rarity == ItemRarity.Common ? "普通" :
                                config.Rarity == ItemRarity.Uncommon ? "稀有" : "史诗";
            rarityText.text = rarityName;
            rarityText.color = ItemCatalog.GetRarityColor(config.Rarity);
        }

        if (borderImage != null)
            borderImage.color = ItemCatalog.GetRarityColor(config.Rarity);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        RefreshAffordability();
    }

    public void RefreshAffordability()
    {
        if (itemConfig == null) return;

        int money = SaveManager.Instance != null ? SaveManager.Instance.GetMoney() : 0;
        bool canAfford = money >= itemConfig.Price;
        bool alreadyOwned = ItemManager.Instance != null && ItemManager.Instance.HasItem(itemConfig.Type);

        if (buyButton != null)
            buyButton.interactable = canAfford && !alreadyOwned;

        if (buyButtonText != null)
        {
            if (alreadyOwned)
                buyButtonText.text = "已拥有";
            else if (!canAfford)
                buyButtonText.text = "金粉不足";
            else
                buyButtonText.text = "购买";
        }
    }

    public void SetSoldOut()
    {
        if (buyButton != null)
            buyButton.interactable = false;

        if (buyButtonText != null)
            buyButtonText.text = "已售出";
    }

    private void OnBuyClicked()
    {
        if (itemConfig == null) return;
        onBuyCallback?.Invoke(itemConfig);
    }
}
