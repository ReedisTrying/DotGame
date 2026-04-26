using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店管理器 - 挂载在Store场景中
/// 提供随机道具供玩家购买
/// </summary>
public class StoreManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("道具卡片的父容器")]
    [SerializeField] private Transform itemContainer;

    [Tooltip("道具卡片预制体（挂载StoreItemUI）")]
    [SerializeField] private GameObject storeItemPrefab;

    [Tooltip("离开按钮")]
    [SerializeField] private Button leaveButton;

    [Tooltip("金粉显示文本")]
    [SerializeField] private TextMeshProUGUI moneyText;

    [Tooltip("标题文本")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("提示文本（如'金粉不足'等反馈）")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Settings")]
    [Tooltip("每次展示的道具数量")]
    [SerializeField] private int itemsToShow = 3;

    private List<StoreItemUI> spawnedCards = new List<StoreItemUI>();

    private void Start()
    {
        // 确保ItemManager存在
        if (ItemManager.Instance == null)
        {
            GameObject obj = new GameObject("ItemManager");
            obj.AddComponent<ItemManager>();
        }

        // 从存档加载道具
        if (SaveManager.Instance != null)
            SaveManager.Instance.LoadItems();

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        if (titleText != null)
            titleText.text = "商店";

        GenerateShopItems();
        RefreshMoneyDisplay();
        ClearFeedback();
    }

    private void OnDestroy()
    {
        if (leaveButton != null)
            leaveButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 生成商店道具卡片
    /// </summary>
    private void GenerateShopItems()
    {
        // 清除旧卡片
        foreach (var card in spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        spawnedCards.Clear();

        // 获取可出售道具
        var items = ItemCatalog.GetRandomUnowned(itemsToShow);

        if (items.Count == 0)
        {
            ShowFeedback("没有更多道具可购买了");
            return;
        }

        if (storeItemPrefab == null || itemContainer == null)
        {
            Debug.LogWarning("[StoreManager] storeItemPrefab或itemContainer未设置");
            return;
        }

        foreach (var itemConfig in items)
        {
            GameObject cardObj = Instantiate(storeItemPrefab, itemContainer);
            StoreItemUI card = cardObj.GetComponent<StoreItemUI>();
            if (card != null)
            {
                card.Setup(itemConfig, OnBuyItem);
                spawnedCards.Add(card);
            }
        }
    }

    /// <summary>
    /// 购买道具
    /// </summary>
    private void OnBuyItem(ItemConfigEntry config)
    {
        if (config == null) return;

        // 检查是否已拥有
        if (ItemManager.Instance != null && ItemManager.Instance.HasItem(config.Type))
        {
            ShowFeedback("你已拥有该道具！");
            return;
        }

        // 检查金粉
        int money = SaveManager.Instance != null ? SaveManager.Instance.GetMoney() : 0;
        if (money < config.Price)
        {
            ShowFeedback("金粉不足！");
            return;
        }

        // 扣款
        if (SaveManager.Instance != null)
            SaveManager.Instance.AddMoney(-config.Price);

        // 添加道具
        if (ItemManager.Instance != null)
            ItemManager.Instance.AddItem(config.Type);

        // 保存
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveItems();
            SaveManager.Instance.SaveGame();
        }

        ShowFeedback($"购买了 {config.DisplayName}！");
        RefreshMoneyDisplay();

        // 刷新所有卡片状态
        foreach (var card in spawnedCards)
        {
            if (card != null)
                card.RefreshAffordability();
        }
    }

    private void OnLeaveClicked()
    {
        GameSceneManager.LoadMap();
    }

    private void RefreshMoneyDisplay()
    {
        if (moneyText != null)
        {
            int money = SaveManager.Instance != null ? SaveManager.Instance.GetMoney() : 0;
            moneyText.text = $"金粉: {money}";
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    private void ClearFeedback()
    {
        if (feedbackText != null)
            feedbackText.text = "";
    }
}
