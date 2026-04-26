using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 单个道具槽UI - 显示道具图标，悬浮/点击显示tooltip
/// </summary>
public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameLabel;

    private ItemType itemType = ItemType.None;
    private GameObject tooltipObj;
    private TextMeshProUGUI tooltipText;

    /// <summary>
    /// 设置此槽位显示的道具
    /// </summary>
    public void SetItem(ItemType type, Sprite icon = null)
    {
        itemType = type;

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.color = GetItemColor(type);
            }
            else
            {
                // 无图标时用纯色方块代替
                iconImage.sprite = null;
                iconImage.color = GetItemColor(type);
            }
        }

        if (nameLabel != null)
        {
            nameLabel.text = ItemManager.GetItemDisplayName(type);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 点击切换tooltip
        if (tooltipObj != null && tooltipObj.activeSelf)
            HideTooltip();
        else
            ShowTooltip();
    }

    private void ShowTooltip()
    {
        if (itemType == ItemType.None) return;

        if (tooltipObj == null)
        {
            CreateTooltip();
        }

        if (tooltipText != null)
        {
            tooltipText.text = $"<b>{ItemManager.GetItemDisplayName(itemType)}</b>\n{ItemManager.GetItemDescription(itemType)}";
        }

        if (tooltipObj != null)
            tooltipObj.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipObj != null)
            tooltipObj.SetActive(false);
    }

    private void CreateTooltip()
    {
        tooltipObj = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObj.transform.SetParent(transform, false);

        var bg = tooltipObj.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var rect = tooltipObj.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta = new Vector2(250, 80);

        // 文字子对象
        var textObj = new GameObject("TooltipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(tooltipObj.transform, false);

        tooltipText = textObj.GetComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 14;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.Center;

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 5);
        textRect.offsetMax = new Vector2(-5, -5);

        tooltipObj.SetActive(false);
    }

    /// <summary>
    /// 获取道具对应的主题色
    /// </summary>
    private static Color GetItemColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.DragonBloodRed: return new Color(0.8f, 0.1f, 0.1f); // 深红
            case ItemType.RedMaggot: return new Color(0.6f, 0.2f, 0.2f);      // 暗红
            case ItemType.PompeiiRed: return new Color(0.9f, 0.3f, 0.2f);     // 橙红
            case ItemType.Cinnabar: return new Color(0.9f, 0.2f, 0.0f);       // 朱红
            case ItemType.WineRed: return new Color(0.5f, 0.0f, 0.1f);        // 酒红
            default: return Color.gray;
        }
    }
}
