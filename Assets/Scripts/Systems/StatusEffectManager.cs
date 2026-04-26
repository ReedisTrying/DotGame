using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态效果管理器 - 管理战斗中的持续状态效果（流血等）
/// 仅当前战斗有效，战斗结束清除
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    public static StatusEffectManager Instance { get; private set; }

    /// <summary>
    /// 流血数据：包含层数和是否推迟移除
    /// </summary>
    private struct BleedingData
    {
        public int Stacks;
        public int DelayedRemovalTurns; // 推迟移除的剩余回合数

        public BleedingData(int stacks, int delayedTurns = 0)
        {
            Stacks = stacks;
            DelayedRemovalTurns = delayedTurns;
        }
    }

    // 当前敌人的流血层数（单敌人模式）
    private BleedingData enemyBleeding;

    /// <summary>
    /// 敌人当前流血层数
    /// </summary>
    public int EnemyBleedingStacks => enemyBleeding.Stacks;

    /// <summary>
    /// 敌人当前总负面状态层数（用于朱砂道具计算）
    /// </summary>
    public int EnemyTotalDebuffStacks => enemyBleeding.Stacks;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 战斗开始时重置所有状态
    /// </summary>
    public void ResetAll()
    {
        enemyBleeding = new BleedingData(0, 0);
    }

    /// <summary>
    /// 给敌人附加流血层数
    /// </summary>
    /// <param name="stacks">流血层数</param>
    /// <param name="delayRemoval">是否推迟1回合移除（地狱火效果）</param>
    public void AddEnemyBleeding(int stacks, bool delayRemoval = false)
    {
        if (stacks <= 0) return;
        enemyBleeding.Stacks += stacks;
        if (delayRemoval)
        {
            // 本次附加的流血推迟1回合才开始被移除
            enemyBleeding.DelayedRemovalTurns = Mathf.Max(enemyBleeding.DelayedRemovalTurns, 1);
        }
    }

    /// <summary>
    /// 回合开始：流血造成伤害
    /// 每存在1层流血，敌人减少1点生命值
    /// </summary>
    /// <returns>本次流血造成的伤害</returns>
    public int ProcessTurnStartBleeding()
    {
        if (enemyBleeding.Stacks <= 0) return 0;
        int bleedDamage = enemyBleeding.Stacks;
        return bleedDamage;
    }

    /// <summary>
    /// 回合结束：移除流血层数
    /// 每回合结束移除5层流血
    /// </summary>
    public void ProcessTurnEndBleeding()
    {
        if (enemyBleeding.Stacks <= 0) return;

        if (enemyBleeding.DelayedRemovalTurns > 0)
        {
            // 推迟移除：本回合不移除，计数减1
            enemyBleeding.DelayedRemovalTurns--;
            return;
        }

        enemyBleeding.Stacks = Mathf.Max(0, enemyBleeding.Stacks - 5);
    }
}
