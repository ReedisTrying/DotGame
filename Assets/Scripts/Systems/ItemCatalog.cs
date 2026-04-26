using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 道具稀有度
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare
}

/// <summary>
/// 单个道具的静态配置信息
/// </summary>
[System.Serializable]
public class ItemConfigEntry
{
    public ItemType Type;
    public string DisplayName;
    public string Description;
    public int Price;
    public ItemRarity Rarity;
    public int DropWeight; // 掉落/出现权重，越大越常见

    public ItemConfigEntry(ItemType type, string name, string desc, int price, ItemRarity rarity, int weight)
    {
        Type = type;
        DisplayName = name;
        Description = desc;
        Price = price;
        Rarity = rarity;
        DropWeight = weight;
    }
}

/// <summary>
/// 道具目录 - 所有道具的静态配置数据
/// 商店、战斗掉落、事件奖励统一从此处读取
/// 后续可替换为JSON加载
/// </summary>
public static class ItemCatalog
{
    private static readonly List<ItemConfigEntry> allItems = new List<ItemConfigEntry>
    {
        new ItemConfigEntry(
            ItemType.DragonBloodRed, "龙血红",
            "红色额外伤害+100%",
            50, ItemRarity.Common, 10
        ),
        new ItemConfigEntry(
            ItemType.Cinnabar, "朱砂",
            "敌人每层负面状态，额外伤害+1%",
            60, ItemRarity.Common, 8
        ),
        new ItemConfigEntry(
            ItemType.WineRed, "酒红",
            "回复额外伤害30%的血量",
            70, ItemRarity.Uncommon, 6
        ),
        new ItemConfigEntry(
            ItemType.RedMaggot, "红蛆",
            "额外伤害10%溅射相邻敌人",
            80, ItemRarity.Uncommon, 7
        ),
        new ItemConfigEntry(
            ItemType.PompeiiRed, "庞贝红",
            "牌型中每个红色骰子附加10层流血",
            100, ItemRarity.Rare, 5
        ),
    };

    /// <summary>
    /// 获取所有道具配置
    /// </summary>
    public static IReadOnlyList<ItemConfigEntry> AllItems => allItems;

    /// <summary>
    /// 按类型查找道具配置
    /// </summary>
    public static ItemConfigEntry GetConfig(ItemType type)
    {
        return allItems.Find(e => e.Type == type);
    }

    /// <summary>
    /// 获取玩家尚未拥有的道具列表
    /// </summary>
    public static List<ItemConfigEntry> GetUnownedItems()
    {
        var result = new List<ItemConfigEntry>();
        foreach (var entry in allItems)
        {
            if (ItemManager.Instance == null || !ItemManager.Instance.HasItem(entry.Type))
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// 从未拥有道具中按权重随机选取N个
    /// </summary>
    public static List<ItemConfigEntry> GetRandomUnowned(int count)
    {
        var pool = GetUnownedItems();
        if (pool.Count <= count) return new List<ItemConfigEntry>(pool);

        // 加权随机
        var result = new List<ItemConfigEntry>();
        var available = new List<ItemConfigEntry>(pool);

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int totalWeight = 0;
            foreach (var item in available)
                totalWeight += item.DropWeight;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;
            ItemConfigEntry picked = available[0];

            foreach (var item in available)
            {
                cumulative += item.DropWeight;
                if (roll < cumulative)
                {
                    picked = item;
                    break;
                }
            }

            result.Add(picked);
            available.Remove(picked);
        }

        return result;
    }

    /// <summary>
    /// 获取稀有度颜色（用于UI显示）
    /// </summary>
    public static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return Color.white;
            case ItemRarity.Uncommon: return new Color(0.3f, 0.8f, 0.3f); // 绿色
            case ItemRarity.Rare: return new Color(0.6f, 0.4f, 1f); // 紫色
            default: return Color.white;
        }
    }
}
