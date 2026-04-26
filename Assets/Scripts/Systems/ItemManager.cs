using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具管理器 - 管理玩家持有的道具
/// 每种道具只能持有一个（不可叠加）
/// </summary>
public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    private HashSet<ItemType> ownedItems = new HashSet<ItemType>();

    /// <summary>
    /// 当前持有的所有道具（只读）
    /// </summary>
    public IReadOnlyCollection<ItemType> OwnedItems => ownedItems;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("ItemManager");
            go.AddComponent<ItemManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 是否持有指定道具
    /// </summary>
    public bool HasItem(ItemType item)
    {
        return ownedItems.Contains(item);
    }

    /// <summary>
    /// 添加道具（不可叠加，重复添加无效）
    /// </summary>
    public bool AddItem(ItemType item)
    {
        if (item == ItemType.None) return false;
        return ownedItems.Add(item);
    }

    /// <summary>
    /// 移除道具
    /// </summary>
    public bool RemoveItem(ItemType item)
    {
        return ownedItems.Remove(item);
    }

    /// <summary>
    /// 清空所有道具
    /// </summary>
    public void ClearItems()
    {
        ownedItems.Clear();
    }

    /// <summary>
    /// 从存档加载道具列表
    /// </summary>
    public void LoadFromSave(List<int> savedItems)
    {
        ownedItems.Clear();
        if (savedItems == null) return;
        foreach (int itemId in savedItems)
        {
            if (System.Enum.IsDefined(typeof(ItemType), itemId))
            {
                ownedItems.Add((ItemType)itemId);
            }
        }
    }

    /// <summary>
    /// 导出为存档格式
    /// </summary>
    public List<int> ToSaveData()
    {
        var result = new List<int>();
        foreach (var item in ownedItems)
        {
            result.Add((int)item);
        }
        return result;
    }

    /// <summary>
    /// 获取道具显示名称
    /// </summary>
    public static string GetItemDisplayName(ItemType item)
    {
        switch (item)
        {
            case ItemType.DragonBloodRed: return "龙血红";
            case ItemType.RedMaggot: return "红蛆";
            case ItemType.PompeiiRed: return "庞贝红";
            case ItemType.Cinnabar: return "朱砂";
            case ItemType.WineRed: return "酒红";
            default: return item.ToString();
        }
    }

    /// <summary>
    /// 获取道具效果描述
    /// </summary>
    public static string GetItemDescription(ItemType item)
    {
        switch (item)
        {
            case ItemType.DragonBloodRed: return "你造成的额外伤害提升100%";
            case ItemType.RedMaggot: return "对敌人造成额外伤害时，对相邻敌人造成本次伤害10%的额外伤害";
            case ItemType.PompeiiRed: return "牌型中每包含一个红色骰子，给敌人附加10层流血";
            case ItemType.Cinnabar: return "敌人每携带1层负面状态，额外造成本次伤害1%的额外伤害";
            case ItemType.WineRed: return "造成额外伤害后，回复相当于本次额外伤害30%的血量";
            default: return "";
        }
    }
}
