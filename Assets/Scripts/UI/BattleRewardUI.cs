using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗奖励选择UI - 胜利后在Battle场景中显示
/// 显示2-3个道具供玩家选择1个，或跳过获得额外金粉
/// </summary>
public class BattleRewardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject rewardItemPrefab;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipButtonText;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Settings")]
    [Tooltip("展示的奖励道具数量")]
    [SerializeField] private int rewardChoices = 3;

    [Tooltip("跳过奖励时给予的额外金粉")]
    [SerializeField] private int skipBonusGold = 20;

    private List<GameObject> spawnedCards = new List<GameObject>();
    private bool rewardChosen = false;

    /// <summary>
    /// 玩家已选择奖励后触发的事件（BattleManager监听后执行场景跳转）
    /// </summary>
    public System.Action OnRewardCompleted;

    private void Awake()
    {
        if (rewardPanel != null)
            rewardPanel.SetActive(false);
    }

    /// <summary>
    /// 显示奖励面板
    /// </summary>
    public void ShowRewards()
    {
        rewardChosen = false;

        // 清除旧卡片
        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();

        if (rewardPanel != null)
            rewardPanel.SetActive(true);

        if (titleText != null)
            titleText.text = "选择奖励";

        // 获取可选道具
        var items = ItemCatalog.GetRandomUnowned(rewardChoices);

        if (items.Count > 0 && rewardItemPrefab != null && itemContainer != null)
        {
            foreach (var itemConfig in items)
            {
                GameObject cardObj = Instantiate(rewardItemPrefab, itemContainer);
                var card = cardObj.GetComponent<RewardItemUI>();
                if (card != null)
                {
                    card.Setup(itemConfig, OnItemChosen);
                }
                spawnedCards.Add(cardObj);
            }
        }
        else
        {
            // 没有可选道具，直接给金粉
            if (titleText != null)
                titleText.text = "已拥有所有道具！";
        }

        // 跳过按钮
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        if (skipButtonText != null)
            skipButtonText.text = $"跳过（+{skipBonusGold} 金粉）";
    }

    /// <summary>
    /// 隐藏奖励面板
    /// </summary>
    public void Hide()
    {
        if (rewardPanel != null)
            rewardPanel.SetActive(false);
    }

    private void OnItemChosen(ItemConfigEntry config)
    {
        if (rewardChosen) return;
        rewardChosen = true;

        // 给予道具
        if (ItemManager.Instance != null)
            ItemManager.Instance.AddItem(config.Type);

        // 保存
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveItems();

        Hide();
        OnRewardCompleted?.Invoke();
    }

    private void OnSkipClicked()
    {
        if (rewardChosen) return;
        rewardChosen = true;

        // 给予额外金粉
        if (SaveManager.Instance != null)
            SaveManager.Instance.AddMoney(skipBonusGold);

        Hide();
        OnRewardCompleted?.Invoke();
    }
}
