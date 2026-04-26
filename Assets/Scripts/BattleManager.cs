using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 敌人伤害详情结构体
/// </summary>
[System.Serializable]
public struct EnemyDamageInfo
{
    public float TotalDamage;        // 总伤害
    public float BaseDamage;         // 基础伤害
    public float FinalMultiplier;    // 最终倍率
    
    public EnemyDamageInfo(float totalDamage, float baseDamage, float finalMultiplier)
    {
        TotalDamage = totalDamage;
        BaseDamage = baseDamage;
        FinalMultiplier = finalMultiplier;
    }
}

/// <summary>
/// 战斗管理器 - 管理游戏循环的状态机
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

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

    #region Game State Enum
    public enum GameState
    {
        Setup,
        EnemyTurn,
        PlayerTurn,
        TargetSelection,
        Resolution,
        EnemyAttack,
        GameOver
    }
    #endregion

    #region Serialized Fields
    [Header("Configuration")]
    [SerializeField]
    [Tooltip("JSON配置文件管理器（可选，如不设置会自动查找）")]
    private ConfigManager configManager;
    
    [SerializeField]
    [Tooltip("运行时配置容器，拖入RunConfig资产")]
    private RunConfigSO runConfig;

    [Header("Resource Pool (for Action Point Dots)")]

    [Header("UI Manager")]
    [SerializeField]
    private GameUIManager uiManager;

    [Header("Battle Reward")]
    [SerializeField]
    [Tooltip("战斗奖励UI（胜利后显示道具选择）")]
    private BattleRewardUI battleRewardUI;
    #endregion

    #region Private Fields
    private GameState currentState;
    public GameState CurrentState => currentState;
    private GameConfig config;
    private HandEvaluator handEvaluator;

    private int playerHP;
    private int playerMaxHP;
    private int currentTurn;

    // 多敌人系统
    private List<EnemyInstance> enemies = new List<EnemyInstance>();
    private int selectedEnemyIndex = -1;
    public int SelectedEnemyIndex => selectedEnemyIndex;
    private const int MaxEnemySlots = 3;

    // 向后兼容：单敌人accessors（用于UI等外部引用）
    private int enemyHP => enemies.Count > 0 ? enemies[0].CurrentHP : 0;
    private int enemyMaxHP => enemies.Count > 0 ? enemies[0].MaxHP : 0;

    private EvaluationResult currentEvaluation;
    private EnemyDamageInfo currentEnemyDamage;
    private List<RuntimeDice> playerHand = new List<RuntimeDice>();    // 当前手牌（场上显示的骰子）
    private List<RuntimeDice> drawPile = new List<RuntimeDice>();      // 待抽堆
    private List<RuntimeDice> discardPile = new List<RuntimeDice>();   // 弃牌堆
    private List<RuntimeDice> enemyHand = new List<RuntimeDice>();     // 当前目标敌人的骰子（兼容用）
    private const int EnemyDiceCount = 3;
    private bool playerTurnEnded = false;
    private bool playRequested = false;      // 玩家点击了"出牌"按钮
    private bool targetSelected = false;     // 玩家选中了目标敌人
    private bool hasHandledGameOverTransition = false;
    
    // 牌型永久加成字典（绿色纯色机制）
    private Dictionary<string, float> permanentHandBonuses = new Dictionary<string, float>();
    
    // 玩家闪避概率（紫色骰子效果，当回合生效）
    private float currentDodgeChance = 0f;
    
    // 敌人伤害倍率（难度系数）

    // 敌人伤害倍率（难度系数）
    private float enemyDamageMultiplier = 3.0f;

    /// <summary>
    /// 处理骰子选中事件
    /// </summary>
    public void OnDiceSelected(Dice dice)
    {
        if (dice == null || dice.RuntimeData == null) return;
        
        // 重新评估并更新UI
        EvaluateHand();
    }

    /// <summary>
    /// 处理骰子取消选中事件
    /// </summary>
    public void OnDiceDeselected(Dice dice)
    {
        if (dice == null || dice.RuntimeData == null) return;
        
        // 重新评估并更新UI
        EvaluateHand();
    }
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeGame();
    }

    private void OnEnable()
    {
        if (uiManager != null)
        {
            uiManager.OnEndTurnClicked += OnEndTurnClicked;
            uiManager.OnPlayClicked += OnPlayClicked;
            uiManager.OnRestartClicked += RestartGame;
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnDiceStateChanged.AddListener(OnDiceChanged);
        }

        if (DiceManager.Instance != null)
        {
            DiceManager.Instance.OnEnemyDiceArranged += OnEnemyDiceArranged;
        }
    }

    private void OnDisable()
    {
        if (uiManager != null)
        {
            uiManager.OnEndTurnClicked -= OnEndTurnClicked;
            uiManager.OnPlayClicked -= OnPlayClicked;
            uiManager.OnRestartClicked -= RestartGame;
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnDiceStateChanged.RemoveListener(OnDiceChanged);
        }

        if (DiceManager.Instance != null)
        {
            DiceManager.Instance.OnEnemyDiceArranged -= OnEnemyDiceArranged;
        }
    }
    #endregion

    #region Initialization
    private void InitializeGame()
    {
        // 1. 加载JSON配置文件（静态数据）
        if (configManager == null)
        {
            configManager = FindFirstObjectByType<ConfigManager>();
            if (configManager == null)
            {
                GameObject configObj = new GameObject("ConfigManager");
                configManager = configObj.AddComponent<ConfigManager>();
            }
        }

        if (configManager.Config == null)
        {
            Debug.LogError("[BattleManager] ConfigManager.Config为null！JSON配置文件加载失败。");
            return;
        }

        config = configManager.Config;
        
        // 2. 应用RunConfig中的玩家选择（骰子配置组和难度）
        if (runConfig != null)
        {
            // 应用骰子配置组选择
            if (config.initial_dice_sets != null)
            {
                config.initial_dice_sets.SetCurrentSet(runConfig.selectedDiceSetId);
            }
            
            // 应用难度选择
            if (config.difficulty_settings != null)
            {
                config.difficulty_settings.current_difficulty = runConfig.selectedDifficultyId;
            }
        }
        else
        {
            Debug.LogWarning("[BattleManager] RunConfig未设置，使用JSON配置文件中的默认值。");
        }

        // 获取UI Manager
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<GameUIManager>();
        }

        // 重新订阅事件（如果Start在OnEnable之后执行且uiManager刚被找到）
        if (uiManager != null)
        {
            // 先移除以防重复订阅
            uiManager.OnEndTurnClicked -= OnEndTurnClicked;
            uiManager.OnPlayClicked -= OnPlayClicked;
            uiManager.OnRestartClicked -= RestartGame;
            
            uiManager.OnEndTurnClicked += OnEndTurnClicked;
            uiManager.OnPlayClicked += OnPlayClicked;
            uiManager.OnRestartClicked += RestartGame;
            
            uiManager.HideGameOver();
        }

        // 确保订阅InteractionManager
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnDiceStateChanged.RemoveListener(OnDiceChanged);
            InteractionManager.Instance.OnDiceStateChanged.AddListener(OnDiceChanged);
        }

        // 创建 HandEvaluator，传入永久加成字典
        handEvaluator = new HandEvaluator(config.score_multipliers, permanentHandBonuses);
        
        // 读取难度系数
        if (config.difficulty_settings != null)
        {
            enemyDamageMultiplier = config.difficulty_settings.GetCurrentEnemyDamageMultiplier();
            // Debug.Log($"Difficulty: {config.difficulty_settings.current_difficulty}, Enemy Damage Multiplier: {enemyDamageMultiplier}");
        }

        // 开始游戏
        StartCoroutine(GameLoop());
    }
    #endregion

    #region Game Loop
    private IEnumerator GameLoop()
    {
        // Setup阶段
        yield return StartCoroutine(SetupPhase());

        // 主游戏循环
        while (currentState != GameState.GameOver)
        {
            switch (currentState)
            {
                case GameState.EnemyTurn:
                    yield return StartCoroutine(EnemyTurnPhase());
                    break;

                case GameState.PlayerTurn:
                    yield return StartCoroutine(PlayerTurnPhase());
                    break;

                case GameState.Resolution:
                    yield return StartCoroutine(ResolutionPhase());
                    break;

                case GameState.EnemyAttack:
                    yield return StartCoroutine(EnemyAttackPhase());
                    break;
            }
        }

        // GameOver阶段
        yield return StartCoroutine(GameOverPhase());
    }

    private IEnumerator SetupPhase()
    {
        SetState(GameState.Setup);
        hasHandledGameOverTransition = false;

        // 尝试从存档加载
        bool loadedFromSave = false;
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null)
        {
            var saveData = SaveManager.Instance.CurrentSaveData;
            // 只有当存档中有有效HP时才加载（避免初始化时的满血被覆盖，或者新游戏时使用默认值）
            // 注意：SaveManager.NewGame会初始化HP为MaxHP
            if (saveData.currentHP > 0)
            {
                playerMaxHP = saveData.maxHP;
                playerHP = saveData.currentHP;
                
                playerHand.Clear();
                drawPile.Clear();
                discardPile.Clear();
                
                if (saveData.playerDice != null)
                {
                    int handSize = config != null ? config.game_rules.hand_size : 6;
                    int totalDice = config?.game_rules?.total_dice_count ?? handSize;

                    // 如果存档骰子不足total_dice_count，从配置补充
                    List<RuntimeDice> allSavedDice = new List<RuntimeDice>(saveData.playerDice);
                    if (allSavedDice.Count < totalDice && config?.initial_dice_sets != null)
                    {
                        var currentSet = config.initial_dice_sets.GetCurrentSet();
                        if (currentSet?.dice != null)
                        {
                            int needed = totalDice - allSavedDice.Count;
                            for (int j = 0; j < needed; j++)
                            {
                                var diceConfig = currentSet.dice[(allSavedDice.Count + j) % currentSet.dice.Count];
                                RuntimeDice extra = RuntimeDice.FromConfig(diceConfig);
                                if (extra != null)
                                    allSavedDice.Add(extra);
                            }
                        }
                    }

                    // 将骰子分配到手牌和待抽堆
                    for (int i = 0; i < allSavedDice.Count; i++)
                    {
                        var d = allSavedDice[i];
                        d.ActiveFaceIndex = Random.Range(0, d.Faces.Count);
                        if (i < handSize)
                        {
                            playerHand.Add(d);
                        }
                        else
                        {
                            drawPile.Add(d);
                        }
                    }
                }
                loadedFromSave = true;
            }
        }

        if (!loadedFromSave)
        {
            // 初始化HP
            playerMaxHP = config.game_rules.player_max_hp;
            playerHP = playerMaxHP;
            
            // 生成初始骰子（数量由配置决定）
            SpawnRandomDiceForPlayer(config.game_rules.hand_size);
        }

        // 初始化多敌人
        enemies.Clear();
        SpawnEnemies();
        selectedEnemyIndex = 0;

        currentTurn = 1;

        // 初始化状态效果管理器
        if (StatusEffectManager.Instance == null)
        {
            GameObject statusObj = new GameObject("StatusEffectManager");
            statusObj.AddComponent<StatusEffectManager>();
        }
        StatusEffectManager.Instance.ResetAll();

        // 从存档加载道具
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadItems();
        }

        // 确保DiceManager生成3D骰子
        EnsureDiceManagerAndSpawnHand();


        // 生成行动点(Dot)
        if (config != null && config.game_rules != null)
        {
            SpawnActionPointDots(config.game_rules.base_action_points);
        }

        // 更新UI
        UpdateAllUI();

        yield return new WaitForSeconds(0.5f);

        // 进入敌人回合
        SetState(GameState.EnemyTurn);
    }

    /// <summary>
    /// 生成敌人实例（多敌人）
    /// </summary>
    private void SpawnEnemies()
    {
        // 从JSON配置加载敌人模板
        var template = config.enemy_template_m0;
        
        // 如果配置中有enemy_templates数组，优先使用；否则用单个模板生成多个
        // 目前用单模板生成3个敌人（后续可扩展为从配置读取）
        int enemyCount = MaxEnemySlots; // 默认3个敌人
        
        for (int i = 0; i < enemyCount; i++)
        {
            string name = $"{template.name}_{i + 1}";
            var enemy = new EnemyInstance(i, name, template.hp);
            enemies.Add(enemy);
        }
    }

    /// <summary>
    /// 检查是否所有敌人已死亡
    /// </summary>
    private bool AreAllEnemiesDead()
    {
        return enemies.All(e => !e.IsAlive);
    }

    /// <summary>
    /// 获取第一个存活敌人的索引
    /// </summary>
    private int GetFirstAliveEnemyIndex()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].IsAlive) return i;
        }
        return -1;
    }

    /// <summary>
    /// 为所有存活敌人投掷骰子
    /// </summary>
    private void RollAllEnemyDice()
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive) continue;
            
            enemy.Hand.Clear();
            
            if (config?.enemy_template_m0?.dice != null && config.enemy_template_m0.dice.Count > 0)
            {
                var enemyDiceConfigs = config.enemy_template_m0.dice;
                for (int i = 0; i < EnemyDiceCount; i++)
                {
                    var diceConfig = enemyDiceConfigs[i % enemyDiceConfigs.Count];
                    RuntimeDice dice = RuntimeDice.FromConfig(diceConfig);
                    if (dice != null)
                    {
                        dice.ActiveFaceIndex = Random.Range(0, dice.Faces.Count);
                        enemy.Hand.Add(dice);
                    }
                }
            }
            
            while (enemy.Hand.Count < EnemyDiceCount)
            {
                RuntimeDice fallbackDice = CreateFallbackDice();
                if (fallbackDice != null && fallbackDice.Faces != null && fallbackDice.Faces.Count > 0)
                {
                    fallbackDice.ActiveFaceIndex = Random.Range(0, fallbackDice.Faces.Count);
                    enemy.Hand.Add(fallbackDice);
                }
                else break;
            }
        }

        // 兼容：同步第一个存活敌人的骰子到enemyHand和3D显示
        int firstAlive = GetFirstAliveEnemyIndex();
        if (firstAlive >= 0)
        {
            enemyHand = new List<RuntimeDice>(enemies[firstAlive].Hand);
            DiceManager diceManager = DiceManager.Instance;
            if (diceManager != null && enemyHand.Count > 0)
            {
                diceManager.ThrowEnemyDiceBatch(enemyHand);
            }
        }
    }

    /// <summary>
    /// 处理打出的骰子（选中的骰子进弃牌堆，补牌）
    /// </summary>
    private void ProcessPlayedDice()
    {
        // 黑色骰子破碎
        ShatterBlackDice();

        // 收集选中的骰子索引
        HashSet<int> consumedIndices = new HashSet<int>();
        for (int i = 0; i < playerHand.Count; i++)
        {
            if (playerHand[i] != null && playerHand[i].isSelected)
            {
                consumedIndices.Add(i);
            }
        }

        DiceManager diceManager = DiceManager.Instance;
        if (diceManager != null)
        {
            var activeDice = diceManager.GetAllActiveDice();
            int checkCount = Mathf.Min(activeDice.Count, playerHand.Count);
            for (int i = 0; i < checkCount; i++)
            {
                var sceneDice = activeDice[i];
                if (sceneDice == null) continue;
                if (sceneDice.IsSelected || (sceneDice.RuntimeData != null && sceneDice.RuntimeData.isSelected))
                {
                    consumedIndices.Add(i);
                }
            }
        }

        // 将使用过的骰子移入弃牌堆
        var sortedConsumed = consumedIndices.OrderByDescending(i => i).ToList();
        foreach (int index in sortedConsumed)
        {
            if (index >= 0 && index < playerHand.Count && playerHand[index] != null)
            {
                var usedDice = playerHand[index];
                usedDice.isSelected = false;
                foreach (var face in usedDice.Faces)
                {
                    face.Reset();
                }
                discardPile.Add(usedDice);
                playerHand.RemoveAt(index);
            }
        }

        // 重置剩余手牌选中状态
        foreach (var runtimeDice in playerHand)
        {
            if (runtimeDice != null)
                runtimeDice.isSelected = false;
        }

        // 从待抽堆抽取骰子补满手牌
        int targetHandSize = config != null ? config.game_rules.hand_size : 6;
        while (playerHand.Count < targetHandSize)
        {
            RuntimeDice drawn = DrawFromPile();
            if (drawn == null) break;
            drawn.ActiveFaceIndex = Random.Range(0, drawn.Faces.Count);
            playerHand.Add(drawn);
        }

        // 更新场景骰子
        if (diceManager != null)
        {
            diceManager.SpawnRuntimeDiceHand(new List<RuntimeDice>(playerHand));
        }
        else
        {
            EnsureDiceManagerAndSpawnHand();
        }
    }

    /// <summary>
    /// 计算单个敌人实例的伤害
    /// </summary>
    private EnemyDamageInfo CalculateEnemyDamageForInstance(EnemyInstance enemy)
    {
        if (enemy == null || enemy.Hand == null || enemy.Hand.Count == 0)
        {
            return new EnemyDamageInfo(0, 0, 1f);
        }

        List<DiceFace> activeFaces = enemy.Hand
            .Where(d => d.ActiveFace != null)
            .Select(d => d.ActiveFace)
            .ToList();

        if (activeFaces.Count == 0)
        {
            return new EnemyDamageInfo(0, 0, 1f);
        }

        List<int> values = activeFaces.Select(f => f.value).ToList();
        var (handName, baseMultiplier, handIndices) = IdentifyEnemyBestHand(values);

        float baseDamage = 0f;
        foreach (int index in handIndices)
        {
            baseDamage += values[index];
        }

        float finalMultiplier = Mathf.Max(1f, baseMultiplier * enemyDamageMultiplier);
        float totalDamage = Mathf.Ceil(baseDamage * finalMultiplier);

        return new EnemyDamageInfo(totalDamage, baseDamage, finalMultiplier);
    }

    private IEnumerator EnemyTurnPhase()
    {
        SetState(GameState.EnemyTurn);

        // 流血效果：回合开始时，每层流血减少1点敌人生命值（所有敌人）
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive) continue;
            int bleedDamage = enemy.ProcessTurnStartBleeding();
            if (bleedDamage > 0)
            {
                enemy.TakeDamage(bleedDamage);
                if (uiManager != null)
                    uiManager.ShowFeedback($"{enemy.Name} 流血！受到 {bleedDamage} 点伤害（{enemy.BleedingStacks}层）");
                UpdateAllUI();

                yield return new WaitForSeconds(0.3f);
            }
        }

        // 检查是否所有敌人死亡
        if (AreAllEnemiesDead())
        {
            SetState(GameState.GameOver);
            yield break;
        }

        // 为所有存活敌人投掷骰子
        RollAllEnemyDice();

        yield return new WaitForSeconds(0.5f);

        // 进入玩家回合
        SetState(GameState.PlayerTurn);
    }

    private IEnumerator PlayerTurnPhase()
    {
        SetState(GameState.PlayerTurn);
        playerTurnEnded = false;
        playRequested = false;

        // 启用按钮
        if (uiManager != null)
        {
            uiManager.SetEndTurnButtonInteractable(true);
            uiManager.ShowFeedback("你的回合！选择骰子后点击「出牌」，或直接「结束回合」");
        }

        // 默认选中第一个存活的敌人
        selectedEnemyIndex = GetFirstAliveEnemyIndex();

        // 初始评估手牌
        EvaluateHand();
        UpdateAllUI();

        // 玩家回合循环：可多次出牌
        while (!playerTurnEnded)
        {
            if (playRequested)
            {
                playRequested = false;

                // 检查是否有选中的骰子
                var selectedDice = playerHand.Where(d => d.isSelected).ToList();
                if (selectedDice.Count == 0)
                {
                    if (uiManager != null)
                        uiManager.ShowFeedback("请先选择要打出的骰子！");
                    continue;
                }

                // 进入目标选择
                if (uiManager != null)
                    uiManager.ShowFeedback("点击选择攻击目标！");

                targetSelected = false;
                SetState(GameState.TargetSelection);

                // 等待目标选择
                while (!targetSelected && !playerTurnEnded)
                {
                    yield return null;
                }

                if (playerTurnEnded) break;

                // 执行单次出牌结算
                SetState(GameState.Resolution);
                yield return StartCoroutine(ResolveSinglePlay(selectedEnemyIndex));

                // 检查是否所有敌人死亡
                if (AreAllEnemiesDead())
                {
                    SetState(GameState.GameOver);
                    yield break;
                }

                // 回到玩家回合继续
                SetState(GameState.PlayerTurn);
                
                // 检查是否还有手牌
                if (playerHand.Count == 0)
                {
                    if (uiManager != null)
                        uiManager.ShowFeedback("手牌已用完！");
                    playerTurnEnded = true;
                    break;
                }

                EvaluateHand();
                UpdateAllUI();

                if (uiManager != null)
                    uiManager.ShowFeedback("继续选择骰子出牌，或结束回合");
            }

            yield return null;
        }

        // 玩家结束回合 → 进入敌人攻击阶段
        SetState(GameState.EnemyAttack);
    }

    /// <summary>
    /// 单次出牌结算 - 对指定敌人造成伤害
    /// </summary>
    private IEnumerator ResolveSinglePlay(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= enemies.Count || !enemies[targetIndex].IsAlive)
        {
            yield break;
        }

        var targetEnemy = enemies[targetIndex];

        // 禁用按钮
        if (uiManager != null)
            uiManager.SetEndTurnButtonInteractable(false);

        // 清理所有Dot
        ClearAllDots();

        // 计算伤害
        EvaluateHand();
        float damageDealt = currentEvaluation.TotalDamage;
        float shieldValue = currentEvaluation.ShieldValue;

        // === 道具系统：额外伤害链式处理 ===
        float originalRedBonus = currentEvaluation.RedBonusDamage;
        float modifiedRedBonus = originalRedBonus;

        if (modifiedRedBonus > 0 && ItemManager.Instance != null)
        {
            // 1. 龙血红：额外伤害+100%
            if (ItemManager.Instance.HasItem(ItemType.DragonBloodRed))
            {
                modifiedRedBonus *= 2f;
                if (uiManager != null)
                    uiManager.ShowFeedback($"龙血红：额外伤害翻倍！{originalRedBonus:F0}→{modifiedRedBonus:F0}");
            }

            // 2. 朱砂：敌人每层负面状态+1%额外伤害
            if (ItemManager.Instance.HasItem(ItemType.Cinnabar))
            {
                int debuffStacks = targetEnemy.TotalDebuffStacks;
                if (debuffStacks > 0)
                {
                    float cinnabarBonus = modifiedRedBonus * debuffStacks * 0.01f;
                    modifiedRedBonus += cinnabarBonus;
                    if (uiManager != null)
                        uiManager.ShowFeedback($"朱砂：{debuffStacks}层负面状态，额外伤害+{cinnabarBonus:F0}");
                }
            }

            // 3. 血性呼唤（铭刻）：额外伤害×该面点数
            if (currentEvaluation.BloodCallMultiplier > 0)
            {
                float beforeBloodCall = modifiedRedBonus;
                modifiedRedBonus *= currentEvaluation.BloodCallMultiplier;
                if (uiManager != null)
                    uiManager.ShowFeedback($"血性呼唤：额外伤害×{currentEvaluation.BloodCallMultiplier}！{beforeBloodCall:F0}→{modifiedRedBonus:F0}");
            }
        }

        // 将修正后的额外伤害替换到总伤害中
        if (originalRedBonus > 0)
        {
            damageDealt = damageDealt - originalRedBonus + modifiedRedBonus;
        }

        if (currentEvaluation.BonusMoney > 0)
        {
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null)
                SaveManager.Instance.AddMoney(currentEvaluation.BonusMoney);
            if (uiManager != null)
                uiManager.ShowFeedback($"获得 {currentEvaluation.BonusMoney} 金粉！");
        }

        // 保存闪避概率（紫色骰子效果，在敌人攻击时使用）
        currentDodgeChance += currentEvaluation.DodgeChance;

        // 暴击判定（黄色骰子效果）
        bool isCrit = false;
        float critMultiplier = 1f;
        if (currentEvaluation.CritRate > 0f)
        {
            float roll = Random.value;
            if (roll < currentEvaluation.CritRate)
            {
                isCrit = true;
                critMultiplier = 1f + currentEvaluation.CritDamage;
                damageDealt = Mathf.Ceil(damageDealt * critMultiplier);
            }
        }

        // 橙色连击
        float comboDamage = 0f;
        if (currentEvaluation.ComboHits > 0)
        {
            int totalComboHits = Mathf.CeilToInt(currentEvaluation.ComboHits * currentEvaluation.FinalMultiplier);
            comboDamage = currentEvaluation.BaseDamage * totalComboHits;
            damageDealt += comboDamage;
        }

        // 对目标敌人造成伤害
        targetEnemy.TakeDamage(Mathf.CeilToInt(damageDealt));

        // 显示伤害反馈
        if (uiManager != null && damageDealt > 0)
        {
            string feedbackText = $"对 {targetEnemy.Name} 造成 {Mathf.CeilToInt(damageDealt)} 点伤害";
            if (isCrit) feedbackText += $"（暴击！×{critMultiplier:F1}）";
            if (comboDamage > 0) feedbackText += $"（含连击 {Mathf.CeilToInt(comboDamage)}）";
            feedbackText += "！";
            uiManager.ShowFeedback(feedbackText);
        }

        // 黑色骰子真实伤害
        if (currentEvaluation.TrueDamage > 0)
        {
            int trueDmg = Mathf.CeilToInt(currentEvaluation.TrueDamage);
            targetEnemy.TakeDamage(trueDmg);
            if (uiManager != null)
                uiManager.ShowFeedback($"黑色毁灭！对 {targetEnemy.Name} 造成 {trueDmg} 点真实伤害！");
        }

        // 红蛆道具：额外伤害溅射相邻敌人10%
        if (modifiedRedBonus > 0 && ItemManager.Instance != null
            && ItemManager.Instance.HasItem(ItemType.RedMaggot))
        {
            int splashDamage = Mathf.CeilToInt(modifiedRedBonus * 0.1f);
            if (splashDamage > 0)
            {
                var adjacentSlots = targetEnemy.GetAdjacentSlotIndices();
                foreach (int adjIdx in adjacentSlots)
                {
                    if (adjIdx >= 0 && adjIdx < enemies.Count && enemies[adjIdx].IsAlive)
                    {
                        enemies[adjIdx].TakeDamage(splashDamage);
                        if (uiManager != null)
                            uiManager.ShowFeedback($"红蛆：对 {enemies[adjIdx].Name} 溅射 {splashDamage} 点伤害！");
                    }
                }
            }
        }

        // 绿色回复
        if (currentEvaluation.HealAmount > 0)
        {
            int healAmt = Mathf.CeilToInt(currentEvaluation.HealAmount);
            playerHP = Mathf.Min(playerMaxHP, playerHP + healAmt);
            if (uiManager != null)
                uiManager.ShowFeedback($"回复 {healAmt} 点血量！");
        }

        // 酒红：回复额外伤害30%
        if (modifiedRedBonus > 0 && ItemManager.Instance != null
            && ItemManager.Instance.HasItem(ItemType.WineRed))
        {
            int wineHeal = Mathf.CeilToInt(modifiedRedBonus * 0.3f);
            playerHP = Mathf.Min(playerMaxHP, playerHP + wineHeal);
            if (uiManager != null)
                uiManager.ShowFeedback($"酒红：回复 {wineHeal} 点血量！");
        }

        // 庞贝红：牌型中每个红色骰子附加10层流血
        if (currentEvaluation.RedCountInHand > 0 && ItemManager.Instance != null
            && ItemManager.Instance.HasItem(ItemType.PompeiiRed))
        {
            int bleedStacks = currentEvaluation.RedCountInHand * 10;
            bool delayRemoval = currentEvaluation.HasHellfire;
            if (currentEvaluation.HasHellfire)
            {
                bleedStacks = Mathf.CeilToInt(bleedStacks * 1.5f);
            }
            targetEnemy.AddBleeding(bleedStacks, delayRemoval);
            if (uiManager != null)
            {
                string bleedMsg = $"庞贝红：对 {targetEnemy.Name} 附加 {bleedStacks} 层流血！";
                if (currentEvaluation.HasHellfire) bleedMsg += "（地狱火：+50%，推迟移除）";
                uiManager.ShowFeedback(bleedMsg);
            }
        }

        UpdateAllUI();
        yield return new WaitForSeconds(0.8f);

        // 处理打出的骰子（放入弃牌堆、补牌）
        ProcessPlayedDice();

        // 重新生成行动点
        if (config != null && config.game_rules != null)
        {
            SpawnActionPointDots(config.game_rules.base_action_points);
        }

        // 重新启用按钮
        if (uiManager != null)
            uiManager.SetEndTurnButtonInteractable(true);
    }

    /// <summary>
    /// ResolutionPhase 保留作为兼容入口，转发到 ResolveSinglePlay
    /// </summary>
    private IEnumerator ResolutionPhase()
    {
        // 使用当前选中的目标
        int target = selectedEnemyIndex >= 0 ? selectedEnemyIndex : GetFirstAliveEnemyIndex();
        yield return StartCoroutine(ResolveSinglePlay(target));
    }

    /// <summary>
    /// 敌人攻击阶段：所有存活敌人依次攻击玩家
    /// </summary>
    private IEnumerator EnemyAttackPhase()
    {
        SetState(GameState.EnemyAttack);

        if (uiManager != null)
            uiManager.SetEndTurnButtonInteractable(false);

        float totalShieldValue = currentEvaluation.ShieldValue;
        float remainingShield = totalShieldValue;

        // 紫色闪避判定（对整个敌人攻击阶段生效一次）
        bool dodged = false;
        if (currentDodgeChance > 0f)
        {
            float dodgeRoll = Random.value;
            if (dodgeRoll < currentDodgeChance)
            {
                dodged = true;
                if (uiManager != null)
                {
                    uiManager.ShowFeedback("闪避成功！完全躲避了敌人的攻击！");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            currentDodgeChance = 0f;
        }

        if (!dodged)
        {
            // 每个存活敌人依次攻击
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                // 用该敌人的骰子计算伤害
                EnemyDamageInfo dmgInfo = CalculateEnemyDamageForInstance(enemy);
                float rawDamage = dmgInfo.TotalDamage;

                // 护盾吸收
                float absorbed = Mathf.Min(remainingShield, rawDamage);
                remainingShield -= absorbed;
                float netDamage = Mathf.Max(0, rawDamage - absorbed);

                if (uiManager != null)
                {
                    if (absorbed > 0)
                    {
                        uiManager.ShowFeedback($"护盾吸收 {enemy.Name} 的 {absorbed:F0} 点伤害");
                    }
                    if (netDamage > 0)
                    {
                        uiManager.ShowFeedback($"{enemy.Name} 攻击！受到 {Mathf.CeilToInt(netDamage)} 点伤害！");
                    }
                }

                playerHP -= Mathf.CeilToInt(netDamage);
                playerHP = Mathf.Max(0, playerHP);

                UpdateAllUI();
                yield return new WaitForSeconds(0.5f);

                if (playerHP <= 0)
                {
                    SetState(GameState.GameOver);
                    yield break;
                }
            }
        }

        // 回合结束：处理所有敌人的流血移除
        foreach (var enemy in enemies)
        {
            if (enemy.IsAlive)
                enemy.ProcessTurnEndBleeding();
        }

        // 准备下一回合
        currentTurn++;
        UpdateAllUI();
        yield return new WaitForSeconds(0.5f);

        SetState(GameState.EnemyTurn);
    }

    private IEnumerator GameOverPhase()
    {
        SetState(GameState.GameOver);

        // 显示GameOver面板
        if (uiManager != null)
        {
            uiManager.ShowGameOver(AreAllEnemiesDead());
        }

        yield return null;
    }
    #endregion

    #region State Management
    private void SetState(GameState newState)
    {
        currentState = newState;

        if (uiManager != null)
        {
            uiManager.UpdateGameState(GetStateDisplayName(newState));
        }

        // Debug.Log($"Game State: {newState}");
    }

    private string GetStateDisplayName(GameState state)
    {
        switch (state)
        {
            case GameState.Setup:
                return "准备中";
            case GameState.EnemyTurn:
                return "敌人回合";
            case GameState.PlayerTurn:
                return "玩家回合";
            case GameState.Resolution:
                return "结算中";
            case GameState.TargetSelection:
                return "选择目标";
            case GameState.EnemyAttack:
                return "敌人攻击";
            case GameState.GameOver:
                return "游戏结束";
            default:
                return state.ToString();
        }
    }
    #endregion

    #region Dice Management
    /// <summary>
    /// 确保存在DiceManager并用当前手牌生成3D骰子
    /// </summary>
    private void EnsureDiceManagerAndSpawnHand()
    {
        if (playerHand == null || playerHand.Count == 0)
        {
            return;
        }

        DiceManager diceManager = DiceManager.Instance;
        if (diceManager == null)
        {
            GameObject diceManagerObj = new GameObject("DiceManager");
            diceManager = diceManagerObj.AddComponent<DiceManager>();
        }

        if (diceManager == null)
        {
            Debug.LogWarning("[BattleManager] 无法创建 DiceManager，跳过3D骰子生成。");
            return;
        }

        diceManager.SpawnRuntimeDiceHand(new List<RuntimeDice>(playerHand));
    }

    private void SpawnRandomDiceForPlayer(int handSize)
    {
        playerHand.Clear();
        drawPile.Clear();
        discardPile.Clear();

        // 获取总骰子数
        int totalDice = config?.game_rules?.total_dice_count ?? handSize;

        // 加载所有骰子
        List<RuntimeDice> allDice = new List<RuntimeDice>();
        var currentDiceSet = config?.initial_dice_sets?.GetCurrentSet();
        if (currentDiceSet?.dice != null && currentDiceSet.dice.Count > 0)
        {
            for (int i = 0; i < totalDice; i++)
            {
                var diceConfig = currentDiceSet.dice[i % currentDiceSet.dice.Count];
                RuntimeDice dice = RuntimeDice.FromConfig(diceConfig);
                if (dice != null)
                {
                    dice.ActiveFaceIndex = Random.Range(0, dice.Faces.Count);
                    allDice.Add(dice);
                }
            }
        }
        else
        {
            for (int i = 0; i < totalDice; i++)
            {
                allDice.Add(CreateFallbackDice());
            }
        }

        // 洗牌
        ShuffleList(allDice);

        // 前handSize个进入手牌，其余进入待抽堆
        for (int i = 0; i < allDice.Count; i++)
        {
            if (i < handSize)
            {
                playerHand.Add(allDice[i]);
            }
            else
            {
                drawPile.Add(allDice[i]);
            }
        }
    }

    /// <summary>
    /// Fisher-Yates洗牌
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private RuntimeDice CreateFallbackDice()
    {
        // 创建一个简单的6面骰子作为后备
        var diceConfig = new DiceConfig
        {
            id = 0,
            faces = new List<DiceFaceConfig>
            {
                new DiceFaceConfig { value = 1, color = "None" },
                new DiceFaceConfig { value = 2, color = "None" },
                new DiceFaceConfig { value = 3, color = "None" },
                new DiceFaceConfig { value = 4, color = "None" },
                new DiceFaceConfig { value = 5, color = "None" },
                new DiceFaceConfig { value = 6, color = "None" }
            },
            active_face_index = Random.Range(0, 6)
        };

        return RuntimeDice.FromConfig(diceConfig);
    }

    private void ResetAllDiceColors()
    {
        foreach (var dice in playerHand)
        {
            if (dice != null)
            {
                foreach (var face in dice.Faces)
                {
                    face.Reset();
                }
            }
        }
    }

    /// <summary>
    /// 投掷敌人骰子
    /// </summary>
    private void RollEnemyDice()
    {
        enemyHand.Clear();

        // 从敌人配置加载骰子
        if (config?.enemy_template_m0?.dice != null && config.enemy_template_m0.dice.Count > 0)
        {
            var enemyDiceConfigs = config.enemy_template_m0.dice;
            for (int i = 0; i < EnemyDiceCount; i++)
            {
                var diceConfig = enemyDiceConfigs[i % enemyDiceConfigs.Count];
                RuntimeDice dice = RuntimeDice.FromConfig(diceConfig);
                if (dice != null)
                {
                    // 随机投掷骰子
                    dice.ActiveFaceIndex = Random.Range(0, dice.Faces.Count);
                    enemyHand.Add(dice);
                }
            }
        }

        while (enemyHand.Count < EnemyDiceCount)
        {
            RuntimeDice fallbackDice = CreateFallbackDice();
            if (fallbackDice != null && fallbackDice.Faces != null && fallbackDice.Faces.Count > 0)
            {
                fallbackDice.ActiveFaceIndex = Random.Range(0, fallbackDice.Faces.Count);
                enemyHand.Add(fallbackDice);
            }
            else
            {
                break;
            }
        }

        DiceManager diceManager = DiceManager.Instance;
        if (diceManager != null && enemyHand.Count > 0)
        {
            diceManager.ThrowEnemyDiceBatch(enemyHand);
        }
    }

    /// <summary>
    /// 计算敌人伤害（参考HandEvaluator但简化，只取点数和×10）
    /// </summary>
    private EnemyDamageInfo CalculateEnemyDamage()
    {
        // 结算前强制同步一次，确保读取到敌方骰子当前朝上面
        DiceManager diceManager = DiceManager.Instance;
        diceManager?.SyncEnemyDiceTopFacesToRuntime();

        List<RuntimeDice> damageSourceHand = enemyHand;
        if (diceManager != null)
        {
            List<RuntimeDice> sceneEnemyDice = diceManager.GetEnemyRuntimeDiceFromScene();
            if (sceneEnemyDice != null && sceneEnemyDice.Count > 0)
            {
                damageSourceHand = sceneEnemyDice;
            }
        }

        if (damageSourceHand == null || damageSourceHand.Count == 0)
        {
            return new EnemyDamageInfo(0, 0, 1f);
        }

        // 获取所有激活面
        List<DiceFace> activeFaces = damageSourceHand
            .Where(d => d.ActiveFace != null)
            .Select(d => d.ActiveFace)
            .ToList();

        if (activeFaces.Count == 0)
        {
            return new EnemyDamageInfo(0, 0, 1f);
        }

        // 获取点数
        List<int> values = activeFaces.Select(f => f.value).ToList();

        // 识别最佳牌型并获取倍率
        var (handName, baseMultiplier, handIndices) = IdentifyEnemyBestHand(values);

        // 计算基础伤害（只计算牌型内的骰子）
        float baseDamage = 0f;
        foreach (int index in handIndices)
        {
            baseDamage += values[index];
        }

        // 应用难度倍率
		float finalMultiplier = Mathf.Max(1f, baseMultiplier * enemyDamageMultiplier);
		
		// 最终伤害 = 基础伤害 × 倍率
		float totalDamage = Mathf.Ceil(baseDamage * finalMultiplier);

        return new EnemyDamageInfo(totalDamage, baseDamage, finalMultiplier);
    }

    /// <summary>
    /// 识别敌人最佳牌型（简化版，只判断点数相同）
    /// </summary>
    private (string handName, float multiplier, List<int> handIndices) IdentifyEnemyBestHand(List<int> values)
    {
        List<int> indices;

        // 三条
        if (TryGetEnemyNOfAKind(values, 3, out indices))
            return ("三条", config.score_multipliers.three_of_a_kind, indices);

        // 对子
        if (TryGetEnemyNOfAKind(values, 2, out indices))
            return ("对子", config.score_multipliers.pair, indices);

        // 高牌（取最大值）
        indices = new List<int> { values.IndexOf(values.Max()) };
        return ("高牌", config.score_multipliers.high_card, indices);
    }

    /// <summary>
    /// 尝试获取N条
    /// </summary>
    private bool TryGetEnemyNOfAKind(List<int> values, int n, out List<int> indices)
    {
        indices = new List<int>();
        var group = values.Select((v, i) => new { Value = v, Index = i })
            .GroupBy(x => x.Value)
            .FirstOrDefault(g => g.Count() >= n);

        if (group != null)
        {
            indices = group.Select(x => x.Index).ToList();
            return true;
        }
        return false;
    }

    private void ProcessDiceAfterTurn()
    {
        // 黑色骰子破碎：先永久移除黑色骰子（不进弃牌堆）
        ShatterBlackDice();

        // 收集被消耗（选中打出）的骰子索引
        HashSet<int> consumedIndices = new HashSet<int>();

        for (int i = 0; i < playerHand.Count; i++)
        {
            if (playerHand[i] != null && playerHand[i].isSelected)
            {
                consumedIndices.Add(i);
            }
        }

        DiceManager diceManager = DiceManager.Instance;
        if (diceManager != null)
        {
            var activeDice = diceManager.GetAllActiveDice();
            int checkCount = Mathf.Min(activeDice.Count, playerHand.Count);
            for (int i = 0; i < checkCount; i++)
            {
                var sceneDice = activeDice[i];
                if (sceneDice == null) continue;

                if (sceneDice.IsSelected || (sceneDice.RuntimeData != null && sceneDice.RuntimeData.isSelected))
                {
                    consumedIndices.Add(i);
                }
            }
        }

        // 将使用过的骰子移入弃牌堆（重置后放入），按索引从大到小移除
        var sortedConsumed = consumedIndices.OrderByDescending(i => i).ToList();
        foreach (int index in sortedConsumed)
        {
            if (index >= 0 && index < playerHand.Count && playerHand[index] != null)
            {
                var usedDice = playerHand[index];
                usedDice.isSelected = false;
                // 重置骰子面到原始状态
                foreach (var face in usedDice.Faces)
                {
                    face.Reset();
                }
                discardPile.Add(usedDice);
                playerHand.RemoveAt(index);
            }
        }

        // 重置剩余手牌选中状态
        foreach (var runtimeDice in playerHand)
        {
            if (runtimeDice != null)
            {
                runtimeDice.isSelected = false;
            }
        }

        // 从待抽堆抽取骰子补满手牌
        int targetHandSize = config != null ? config.game_rules.hand_size : 6;
        while (playerHand.Count < targetHandSize)
        {
            RuntimeDice drawn = DrawFromPile();
            if (drawn == null) break; // 没有骰子可抽了
            drawn.ActiveFaceIndex = Random.Range(0, drawn.Faces.Count);
            playerHand.Add(drawn);
        }

        // 更新场景骰子
        if (diceManager != null)
        {
            diceManager.SpawnRuntimeDiceHand(new List<RuntimeDice>(playerHand));
        }
        else
        {
            EnsureDiceManagerAndSpawnHand();
        }

        // 生成行动点(Dot)
        if (config != null && config.game_rules != null)
        {
            SpawnActionPointDots(config.game_rules.base_action_points);
        }
    }

    /// <summary>
    /// 从待抽堆抽取一个骰子。若待抽堆为空，将弃牌堆洗牌后放入待抽堆。
    /// </summary>
    private RuntimeDice DrawFromPile()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
                return null;

            // 弃牌堆洗入待抽堆
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleList(drawPile);
        }

        var dice = drawPile[0];
        drawPile.RemoveAt(0);
        return dice;
    }

	/// <summary>
	/// 黑色骰子破碎：永久移除骰子
	/// </summary>
	private void ShatterBlackDice()
	{
		if (currentEvaluation.ShatteredDiceIndices == null || currentEvaluation.ShatteredDiceIndices.Count == 0)
			return;

		// 按索引从大到小移除，避免索引偏移
		var sortedIndices = currentEvaluation.ShatteredDiceIndices.OrderByDescending(i => i).ToList();
		foreach (int index in sortedIndices)
		{
			if (index >= 0 && index < playerHand.Count)
			{
				playerHand.RemoveAt(index);
			}
		}
	}
    private void SpawnActionPointDots(int count)
    {

        List<DiceColor> dotTypes = new List<DiceColor>();

        var motherDiceConfig = config.mother_dice_pool.dice[0];
        var motherDice = RuntimeDice.FromConfig(motherDiceConfig);
        for (int i = 0; i < count; i++)
        {
            // Roll the mother die
            if (motherDice != null && motherDice.Faces.Count > 0)
            {
                int faceIndex = Random.Range(0, motherDice.Faces.Count);
                var face = motherDice.Faces[faceIndex];
                
                // Add dots based on face value - each face generates multiple dots based on its value
                for (int j = 0; j < face.value; j++)
                {
                    dotTypes.Add(face.color);
                }
            }
        }


        uiManager.SpawnDots(dotTypes);

    }

    private void ClearAllDots()
    {
        if (uiManager != null)
        {
            uiManager.ClearDots();
        }
        // Debug.Log("All Action Point Dots cleared.");
    }

    /// <summary>
    /// 按颜色排序骰子
    /// </summary>
    public void SortDiceByColor()
    {
        if (playerHand == null || playerHand.Count == 0) return;

        playerHand.Sort((a, b) => 
        {
            int colorComparison = CompareDiceColors(a.ActiveFace.color, b.ActiveFace.color);
            if (colorComparison != 0) return colorComparison;
            // 颜色相同则按点数降序排列
            return b.ActiveFace.value.CompareTo(a.ActiveFace.value);
        });
    }

    /// <summary>
    /// 按点数排序骰子
    /// </summary>
    public void SortDiceByValue()
    {
        if (playerHand == null || playerHand.Count == 0) return;

        playerHand.Sort((a, b) => 
        {
            // 按点数降序排列
            int valueComparison = b.ActiveFace.value.CompareTo(a.ActiveFace.value);
            if (valueComparison != 0) return valueComparison;
            // 点数相同则按颜色排序
            return CompareDiceColors(a.ActiveFace.color, b.ActiveFace.color);
        });
    }

    /// <summary>
    /// 比较骰子颜色（自定义排序顺序）
    /// </summary>
    private int CompareDiceColors(DiceColor a, DiceColor b)
    {
        // 自定义颜色排序顺序：红 > 黄 > 蓝 > 橙 > 绿 > 紫 > 黑 > 无色
        int GetColorPriority(DiceColor color)
        {
            switch (color)
            {
                case DiceColor.Red: return 0;
                case DiceColor.Yellow: return 1;
                case DiceColor.Blue: return 2;
                case DiceColor.Orange: return 3;
                case DiceColor.Green: return 4;
                case DiceColor.Purple: return 5;
                case DiceColor.Black: return 6;
                case DiceColor.None: return 7;
                default: return 8;
            }
        }

        return GetColorPriority(a).CompareTo(GetColorPriority(b));
    }
    #endregion

    #region Hand Evaluation
    /// <summary>
    /// 评估当前手牌并更新UI
    /// 每当骰子发生变化时都应该调用此方法
    /// </summary>
    public void EvaluateHand()
    {
        if (handEvaluator == null)
        {
            // Debug.LogWarning("BattleManager: HandEvaluator is null!");
            return;
        }

        // 筛选被选中的骰子
        var selectedDice = playerHand.Where(d => d.isSelected).ToList();

        // 评估手牌
        currentEvaluation = handEvaluator.Evaluate(selectedDice);

        //Debug.Log($"Hand Evaluated: {currentEvaluation.HandName}, Damage={currentEvaluation.TotalDamage}, Shield={currentEvaluation.ShieldValue}");

        // 更新UI
        UpdateAllUI();
    }
    #endregion

    #region Enemy AI

    #endregion

    #region UI Updates
    private void UpdateAllUI()
    {
        if (uiManager == null) return;

        // 检查是否有选中的骰子
        bool hasSelectedDice = playerHand != null && playerHand.Any(d => d.isSelected);

        // 计算怪物伤害
        currentEnemyDamage = CalculateEnemyDamage();

        bool showEnemyDamage = currentState == GameState.PlayerTurn && 
                               DiceManager.Instance != null && 
                               !DiceManager.Instance.IsEnemyThrowing;

        uiManager.UpdateBattleInfo(
            playerHP,
            enemyHP,
            currentEvaluation.TotalDamage,
            currentEvaluation.BaseDamage,
            currentEvaluation.FinalMultiplier,
            currentEvaluation.HandName,
            currentTurn,
            playerMaxHP,
            enemyMaxHP,
            currentEvaluation.ShieldValue,
            Mathf.CeilToInt(currentEnemyDamage.TotalDamage),
            currentEnemyDamage.BaseDamage,
            currentEnemyDamage.FinalMultiplier,
            hasSelectedDice,
            currentEvaluation.ActiveEffects,
            showEnemyDamage
        );

        // 刷新道具栏和敌人状态UI
        uiManager.RefreshItemDisplay();
        uiManager.RefreshEnemyStatus();

        // 刷新多敌人面板
        uiManager.RefreshEnemyPanel(enemies, selectedEnemyIndex);
        
    }

    #endregion

    #region Button Callbacks
    private void OnEndTurnClicked()
    {
        if (currentState == GameState.PlayerTurn)
        {
            // 标记回合结束，让PlayerTurnPhase退出循环
            playerTurnEnded = true;
        }
        else if (currentState == GameState.TargetSelection)
        {
            // 在目标选择阶段也可以结束回合（取消出牌）
            playerTurnEnded = true;
            targetSelected = true; // 解除等待
        }
    }

    /// <summary>
    /// 玩家点击"出牌"按钮
    /// </summary>
    public void OnPlayClicked()
    {
        if (currentState == GameState.PlayerTurn)
        {
            playRequested = true;
        }
    }

    /// <summary>
    /// 玩家点击选择目标敌人
    /// </summary>
    public void OnEnemyTargetSelected(int index)
    {
        if (currentState == GameState.TargetSelection)
        {
            if (index >= 0 && index < enemies.Count && enemies[index].IsAlive)
            {
                selectedEnemyIndex = index;
                targetSelected = true;
            }
        }
    }

    private void RestartGame()
    {
        if (currentState == GameState.GameOver)
        {
            HandleBattleFinishedTransition();
        }
        else
        {
            // 战斗未结束时的重开（如调试用），重新加载当前场景
            GameSceneManager.ReloadCurrentScene();
        }
    }

    private void HandleBattleFinishedTransition()
    {
        if (hasHandledGameOverTransition)
        {
            return;
        }

        hasHandledGameOverTransition = true;

        if (AreAllEnemiesDead()) // 玩家胜利
        {
            // 重置所有骰子面并合并保存
            SaveVictoryData();

            // 如果有奖励UI，显示道具选择后再跳转
            if (battleRewardUI != null)
            {
                battleRewardUI.OnRewardCompleted = OnRewardCompleted;
                battleRewardUI.ShowRewards();
            }
            else
            {
                // 无奖励UI，直接跳转
                GameSceneManager.LoadMap();
            }
        }
        else // 玩家失败
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.DeleteSaveFile();
            }

            GameSceneManager.LoadStartMenu();
        }
    }

    /// <summary>
    /// 保存胜利数据（骰子、金粉、HP）
    /// </summary>
    private void SaveVictoryData()
    {
        var allDice = new List<RuntimeDice>();
        allDice.AddRange(playerHand);
        allDice.AddRange(drawPile);
        allDice.AddRange(discardPile);
        foreach (var dice in allDice)
        {
            if (dice != null)
            {
                foreach (var face in dice.Faces)
                {
                    face.Reset();
                }
            }
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdatePlayerStats(playerHP, SaveManager.Instance.GetMoney());
            SaveManager.Instance.AddMoney(10);
            SaveManager.Instance.UpdatePlayerDice(allDice);
        }
    }

    /// <summary>
    /// 奖励选择完成后的回调
    /// </summary>
    private void OnRewardCompleted()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();

        GameSceneManager.LoadMap();
    }
    #endregion

    #region Public Methods for External Triggers
    /// <summary>
    /// 当骰子发生变化时（由UI或其他系统触发）
    /// </summary>
    public void OnDiceChanged()
    {
        if (currentState == GameState.PlayerTurn)
        {
            EvaluateHand();
        }
    }

    /// <summary>
    /// 当骰子选中状态发生变化时调用
    /// </summary>
    public void OnDiceSelectionChanged()
    {
        if (currentState == GameState.PlayerTurn)
        {
            EvaluateHand();
        }
    }

    private void OnEnemyDiceArranged()
    {
        if (currentState == GameState.PlayerTurn)
        {
            UpdateAllUI();
        }
    }
    #endregion
}
