using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// Battle场景自动配置 - 添加出牌按钮、敌人面板、战斗奖励UI
/// 菜单: Dot Tools > Setup Battle Scene UI
/// </summary>
public class BattleSceneSetup : Editor
{
    [MenuItem("Dot Tools/Setup Battle Scene UI")]
    public static void SetupBattleUI()
    {
        // 确认当前场景
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.name.Contains("Battle"))
        {
            if (!EditorUtility.DisplayDialog("警告", 
                $"当前场景是 '{scene.name}'，不是Battle场景。确定继续？", "继续", "取消"))
                return;
        }

        // 找到Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到Canvas！", "确定");
            return;
        }

        // 找到GameUIManager
        GameUIManager uiManager = FindFirstObjectByType<GameUIManager>();
        if (uiManager == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到GameUIManager！", "确定");
            return;
        }

        // 找到BattleManager
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到BattleManager！", "确定");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Battle Scene UI");

        // === 1. 创建"出牌"按钮 ===
        GameObject playBtnObj = CreatePlayButton(canvas.transform);

        // === 2. 创建敌人面板 ===
        GameObject enemyPanelObj = CreateEnemyPanel(canvas.transform);

        // === 3. 创建战斗奖励面板 ===
        GameObject rewardPanelObj = CreateBattleRewardPanel(canvas.transform);

        // === 4. 连接引用 ===
        WireReferences(uiManager, battleManager, playBtnObj, enemyPanelObj, rewardPanelObj);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("完成", 
            "Battle场景UI配置完成！\n\n已添加:\n• 出牌按钮\n• 敌人面板 (3槽位)\n• 战斗奖励面板\n\n请保存场景 (Ctrl+S)", "确定");
    }

    /// <summary>
    /// 创建出牌按钮（放在打出按钮旁边）
    /// </summary>
    private static GameObject CreatePlayButton(Transform canvasTransform)
    {
        // 检查是否已存在
        Transform existing = canvasTransform.Find("出牌按钮");
        if (existing != null)
        {
            Debug.Log("[BattleSetup] 出牌按钮已存在，跳过创建");
            return existing.gameObject;
        }

        GameObject btnObj = new GameObject("出牌按钮", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(btnObj, "Create Play Button");
        btnObj.transform.SetParent(canvasTransform, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-380, 57); // 左侧偏移
        rect.sizeDelta = new Vector2(180, 180);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        // 按钮文字
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "出牌";
        tmp.fontSize = 46;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        Debug.Log("[BattleSetup] 已创建出牌按钮");
        return btnObj;
    }

    /// <summary>
    /// 创建敌人面板（3个敌人槽位）
    /// </summary>
    private static GameObject CreateEnemyPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("敌人面板");
        if (existing != null)
        {
            Debug.Log("[BattleSetup] 敌人面板已存在，跳过创建");
            return existing.gameObject;
        }

        // 面板容器
        GameObject panelObj = new GameObject("敌人面板", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(panelObj, "Create Enemy Panel");
        panelObj.transform.SetParent(canvasTransform, false);
        panelObj.AddComponent<EnemyPanelUI>();

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1);
        panelRect.anchorMax = new Vector2(0.5f, 1);
        panelRect.pivot = new Vector2(0.5f, 1);
        panelRect.anchoredPosition = new Vector2(0, -20);
        panelRect.sizeDelta = new Vector2(900, 200);

        HorizontalLayoutGroup layout = panelObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(10, 10, 10, 10);

        // 创建3个敌人槽位
        EnemySlotUI[] slots = new EnemySlotUI[3];
        for (int i = 0; i < 3; i++)
        {
            slots[i] = CreateEnemySlot(panelObj.transform, i);
        }

        // 将slots赋值给EnemyPanelUI
        var panel = panelObj.GetComponent<EnemyPanelUI>();
        SerializedObject so = new SerializedObject(panel);
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }
        so.ApplyModifiedProperties();

        Debug.Log("[BattleSetup] 已创建敌人面板（3槽位）");
        return panelObj;
    }

    /// <summary>
    /// 创建单个敌人槽位
    /// </summary>
    private static EnemySlotUI CreateEnemySlot(Transform parent, int index)
    {
        GameObject slotObj = new GameObject($"EnemySlot_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slotObj.transform.SetParent(parent, false);

        EnemySlotUI slot = slotObj.AddComponent<EnemySlotUI>();

        Image bgImage = slotObj.GetComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // 选中指示器
        GameObject indicator = new GameObject("SelectedIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        indicator.transform.SetParent(slotObj.transform, false);
        RectTransform indRect = indicator.GetComponent<RectTransform>();
        indRect.anchorMin = Vector2.zero;
        indRect.anchorMax = Vector2.one;
        indRect.sizeDelta = new Vector2(4, 4);
        indRect.offsetMin = new Vector2(-2, -2);
        indRect.offsetMax = new Vector2(2, 2);
        Image indImg = indicator.GetComponent<Image>();
        indImg.color = new Color(1f, 0.5f, 0f, 0.8f);
        indImg.type = Image.Type.Sliced;
        indicator.SetActive(false);

        // 名称文本
        GameObject nameObj = CreateTMPChild(slotObj.transform, "Name", "敌人", 22,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -5), new Vector2(0, 35));
        
        // HP文本
        GameObject hpObj = CreateTMPChild(slotObj.transform, "HP", "100/100", 20,
            new Vector2(0, 0.3f), new Vector2(1, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 流血文本
        GameObject bleedObj = CreateTMPChild(slotObj.transform, "Bleeding", "", 18,
            new Vector2(0, 0), new Vector2(1, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bleedObj.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.1f, 0.1f);

        // 血条填充
        GameObject healthFill = new GameObject("HealthFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        healthFill.transform.SetParent(slotObj.transform, false);
        RectTransform fillRect = healthFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.05f, 0.6f);
        fillRect.anchorMax = new Vector2(0.95f, 0.65f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = healthFill.GetComponent<Image>();
        fillImg.color = Color.green;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;

        // 连接EnemySlotUI引用
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("nameText").objectReferenceValue = nameObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("hpText").objectReferenceValue = hpObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("bleedingText").objectReferenceValue = bleedObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        so.FindProperty("healthFillImage").objectReferenceValue = fillImg;
        so.FindProperty("selectedIndicator").objectReferenceValue = indicator;
        so.ApplyModifiedProperties();

        return slot;
    }

    /// <summary>
    /// 创建战斗奖励面板
    /// </summary>
    private static GameObject CreateBattleRewardPanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("战斗奖励面板");
        if (existing != null)
        {
            Debug.Log("[BattleSetup] 战斗奖励面板已存在，跳过创建");
            return existing.gameObject;
        }

        // 全屏遮罩背景
        GameObject panelObj = new GameObject("战斗奖励面板", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panelObj, "Create Battle Reward Panel");
        panelObj.transform.SetParent(canvasTransform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.7f);

        BattleRewardUI rewardUI = panelObj.AddComponent<BattleRewardUI>();

        // 标题
        GameObject titleObj = CreateTMPChild(panelObj.transform, "Title", "选择奖励", 48,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -60), new Vector2(400, 60));

        // 道具卡片容器
        GameObject containerObj = new GameObject("ItemContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerObj.transform.SetParent(panelObj.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(900, 300);

        HorizontalLayoutGroup containerLayout = containerObj.GetComponent<HorizontalLayoutGroup>();
        containerLayout.spacing = 30;
        containerLayout.childAlignment = TextAnchor.MiddleCenter;
        containerLayout.childForceExpandWidth = true;
        containerLayout.childForceExpandHeight = true;

        // 跳过按钮
        GameObject skipBtnObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipBtnObj.transform.SetParent(panelObj.transform, false);
        RectTransform skipRect = skipBtnObj.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0);
        skipRect.anchorMax = new Vector2(0.5f, 0);
        skipRect.pivot = new Vector2(0.5f, 0);
        skipRect.anchoredPosition = new Vector2(0, 80);
        skipRect.sizeDelta = new Vector2(300, 60);
        skipBtnObj.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.9f);

        GameObject skipTextObj = CreateTMPChild(skipBtnObj.transform, "Text", "跳过（+20 金粉）", 28,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 创建奖励道具卡片预制体模板
        GameObject rewardItemTemplate = CreateRewardItemTemplate(panelObj.transform);
        rewardItemTemplate.SetActive(false);

        // 连接BattleRewardUI引用
        SerializedObject so = new SerializedObject(rewardUI);
        so.FindProperty("rewardPanel").objectReferenceValue = panelObj;
        so.FindProperty("itemContainer").objectReferenceValue = containerObj.transform;
        so.FindProperty("rewardItemPrefab").objectReferenceValue = rewardItemTemplate;
        so.FindProperty("skipButton").objectReferenceValue = skipBtnObj.GetComponent<Button>();
        so.FindProperty("skipButtonText").objectReferenceValue = skipTextObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        panelObj.SetActive(false); // 默认隐藏

        Debug.Log("[BattleSetup] 已创建战斗奖励面板");
        return panelObj;
    }

    /// <summary>
    /// 创建奖励道具卡片模板
    /// </summary>
    private static GameObject CreateRewardItemTemplate(Transform parent)
    {
        GameObject cardObj = new GameObject("RewardItemTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(parent, false);

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(250, 280);

        RewardItemUI rewardItem = cardObj.AddComponent<RewardItemUI>();

        // 边框
        GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderObj.transform.SetParent(cardObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(4, 4);
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.color = Color.white;
        borderImg.type = Image.Type.Sliced;

        // 名称
        GameObject nameObj = CreateTMPChild(cardObj.transform, "Name", "道具名称", 30,
            new Vector2(0, 0.7f), new Vector2(1, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 稀有度
        GameObject rarityObj = CreateTMPChild(cardObj.transform, "Rarity", "普通", 20,
            new Vector2(0, 0.55f), new Vector2(1, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 描述
        GameObject descObj = CreateTMPChild(cardObj.transform, "Description", "道具描述", 22,
            new Vector2(0, 0.15f), new Vector2(1, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 选择按钮
        GameObject selectBtnObj = new GameObject("SelectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        selectBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform selectRect = selectBtnObj.GetComponent<RectTransform>();
        selectRect.anchorMin = new Vector2(0.1f, 0.02f);
        selectRect.anchorMax = new Vector2(0.9f, 0.14f);
        selectRect.offsetMin = Vector2.zero;
        selectRect.offsetMax = Vector2.zero;
        selectBtnObj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 0.9f);

        CreateTMPChild(selectBtnObj.transform, "BtnText", "选择", 22,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 连接RewardItemUI引用
        SerializedObject so = new SerializedObject(rewardItem);
        so.FindProperty("nameText").objectReferenceValue = nameObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("descriptionText").objectReferenceValue = descObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("rarityText").objectReferenceValue = rarityObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("selectButton").objectReferenceValue = selectBtnObj.GetComponent<Button>();
        so.FindProperty("borderImage").objectReferenceValue = borderImg;
        so.ApplyModifiedProperties();

        return cardObj;
    }

    /// <summary>
    /// 连接所有引用
    /// </summary>
    private static void WireReferences(GameUIManager uiManager, BattleManager battleManager,
        GameObject playBtnObj, GameObject enemyPanelObj, GameObject rewardPanelObj)
    {
        // GameUIManager
        SerializedObject uiSO = new SerializedObject(uiManager);
        
        var playBtnProp = uiSO.FindProperty("playButton");
        if (playBtnProp != null)
        {
            playBtnProp.objectReferenceValue = playBtnObj.GetComponent<Button>();
        }

        var enemyPanelProp = uiSO.FindProperty("enemyPanelUI");
        if (enemyPanelProp != null)
        {
            enemyPanelProp.objectReferenceValue = enemyPanelObj.GetComponent<EnemyPanelUI>();
        }

        uiSO.ApplyModifiedProperties();

        // BattleManager
        SerializedObject bmSO = new SerializedObject(battleManager);
        
        var rewardProp = bmSO.FindProperty("battleRewardUI");
        if (rewardProp != null)
        {
            rewardProp.objectReferenceValue = rewardPanelObj.GetComponent<BattleRewardUI>();
        }

        bmSO.ApplyModifiedProperties();

        Debug.Log("[BattleSetup] 已连接所有引用");
    }

    /// <summary>
    /// 辅助：创建TMP文本子对象
    /// </summary>
    private static GameObject CreateTMPChild(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return obj;
    }
}
