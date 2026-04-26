using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    // Map State
    public int mapSeed;
    public int currentFloor;
    public int currentNodeIndex;
    public List<Vector2Int> visitedNodes = new List<Vector2Int>(); // x: Floor, y: Index

    // Player Stats
    public int currentHP;
    public int maxHP;
    public int money;

    // Inventory
    public List<RuntimeDice> playerDice = new List<RuntimeDice>();

    // Items (stored as int IDs of ItemType enum)
    public List<int> ownedItems = new List<int>();
}
