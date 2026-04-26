using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// Store场景自动配置 - 创建商店UI和管理器
/// 菜单: Dot Tools > Setup Store Scene UI
/// </summary>
public class StoreSceneSetup : Editor
{
    [MenuItem("Dot Tools/Setup Store Scene UI")]
    public static void SetupStoreUI()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.name.Contains("Store"))
        {
            if (!EditorUtility.DisplayDialog("警告",
                $"当前场景是 '{scene.name}'，不是Store场景。确定继续？", "继续", "取消"))
                return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Store Scene UI");

        // === 1. 创建Canvas ===
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        GameObject canvasObj;
        if (existingCanvas != null)
        {
            canvasObj = existingCanvas.gameObject;
            Debug.Log("[StoreSetup] 使用已有Canvas");
        }
        else
        {
            canvasObj = CreateCanvas();
        }

        // === 2. 确保有EventSystem ===
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSysObj = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSysObj, "Create EventSystem");
        }

        // === 3. 创建Camera（如果没有） ===
        Camera cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Undo.RegisterCreatedObjectUndo(camObj, "Create Camera");
            camObj.tag = "MainCamera";
            cam = camObj.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        // === 4. 创建StoreManager ===
        StoreManager existingManager = FindFirstObjectByType<StoreManager>();
        GameObject managerObj;
        if (existingManager != null)
        {
            managerObj = existingManager.gameObject;
            Debug.Log("[StoreSetup] 使用已有StoreManager");
        }
        else
        {
            managerObj = new GameObject("StoreManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create StoreManager");
            managerObj.AddComponent<StoreManager>();
        }

        // === 5. 创建UI元素 ===
        Transform canvasTransform = canvasObj.transform;

        // 标题
        GameObject titleObj = FindOrCreateChild(canvasTransform, "商店标题");
        if (titleObj.GetComponent<TextMeshProUGUI>() == null)
        {
            SetupTMPElement(titleObj, "商 店", 64,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -60), new Vector2(400, 80));
        }

        // 金粉显示
        GameObject moneyObj = FindOrCreateChild(canvasTransform, "金粉显示");
        if (moneyObj.GetComponent<TextMeshProUGUI>() == null)
        {
            SetupTMPElement(moneyObj, "金粉: 0", 36,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-40, -40), new Vector2(300, 50));
            moneyObj.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;
        }

        // 反馈文本
        GameObject feedbackObj = FindOrCreateChild(canvasTransform, "反馈文本");
        if (feedbackObj.GetComponent<TextMeshProUGUI>() == null)
        {
            SetupTMPElement(feedbackObj, "", 28,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 140), new Vector2(600, 40));
            feedbackObj.GetComponent<TextMeshProUGUI>().color = Color.yellow;
        }

        // 道具卡片容器
        GameObject containerObj = FindOrCreateChild(canvasTransform, "道具容器");
        if (containerObj.GetComponent<HorizontalLayoutGroup>() == null)
        {
            containerObj.AddComponent<HorizontalLayoutGroup>();
        }
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0, 30);
        containerRect.sizeDelta = new Vector2(1000, 350);
        HorizontalLayoutGroup containerLayout = containerObj.GetComponent<HorizontalLayoutGroup>();
        containerLayout.spacing = 30;
        containerLayout.childAlignment = TextAnchor.MiddleCenter;
        containerLayout.childForceExpandWidth = true;
        containerLayout.childForceExpandHeight = true;
        containerLayout.padding = new RectOffset(20, 20, 20, 20);

        // 离开按钮
        GameObject leaveBtnObj = FindOrCreateChild(canvasTransform, "离开按钮");
        if (leaveBtnObj.GetComponent<Button>() == null)
        {
            EnsureComponents(leaveBtnObj, typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform leaveRect = leaveBtnObj.GetComponent<RectTransform>();
            leaveRect.anchorMin = new Vector2(0.5f, 0);
            leaveRect.anchorMax = new Vector2(0.5f, 0);
            leaveRect.pivot = new Vector2(0.5f, 0);
            leaveRect.anchoredPosition = new Vector2(0, 40);
            leaveRect.sizeDelta = new Vector2(250, 65);
            leaveBtnObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f, 0.9f);

            GameObject leaveTextObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            leaveTextObj.transform.SetParent(leaveBtnObj.transform, false);
            RectTransform ltRect = leaveTextObj.GetComponent<RectTransform>();
            ltRect.anchorMin = Vector2.zero;
            ltRect.anchorMax = Vector2.one;
            ltRect.offsetMin = Vector2.zero;
            ltRect.offsetMax = Vector2.zero;
            TextMeshProUGUI ltTMP = leaveTextObj.GetComponent<TextMeshProUGUI>();
            ltTMP.text = "离开商店";
            ltTMP.fontSize = 32;
            ltTMP.alignment = TextAlignmentOptions.Center;
            ltTMP.color = Color.white;
        }

        // 创建商品卡片模板（作为隐藏的子对象）
        GameObject storeItemTemplate = CreateStoreItemTemplate(canvasTransform);

        // === 6. 连接StoreManager引用 ===
        StoreManager storeManager = managerObj.GetComponent<StoreManager>();
        SerializedObject so = new SerializedObject(storeManager);
        so.FindProperty("itemContainer").objectReferenceValue = containerObj.transform;
        so.FindProperty("storeItemPrefab").objectReferenceValue = storeItemTemplate;
        so.FindProperty("leaveButton").objectReferenceValue = leaveBtnObj.GetComponent<Button>();
        so.FindProperty("moneyText").objectReferenceValue = moneyObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("feedbackText").objectReferenceValue = feedbackObj.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("完成",
            "Store场景UI配置完成！\n\n已添加:\n• Canvas + EventSystem\n• 商店标题/金粉/反馈文本\n• 道具卡片容器 + 模板\n• 离开按钮\n• StoreManager\n\n请保存场景 (Ctrl+S)", "确定");
    }

    /// <summary>
    /// 创建Canvas
    /// </summary>
    private static GameObject CreateCanvas()
    {
        GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Debug.Log("[StoreSetup] 已创建Canvas");
        return canvasObj;
    }

    /// <summary>
    /// 创建商品卡片模板
    /// </summary>
    private static GameObject CreateStoreItemTemplate(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("StoreItemTemplate");
        if (existing != null) return existing.gameObject;

        GameObject cardObj = new GameObject("StoreItemTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(canvasTransform, false);

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.color = new Color(0.15f, 0.12f, 0.18f, 0.95f);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(280, 330);

        StoreItemUI storeItem = cardObj.AddComponent<StoreItemUI>();

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
        GameObject nameObj = CreateTMPChild(cardObj.transform, "Name", "道具名称", 28,
            new Vector2(0, 0.78f), new Vector2(1, 0.95f), new Vector2(0.5f, 0.5f));

        // 稀有度
        GameObject rarityObj = CreateTMPChild(cardObj.transform, "Rarity", "普通", 20,
            new Vector2(0, 0.68f), new Vector2(1, 0.78f), new Vector2(0.5f, 0.5f));

        // 描述
        GameObject descObj = CreateTMPChild(cardObj.transform, "Description", "道具描述", 20,
            new Vector2(0, 0.3f), new Vector2(1, 0.68f), new Vector2(0.5f, 0.5f));

        // 价格
        GameObject priceObj = CreateTMPChild(cardObj.transform, "Price", "50 金粉", 24,
            new Vector2(0, 0.17f), new Vector2(1, 0.3f), new Vector2(0.5f, 0.5f));
        priceObj.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.85f, 0.3f); // 金色

        // 购买按钮
        GameObject buyBtnObj = new GameObject("BuyButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buyBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform buyRect = buyBtnObj.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.1f, 0.02f);
        buyRect.anchorMax = new Vector2(0.9f, 0.15f);
        buyRect.offsetMin = Vector2.zero;
        buyRect.offsetMax = Vector2.zero;
        buyBtnObj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 0.9f);

        GameObject buyTextObj = CreateTMPChild(buyBtnObj.transform, "BuyText", "购买", 24,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

        // 连接引用
        SerializedObject so = new SerializedObject(storeItem);
        so.FindProperty("nameText").objectReferenceValue = nameObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("descriptionText").objectReferenceValue = descObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("priceText").objectReferenceValue = priceObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("rarityText").objectReferenceValue = rarityObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("buyButton").objectReferenceValue = buyBtnObj.GetComponent<Button>();
        so.FindProperty("buyButtonText").objectReferenceValue = buyTextObj.GetComponent<TextMeshProUGUI>();
        so.FindProperty("borderImage").objectReferenceValue = borderImg;
        so.ApplyModifiedProperties();

        cardObj.SetActive(false); // 模板隐藏
        return cardObj;
    }

    #region Helpers

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void SetupTMPElement(GameObject obj, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        EnsureComponents(obj, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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
    }

    private static GameObject CreateTMPChild(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return obj;
    }

    private static void EnsureComponents(GameObject obj, params System.Type[] types)
    {
        foreach (var type in types)
        {
            if (obj.GetComponent(type) == null)
                obj.AddComponent(type);
        }
    }

    #endregion
}
