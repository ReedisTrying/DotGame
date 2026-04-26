using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗奖励中的单个道具选项卡片
/// </summary>
public class RewardItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image borderImage;

    private ItemConfigEntry itemConfig;
    private System.Action<ItemConfigEntry> onSelectCallback;

    public void Setup(ItemConfigEntry config, System.Action<ItemConfigEntry> onSelect)
    {
        itemConfig = config;
        onSelectCallback = onSelect;

        if (nameText != null)
        {
            nameText.text = config.DisplayName;
            nameText.color = ItemCatalog.GetRarityColor(config.Rarity);
        }

        if (descriptionText != null)
            descriptionText.text = config.Description;

        if (rarityText != null)
        {
            string rarityName = config.Rarity == ItemRarity.Common ? "普通" :
                                config.Rarity == ItemRarity.Uncommon ? "稀有" : "史诗";
            rarityText.text = rarityName;
            rarityText.color = ItemCatalog.GetRarityColor(config.Rarity);
        }

        if (borderImage != null)
            borderImage.color = ItemCatalog.GetRarityColor(config.Rarity);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelectCallback?.Invoke(itemConfig));
        }
    }
}
