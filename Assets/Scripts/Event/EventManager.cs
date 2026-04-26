using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EventManager : MonoBehaviour
{
    [Header("Database")]
    public List<GameEventSO> allEvents; // 在Inspector里把做好的事件都拖进去

    [Header("References")]
    public EventDisplay eventDisplay;
    public GameObject eventPanel; // 整个UI面板

    private void Awake()
    {
        EnsureEventDatabase();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureEventDatabase();
    }
#endif

    private void Start()
    {
        // 测试：游戏开始时随机加载一个事件
        // 实际开发中，应该是在玩家进入MapNode时调用 LoadRandomEvent()
        // LoadRandomEvent(); 
    }

    public void EnterEventNode()
    {
        eventPanel.SetActive(true);
        LoadRandomEvent();
    }

    private void LoadRandomEvent()
    {
        if (allEvents == null || allEvents.Count == 0)
        {
            EnsureEventDatabase();
            if (allEvents == null || allEvents.Count == 0)
            {
                Debug.LogWarning("EventManager: 事件库为空，无法加载随机事件。");
                return;
            }
        }
        
        // 随机取一个
        GameEventSO randomEvent = allEvents[Random.Range(0, allEvents.Count)];
        
        // 显示它，并传入“当玩家点击按钮时该怎么办”的回调函数
        eventDisplay.ShowEvent(randomEvent, ExecuteOption);
    }

    // 核心逻辑：处理选项后果
    private void ExecuteOption(EventOption option)
    {
        Debug.Log($"玩家选择了: {option.buttonText}");

        if (option.effects == null || option.effects.Count == 0)
        {
            CloseEvent();
            return;
        }

        // 遍历该选项的所有效果并执行
        foreach (var effect in option.effects)
        {
            ApplyEffect(effect);
        }

        if (option.effects.Exists(e => e.type == EffectType.Leave))
        {
            CloseEvent();
        }
        else
        {
            // 比如获得奖励后，可能需要刷新界面显示“你获得了X”，或者直接关闭
            CloseEvent();
        }
    }

    private void ApplyEffect(EventEffect effect)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.CurrentSaveData == null) return;
        var data = SaveManager.Instance.CurrentSaveData;

        switch (effect.type)
        {
            case EffectType.TakeDamage:
                Debug.Log($"扣血: {effect.intValue}");
                SaveManager.Instance.UpdatePlayerStats(data.currentHP - effect.intValue, SaveManager.Instance.GetMoney());
                break;

            case EffectType.GainGold:
                // GameManager.Instance.AddGold(effect.intValue);
                Debug.Log($"加钱: {effect.intValue}");
                SaveManager.Instance.AddMoney(effect.intValue);
                break;

            case EffectType.LoseGold:
                Debug.Log($"失去金粉: {effect.intValue}");
                SaveManager.Instance.AddMoney(-effect.intValue);
                break;

            case EffectType.GainRedDot:
                Debug.Log($"获得红点: {effect.intValue}");
                break;

            case EffectType.GainBlueDot:
                Debug.Log($"获得蓝点: {effect.intValue}");
                break;

            case EffectType.LoseRedDot:
                Debug.Log($"失去红点: {effect.intValue}");
                break;

            case EffectType.LoseAnyColorDots:
                Debug.Log($"失去任意颜色点: {effect.intValue}");
                break;

            case EffectType.LoseMaxHealth:
                Debug.Log($"失去最大生命值: {effect.intValue}");
                break;

            case EffectType.GainAfterimageDot:
                Debug.Log($"获得重影点: {effect.intValue}");
                break;

            case EffectType.GainGildedDot:
                Debug.Log($"获得鎏金点: {effect.intValue}");
                break;

            case EffectType.GainPrismDot:
                Debug.Log($"获得棱镜点: {effect.intValue}");
                break;

            case EffectType.GainImpastoDot:
                Debug.Log($"获得厚涂点: {effect.intValue}");
                break;

            case EffectType.GainInkDotsToManaDice:
                Debug.Log($"法力骰获得墨点: {effect.intValue}");
                break;

            case EffectType.DoubleOneDiceValue:
                Debug.Log("选择一颗骰子，点数翻倍");
                break;

            case EffectType.CopyDiceFaceToAnotherFace:
                Debug.Log("选择一颗骰子，复制一个面到另一个面");
                break;

            case EffectType.SetOneDiceValueToThree:
                Debug.Log("选择一颗骰子，将目标面设为3点");
                break;

            case EffectType.RandomDoubleOrHalfAllFaces:
                Debug.Log("选择一颗骰子，所有面随机翻倍或减半");
                break;

            case EffectType.DestroyOneDotGainGold:
                Debug.Log($"销毁一个点并获得金粉: {effect.intValue}");
                break;

            case EffectType.RemoveAllSpecialDotsGainGold:
                Debug.Log($"移除所有异质点并按单价获得金粉，单价: {effect.intValue}");
                break;

            case EffectType.TemporaryBattleDamageMultiplier:
                Debug.Log($"获得临时战斗伤害倍率效果，参数: {effect.intValue}");
                break;

            case EffectType.GainItem:
                ItemType itemType = (ItemType)effect.intValue;
                if (itemType != ItemType.None)
                {
                    if (ItemManager.Instance == null)
                    {
                        GameObject obj = new GameObject("ItemManager");
                        obj.AddComponent<ItemManager>();
                    }
                    if (!ItemManager.Instance.HasItem(itemType))
                    {
                        ItemManager.Instance.AddItem(itemType);
                        SaveManager.Instance.SaveItems();
                        var config = ItemCatalog.GetConfig(itemType);
                        string displayName = config != null ? config.DisplayName : itemType.ToString();
                        Debug.Log($"获得道具: {displayName}");
                    }
                    else
                    {
                        Debug.Log($"已拥有道具: {itemType}，跳过");
                    }
                }
                break;
                
            case EffectType.Leave:
                // 只是离开，什么都不做
                break;

            case EffectType.None:
            default:
                break;
        }
    }

    private void EnsureEventDatabase()
    {
        if (allEvents != null && allEvents.Count > 0)
        {
            return;
        }

        GameEventSO[] loaded = Resources.LoadAll<GameEventSO>("Events");
        if (loaded == null || loaded.Length == 0)
        {
            return;
        }

        allEvents = loaded
            .Where(e => e != null)
            .OrderBy(e => e.name)
            .ToList();
    }

    public void CloseEvent()
    {
        if (eventPanel != null)
            eventPanel.SetActive(false);

        // 通知地图管理器，节点完成，可以去下一个点了
        // MapManager.Instance.CompleteNode();
        GameSceneManager.LoadMap();
    }
}