using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 排序类型枚举
/// </summary>
public enum SortType
{
    None,
    ByColor,
    ByValue
}

/// <summary>
/// 游戏UI管理器 - 处理视觉表现
/// </summary>
public class GameUIManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Battle Info UI")]
    [Tooltip("敌人立绘图片组件")]
    [SerializeField]
    private Image enemyPortraitImage;

    [Tooltip("普通战斗敌人立绘")]
    [SerializeField]
    private List<Sprite> battleEnemyPortraits = new List<Sprite>();

    [Tooltip("精英战敌人立绘")]
    [SerializeField]
    private Sprite eliteEnemyPortrait;

    [Tooltip("Boss敌人立绘")]
    [SerializeField]
    private Sprite bossEnemyPortrait;

    [Tooltip("显示敌人HP的文本")]
    [SerializeField]
    private TextMeshProUGUI enemyHPText;

    [Tooltip("敌人血条控制脚本")]
    [SerializeField]
    private EnemyHealthBar enemyHealthBar;

    [Tooltip("显示玩家HP的文本")]
    [SerializeField]
    private TextMeshProUGUI playerHPText;

    [Tooltip("玩家血条控制脚本")]
    [SerializeField]
    private HealthBar playerHealthBar;

    [Tooltip("显示预计造成伤害的文本")]
    [SerializeField]
    private TextMeshProUGUI projectedDamageText;

    [Tooltip("显示当前牌型名称的文本")]
    [SerializeField]
    private TextMeshProUGUI currentHandNameText;

    [Tooltip("显示预计获得护盾的文本")]
    [SerializeField]
    private TextMeshProUGUI projectedEffectText;

    [Tooltip("显示即将受到伤害的文本")]
    [SerializeField]
    private TextMeshProUGUI incomingDamageText;

    [Tooltip("显示当前回合数的文本")]
    [SerializeField]
    private TextMeshProUGUI turnCountText;

    [Header("Item & Status UI")]
    [Tooltip("道具栏UI容器（挂载ItemDisplayUI脚本）")]
    [SerializeField]
    private ItemDisplayUI itemDisplayUI;

    [Tooltip("敌人状态效果UI（挂载EnemyStatusUI脚本）")]
    [SerializeField]
    private EnemyStatusUI enemyStatusUI;

    [Tooltip("多敌人面板UI（挂载EnemyPanelUI脚本）")]
    [SerializeField]
    private EnemyPanelUI enemyPanelUI;

    [Header("Resource Pool")]
    [Tooltip("生成行动点(Dot)的父容器")]
    [SerializeField]
    private Transform dotResourceContainer;

    [Tooltip("行动点(Dot)的预制体")]
    [SerializeField]
    private GameObject dotPrefab;

    private List<GameObject> spawnedDots = new List<GameObject>();

    [Header("Feedback Settings")]
    [Tooltip("漂浮文字的预制体")]
    [SerializeField]
    private GameObject feedbackTextPrefab;

    [Tooltip("漂浮文字生成的父画布")]
    [SerializeField]
    private Transform feedbackCanvas;

    [Tooltip("漂浮文字持续时间")]
    [SerializeField]
    private float feedbackDuration = 2f;

    [Tooltip("漂浮文字上升速度")]
    [SerializeField]
    private float feedbackFloatSpeed = 100f;

    [Header("Game State UI")]
    [Tooltip("显示当前游戏状态的文本")]
    [SerializeField]
    private TextMeshProUGUI gameStateText;

    [Tooltip("结束回合按钮")]
    [SerializeField]
    private Button endTurnButton;

    [SerializeField]
    private Image endTurnButtonText;

    [Tooltip("出牌按钮（选好骰子后点击打出）")]
    [SerializeField]
    private Button playButton;

    // 呼吸动效引用
    private Tween endTurnButtonBreathTween;

    [Tooltip("重新开始按钮")]
    [SerializeField]
    private Button restartButton;

    [Header("Game Over UI")]
    [Tooltip("游戏结束面板")]
    [SerializeField]
    private GameObject gameOverPanel;

    [Tooltip("游戏结束消息文本")]
    [SerializeField]
    private TextMeshProUGUI gameOverMessageText;

    [Tooltip("游戏结束后允许点击跳转前的最短延迟（秒），用于防止误触")]
    [SerializeField]
    private float gameOverClickEnableDelay = 0.15f;

    [Header("Sorting Buttons")]
    [Tooltip("按颜色排序按钮")]
    [SerializeField]
    private Button sortByColorButton;

    [Tooltip("按点数排序按钮")]
    [SerializeField]
    private Button sortByValueButton;

    #endregion

    // Events
    public System.Action OnEndTurnClicked;
    public System.Action OnPlayClicked;
    public System.Action OnRestartClicked;
    
    // 排序状态
    private SortType currentSortType = SortType.None;

    // 游戏结束后任意点击跳转控制
    private bool waitingForGameOverClick = false;
    private float gameOverShownTime = 0f;

    // HP UI handled by HealthBar

    private void Awake()
    {
        // 确保DiceManager存在
        if (DiceManager.Instance == null)
        {
            GameObject diceManagerObj = new GameObject("DiceManager");
            diceManagerObj.AddComponent<DiceManager>();
        }
    }

    private void Start()
    {        
        RefreshEnemyPortrait();

        // DiceSlotUI 已移除，3D 骰子由 DiceManager 负责实例化与展示
    }

    private void OnEnable()
    {
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());
        
        if (playButton != null)
            playButton.onClick.AddListener(() => OnPlayClicked?.Invoke());
        
        if (restartButton != null)
            restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            
        if (sortByColorButton != null)
            sortByColorButton.onClick.AddListener(() => SortDiceByColor());
            
        if (sortByValueButton != null)
            sortByValueButton.onClick.AddListener(() => SortDiceByValue());

        StartEndTurnButtonBreath();
    }

    private void OnDisable()
    {
        if (endTurnButton != null)
            endTurnButton.onClick.RemoveAllListeners();
        
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
            
        if (restartButton != null)
            restartButton.onClick.RemoveAllListeners();
            
        if (sortByColorButton != null)
            sortByColorButton.onClick.RemoveAllListeners();
            
        if (sortByValueButton != null)
            sortByValueButton.onClick.RemoveAllListeners();

        StopEndTurnButtonBreath();

        waitingForGameOverClick = false;
    }

    private void Update()
    {
        if (!waitingForGameOverClick)
            return;

        if (gameOverPanel == null || !gameOverPanel.activeInHierarchy)
        {
            waitingForGameOverClick = false;
            return;
        }

        if (Time.unscaledTime - gameOverShownTime < gameOverClickEnableDelay)
            return;

        bool hasMouseClick = Input.GetMouseButtonDown(0);
        bool hasTouchClick = false;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                {
                    hasTouchClick = true;
                    break;
                }
            }
        }

        if (!hasMouseClick && !hasTouchClick)
            return;

        waitingForGameOverClick = false;
        OnRestartClicked?.Invoke();
    }

    /// <summary>
    /// 为结束回合按钮文字添加缓慢呼吸动效
    /// </summary>
    private void StartEndTurnButtonBreath()
    {
        StopEndTurnButtonBreath();

        if (endTurnButtonText == null)
            return;

        RectTransform rectTransform = endTurnButtonText.rectTransform;
        if (rectTransform == null)
            return;

        // 轻微缩放呼吸动画
        endTurnButtonBreathTween = rectTransform
            .DOScale(1.05f, 1.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 停止结束回合按钮文字呼吸动效
    /// </summary>
    private void StopEndTurnButtonBreath()
    {
        if (endTurnButtonBreathTween != null)
        {
            endTurnButtonBreathTween.Kill();
            endTurnButtonBreathTween = null;
        }

        if (endTurnButtonText != null)
        {
            RectTransform rectTransform = endTurnButtonText.rectTransform;
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
            }
        }
    }

    public void UpdateGameState(string stateName)
    {
        if (gameStateText != null)
            gameStateText.text = $"{stateName}";
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = interactable;
    }

    public void SetPlayButtonInteractable(bool interactable)
    {
        if (playButton != null)
            playButton.interactable = interactable;
    }

    public void ShowGameOver(bool isWin)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        gameOverShownTime = Time.unscaledTime;
        waitingForGameOverClick = true;

        if (gameOverMessageText != null)
        {
            gameOverMessageText.text = isWin ? "胜利！\n你击败了白蚀线稿！" : "失败！\n你的存在感被抹除了...";
        }
    }

    public void HideGameOver()
    {
        waitingForGameOverClick = false;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }


    #region Public Methods

    /// <summary>
    /// 按当前战斗节点类型刷新敌人立绘
    /// </summary>
    public void RefreshEnemyPortrait()
    {
        if (enemyPortraitImage == null)
        {
            return;
        }

        Sprite targetPortrait = GetPortraitByNodeType(GameSceneManager.CurrentBattleNodeType);
        if (targetPortrait == null)
        {
            targetPortrait = GetRandomBattlePortrait();
        }

        if (targetPortrait != null)
        {
            enemyPortraitImage.sprite = targetPortrait;
        }
    }

    /// <summary>
    /// 更新战斗信息
    /// </summary>
    public void UpdateBattleInfo(int playerHP, int enemyHP, float projectedDamage,
                                  float playerBaseDamage, float playerMultiplier,
                                  string handName, int turnCount, int playerMaxHP,
                                  int enemyMaxHP,
                                  float projectedShield = 0f, int incomingDamage = 0,
                                  float enemyBaseDamage = 0f, float enemyMultiplier = 1f,
                                  bool hasSelectedDice = true,
                                  List<string> activeEffects = null,
                                  bool showIncomingDamage = true)
    {
        // 更新HP显示
        if (playerHPText != null)
        {
            playerHPText.text = $"{playerHP}/{playerMaxHP}";
        }

        if (playerHealthBar != null)
        {
            playerHealthBar.UpdateHealth(playerHP, playerMaxHP);
        }

        if (enemyHPText != null)
        {
            enemyHPText.text = $"{enemyHP}";
        }

        if (enemyHealthBar != null)
        {
            enemyHealthBar.UpdateHealth(enemyHP, enemyMaxHP);
        }

        // 根据是否有选中的骰子来显示/隐藏预测信息
        if (hasSelectedDice)
        {
            // 更新伤害预测
            if (projectedDamageText != null)
            {
                projectedDamageText.gameObject.SetActive(true);
                projectedDamageText.text = $"{playerBaseDamage:F0}×{playerMultiplier:F1}={projectedDamage:F0}";
            }

            // 更新护盾预测和特殊效果
            if (projectedEffectText != null)
            {
                projectedEffectText.gameObject.SetActive(true);
                
                string effectText = "";
                
                // 添加特殊效果描述
                if (activeEffects != null && activeEffects.Count > 0)
                {
                    foreach (var effect in activeEffects)
                    {
                        if (!string.IsNullOrEmpty(effectText))
                        {
                            effectText += "\n";
                        }
                        effectText += $"{effect}";
                    }
                }
                
                projectedEffectText.text = effectText;
            }

            // 更新当前手牌名称
            if (currentHandNameText != null)
            {
                currentHandNameText.gameObject.SetActive(true);
                currentHandNameText.text = handName ?? "无";
            }
        }
        else
        {
            // 没有选中骰子时隐藏相关信息
            if (projectedDamageText != null) projectedDamageText.gameObject.SetActive(false);
            if (projectedEffectText != null) projectedEffectText.gameObject.SetActive(false);
            if (currentHandNameText != null) currentHandNameText.gameObject.SetActive(false);
        }

        // 更新回合数
        if (turnCountText != null)
        {
            turnCountText.text = $"回合 {turnCount}";
        }

        // 更新即将到来的伤害
        if (incomingDamageText != null)
        {
            if (showIncomingDamage)
            {
                incomingDamageText.gameObject.SetActive(true);
                incomingDamageText.text = $"伤害: {enemyBaseDamage:F0}×{enemyMultiplier:F1}={incomingDamage}";
                incomingDamageText.color = incomingDamage > 0 ? Color.red : Color.gray;
            }
            else
            {
                incomingDamageText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 显示反馈文本效果
    /// </summary>
    public void ShowFeedback(string message)
    {
        StartCoroutine(ShowFeedbackCoroutine(message));
    }

    /// <summary>
    /// 更新资源池中的Dot数量
    /// </summary>
    public void SpawnDots(List<DiceColor> dotTypes)
    {
        if (dotResourceContainer == null || dotPrefab == null)
        {
            // Debug.LogWarning("GameUIManager: DotResourceContainer or DotPrefab is null!");
            return;
        }

        // 检查是否已经生成了相同数量和类型的Dot，如果是则跳过
        if (spawnedDots.Count == dotTypes.Count)
        {
            bool allMatch = true;
            for (int i = 0; i < spawnedDots.Count; i++)
            {
                if (spawnedDots[i] == null)
                {
                    allMatch = false;
                    break;
                }
                
                Dot dot = spawnedDots[i].GetComponent<Dot>();
                if (dot == null || dot.DotType != dotTypes[i])
                {
                    allMatch = false;
                    break;
                }
            }
            
            if (allMatch)
            {
                // 已经存在相同的Dot，不需要重新生成
                return;
            }
        }

        ClearDots();

        foreach (var dotType in dotTypes)
        {
            GameObject dotObj = Instantiate(dotPrefab, dotResourceContainer);
            Dot dot = dotObj.GetComponent<Dot>();

            if (dot != null)
            {
                dot.SetDotType(dotType);
            }

            spawnedDots.Add(dotObj);
        }
    }

    public void ClearDots()
    {
        foreach (var dot in spawnedDots)
        {
            if (dot != null)
            {
                Destroy(dot);
            }
        }
        spawnedDots.Clear();
    }

    /// <summary>
    /// 刷新道具栏UI
    /// </summary>
    public void RefreshItemDisplay()
    {
        if (itemDisplayUI != null)
            itemDisplayUI.Refresh();
    }

    /// <summary>
    /// 刷新敌人状态效果UI（流血等）
    /// </summary>
    public void RefreshEnemyStatus()
    {
        if (enemyStatusUI != null)
            enemyStatusUI.RefreshFromManager();
    }

    /// <summary>
    /// 刷新多敌人面板显示
    /// </summary>
    public void RefreshEnemyPanel(System.Collections.Generic.List<EnemyInstance> enemies, int selectedIndex)
    {
        if (enemyPanelUI != null)
        {
            enemyPanelUI.Refresh(enemies);
            enemyPanelUI.UpdateSelection(selectedIndex);
        }
    }

    #endregion

    private Sprite GetPortraitByNodeType(NodeType nodeType)
    {
        return nodeType switch
        {
            NodeType.Elite => eliteEnemyPortrait,
            NodeType.Boss => bossEnemyPortrait,
            _ => GetRandomBattlePortrait()
        };
    }

    private Sprite GetRandomBattlePortrait()
    {
        if (battleEnemyPortraits == null || battleEnemyPortraits.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, battleEnemyPortraits.Count);
        return battleEnemyPortraits[randomIndex];
    }

    #region Private Helper Methods

    /// <summary>
    /// 显示反馈文本的协程
    /// </summary>
    private IEnumerator ShowFeedbackCoroutine(string message)
    {
        // 如果没有预制体或画布，使用Debug输出
        if (feedbackTextPrefab == null || feedbackCanvas == null)
        {
            // Debug.Log($"[Feedback] {message}");
            yield break;
        }

        // 实例化反馈文本
        GameObject feedbackObj = Instantiate(feedbackTextPrefab, feedbackCanvas);
        TextMeshProUGUI feedbackText = feedbackObj.GetComponent<TextMeshProUGUI>();

        if (feedbackText == null)
        {
            // Debug.LogWarning("GameUIManager: Feedback prefab doesn't have TextMeshProUGUI component.");
            Destroy(feedbackObj);
            yield break;
        }

        // 设置文本
        feedbackText.text = message;

        // 设置初始位置（画布中心）
        RectTransform rectTransform = feedbackObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            
            // 使用DOTween实现向上漂浮
            Vector2 targetPos = Vector2.zero + Vector2.up * feedbackFloatSpeed;
            rectTransform.DOAnchorPos(targetPos, feedbackDuration).SetEase(Ease.OutQuad);
        }

        // 使用DOTween实现淡出效果
        if (feedbackText != null)
        {
            CanvasGroup canvasGroup = feedbackObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = feedbackObj.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = 1f;
            canvasGroup.DOFade(0f, feedbackDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => {
                    if (feedbackObj != null)
                    {
                        Destroy(feedbackObj);
                    }
                });
        }

        yield break;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        if (feedbackCanvas == null)
        {
            // 尝试自动查找Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                feedbackCanvas = canvas.transform;
            }
        }
    }

    #endregion

    #region Sorting Methods
    
    /// <summary>
    /// 按颜色排序骰子
    /// </summary>
    public void SortDiceByColor()
    {
        currentSortType = SortType.ByColor;
        
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.SortDiceByColor();
        }
        
        UpdateButtonVisuals();
    }

    /// <summary>
    /// 按点数排序骰子
    /// </summary>
    public void SortDiceByValue()
    {
        currentSortType = SortType.ByValue;
        
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.SortDiceByValue();
        }
        
        UpdateButtonVisuals();
    }

    /// <summary>
    /// 更新排序按钮的视觉效果
    /// </summary>
    private void UpdateButtonVisuals()
    {
        if (sortByColorButton != null)
        {
            var colorImage = sortByColorButton.GetComponent<Image>();
            if (colorImage != null)
            {
                colorImage.color = currentSortType == SortType.ByColor ? 
                    Color.grey : Color.black;
            }
        }

        if (sortByValueButton != null)
        {
            var valueImage = sortByValueButton.GetComponent<Image>();
            if (valueImage != null)
            {
                valueImage.color = currentSortType == SortType.ByValue ? 
                    Color.grey : Color.black;
            }
        }
    }

    #endregion

}
