using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class                                                                                                                                SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField]
    private string saveFileName = "savegame.json";

    public SaveData CurrentSaveData { get; private set; }

    private string SaveFilePath => Path.Combine(Directory.GetCurrentDirectory(), saveFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("SaveManager");
            go.AddComponent<SaveManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void NewGame(RunConfigSO runConfig)
    {
        CurrentSaveData = new SaveData();
        
        // Load Game Config from JSON
        GameConfig gameConfig = null;
        var jsonAsset = Resources.Load<TextAsset>("M0_Data_Config");
        if (jsonAsset != null)
        {
            gameConfig = JsonUtility.FromJson<GameConfig>(jsonAsset.text);
        }
        else
        {
            Debug.LogError("SaveManager: Failed to load M0_Data_Config from Resources.");
        }

        // Initialize Map
        CurrentSaveData.mapSeed = Random.Range(int.MinValue, int.MaxValue);
        CurrentSaveData.currentFloor = 0;
        CurrentSaveData.currentNodeIndex = -1;
        CurrentSaveData.visitedNodes = new List<Vector2Int>();

        // Initialize Player Stats
        if (gameConfig != null && gameConfig.game_rules != null)
        {
            CurrentSaveData.maxHP = gameConfig.game_rules.player_max_hp;
            CurrentSaveData.currentHP = gameConfig.game_rules.player_max_hp;
        }
        else
        {
            CurrentSaveData.maxHP = 100; // Default
            CurrentSaveData.currentHP = 100;
        }
        CurrentSaveData.money = 0; // Starting money

        // Initialize Dice from Config
        if (gameConfig != null && gameConfig.initial_dice_sets != null)
        {
            // Use runConfig to select the dice set if available, otherwise use default
            if (runConfig != null)
            {
                gameConfig.initial_dice_sets.SetCurrentSet(runConfig.selectedDiceSetId);
            }

            var diceSet = gameConfig.initial_dice_sets.GetCurrentSet();
            if (diceSet != null && diceSet.dice != null)
            {
                foreach (var diceConfig in diceSet.dice)
                {
                    CurrentSaveData.playerDice.Add(RuntimeDice.FromConfig(diceConfig));
                }
            }
        }

        SaveGame();
    }

    public void SaveGame()
    {
        if (CurrentSaveData == null) return;

        string json = JsonUtility.ToJson(CurrentSaveData, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log($"Game Saved to {SaveFilePath}");
    }

    public bool LoadGame()
    {
        if (File.Exists(SaveFilePath))
        {
            try
            {
                string json = File.ReadAllText(SaveFilePath);
                CurrentSaveData = JsonUtility.FromJson<SaveData>(json);
                Debug.Log("Game Loaded successfully.");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load save file: {e.Message}");
                CurrentSaveData = null;
                return false;
            }
        }
        else
        {
            Debug.Log("No save file found.");
            CurrentSaveData = null;
            return false;
        }
    }

    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }
    
    public void DeleteSaveFile()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            CurrentSaveData = null;
            Debug.Log("Save file deleted.");
        }
    }

    // Helper to update player stats
    public void UpdatePlayerStats(int hp, int money)
    {
        if (CurrentSaveData == null) return;
        CurrentSaveData.currentHP = hp;
        CurrentSaveData.money = money;
        SaveGame();
    }

    public int GetMoney()
    {
        return CurrentSaveData != null ? CurrentSaveData.money : 0;
    }

    public void AddMoney(int amount)
    {
        if (CurrentSaveData == null || amount == 0) return;
        CurrentSaveData.money = Mathf.Max(0, CurrentSaveData.money + amount);
        SaveGame();
    }

    // Helper to update dice
    public void UpdatePlayerDice(List<RuntimeDice> newDiceList)
    {
        if (CurrentSaveData == null) return;
        CurrentSaveData.playerDice = new List<RuntimeDice>(newDiceList);
        SaveGame();
    }

    // Helper to update map position
    public void UpdateMapPosition(int floor, int nodeIndex)
    {
        if (CurrentSaveData == null) return;
        CurrentSaveData.currentFloor = floor;
        CurrentSaveData.currentNodeIndex = nodeIndex;
        // Add to visited if needed, but usually tracked separately
        if (!CurrentSaveData.visitedNodes.Contains(new Vector2Int(floor, nodeIndex)))
        {
            CurrentSaveData.visitedNodes.Add(new Vector2Int(floor, nodeIndex));
        }
        SaveGame();
    }

    /// <summary>
    /// 保存道具到存档并同步ItemManager
    /// </summary>
    public void SaveItems()
    {
        if (CurrentSaveData == null || ItemManager.Instance == null) return;
        CurrentSaveData.ownedItems = ItemManager.Instance.ToSaveData();
        SaveGame();
    }

    /// <summary>
    /// 从存档加载道具到ItemManager
    /// </summary>
    public void LoadItems()
    {
        if (CurrentSaveData == null || ItemManager.Instance == null) return;
        ItemManager.Instance.LoadFromSave(CurrentSaveData.ownedItems);
    }
}
