using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个敌人实例 - 多敌人系统的核心数据
/// </summary>
[Serializable]
public class EnemyInstance
{
    public int SlotIndex;          // 槽位索引 (0, 1, 2)
    public string Name;
    public int CurrentHP;
    public int MaxHP;
    public List<RuntimeDice> Hand; // 该敌人的骰子
    public EnemyDamageInfo DamageInfo;

    // 状态效果
    public int BleedingStacks;
    public int BleedingDelayedRemovalTurns;

    public bool IsAlive => CurrentHP > 0;

    /// <summary>
    /// 获取总负面状态层数（朱砂道具用）
    /// </summary>
    public int TotalDebuffStacks => BleedingStacks;

    public EnemyInstance(int slotIndex, string name, int maxHP)
    {
        SlotIndex = slotIndex;
        Name = name;
        MaxHP = maxHP;
        CurrentHP = maxHP;
        Hand = new List<RuntimeDice>();
        BleedingStacks = 0;
        BleedingDelayedRemovalTurns = 0;
    }

    /// <summary>
    /// 附加流血
    /// </summary>
    public void AddBleeding(int stacks, bool delayRemoval = false)
    {
        if (stacks <= 0) return;
        BleedingStacks += stacks;
        if (delayRemoval)
        {
            BleedingDelayedRemovalTurns = Mathf.Max(BleedingDelayedRemovalTurns, 1);
        }
    }

    /// <summary>
    /// 回合开始：流血造成伤害
    /// </summary>
    public int ProcessTurnStartBleeding()
    {
        if (BleedingStacks <= 0) return 0;
        return BleedingStacks;
    }

    /// <summary>
    /// 回合结束：移除5层流血
    /// </summary>
    public void ProcessTurnEndBleeding()
    {
        if (BleedingStacks <= 0) return;
        if (BleedingDelayedRemovalTurns > 0)
        {
            BleedingDelayedRemovalTurns--;
            return;
        }
        BleedingStacks = Mathf.Max(0, BleedingStacks - 5);
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Max(0, CurrentHP);
    }

    /// <summary>
    /// 获取相邻敌人的槽位索引（3槽位: 0-1-2）
    /// </summary>
    public List<int> GetAdjacentSlotIndices()
    {
        var adj = new List<int>();
        if (SlotIndex > 0) adj.Add(SlotIndex - 1);
        if (SlotIndex < 2) adj.Add(SlotIndex + 1);
        return adj;
    }
}
