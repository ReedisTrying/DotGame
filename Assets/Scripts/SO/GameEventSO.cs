using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEvent", menuName = "Event/Event Data")]
public class GameEventSO : ScriptableObject
{
    [Header("Basic Info")]
    public string title;
    [TextArea(5, 10)] 
    public string description;

    [Header("Options")]
    public List<EventOption> options = new(3);
}

[System.Serializable]
public class EventOption
{
    public string buttonText; // 按钮上写的字，如 "接受力量"
    public string hoverText;  // 鼠标悬停提示，如 "获得一张诅咒牌，生命值-5"
    
    // 这里定义后果。支持多种效果组合
    [Header("Consequences")]
    public List<EventEffect> effects;
}

[System.Serializable]
public struct EventEffect
{
    public EffectType type;
    public int intValue; // 数值参数（例如 +2 点、-10 生命、持续3回合）
    // public ItemSO item; // 如果你需要给物品，可以在这加引用
}

public enum EffectType
{
    None,
    TakeDamage,     // 扣血
    GainGold,       // 获得金币
    LoseGold,       // 失去金币
    ForgeDice,    // 升级骰子
    AddCurse,       // 获得诅咒
    Leave,          // 离开事件

    // --- 资源变化 ---
    GainRedDot,             // 获得红点
    GainBlueDot,            // 获得蓝点
    LoseRedDot,             // 失去红点
    LoseAnyColorDots,       // 失去任意颜色点（数量= intValue）
    LoseMaxHealth,          // 失去最大生命值

    // --- 特殊点相关 ---
    GainAfterimageDot,      // 获得重影点
    GainGildedDot,          // 获得鎏金点
    GainPrismDot,           // 获得棱镜点
    GainImpastoDot,         // 获得厚涂点
    GainInkDotsToManaDice,  // 法力骰获得墨点（数量= intValue）

    // --- 骰子改造 ---
    DoubleOneDiceValue,         // 选择一颗骰子，点数翻倍
    CopyDiceFaceToAnotherFace,  // 复制一个面到另一个面
    SetOneDiceValueToThree,     // 选择一颗骰子，将目标面设为3点
    RandomDoubleOrHalfAllFaces, // 选择一颗骰子，所有面随机翻倍或减半

    // --- 清理 / 置换 ---
    DestroyOneDotGainGold,          // 销毁一个点并获得金币
    RemoveAllSpecialDotsGainGold,   // 移除异质点，每个换金币（单价= intValue）

    // --- 战斗临时效果 ---
    TemporaryBattleDamageMultiplier, // 下一次战斗前N回合伤害倍率（倍率和回合数由配置约定）

    // --- 道具 ---
    GainItem // 获得道具（intValue = ItemType枚举值）
}