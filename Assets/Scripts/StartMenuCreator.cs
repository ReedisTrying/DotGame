using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 启动界面创建工具 - 在Unity编辑器中快速创建启动界面UI
/// 使用方法：将此脚本放在Editor文件夹中，然后在菜单栏选择 Tools > Create Start Menu UI
/// </summary>
public class StartMenuCreator : EditorWindow
{
	[MenuItem("Tools/Create Start Menu UI")]
	public static void CreateStartMenuUI()
	{
		// 创建Canvas
		GameObject canvasObj = new GameObject("StartMenuCanvas");
		Canvas canvas = canvasObj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		
		CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		
		canvasObj.AddComponent<GraphicRaycaster>();
		
		// 创建标题
		CreateText(canvasObj.transform, "TitleText", "Dot - 启动界面", new Vector2(0, 300), new Vector2(800, 100), 72, TextAlignmentOptions.Center);
		
		// 创建骰子配置组面板
		GameObject dicePanel = CreatePanel(canvasObj.transform, "DiceSetPanel", new Vector2(-300, 0), new Vector2(500, 600));
		CreateText(dicePanel.transform, "Label", "选择骰子配置组", new Vector2(0, 200), new Vector2(450, 60), 36, TextAlignmentOptions.Center);
		
		GameObject picassoBtn = CreateButton(dicePanel.transform, "PicassoButton", "毕加索\n(立体主义·红蓝绿)", new Vector2(0, 80), new Vector2(400, 80));
		GameObject matisseBtn = CreateButton(dicePanel.transform, "MatisseButton", "马蒂斯\n(野兽派·红绿)", new Vector2(0, -20), new Vector2(400, 80));
		GameObject monetBtn = CreateButton(dicePanel.transform, "MonetButton", "莫奈\n(印象派·蓝绿)", new Vector2(0, -120), new Vector2(400, 80));
		
		// 创建难度面板
		GameObject diffPanel = CreatePanel(canvasObj.transform, "DifficultyPanel", new Vector2(300, 0), new Vector2(500, 600));
		CreateText(diffPanel.transform, "Label", "选择难度", new Vector2(0, 200), new Vector2(450, 60), 36, TextAlignmentOptions.Center);
		
		GameObject easyBtn = CreateButton(diffPanel.transform, "EasyButton", "简单\n(怪物伤害 x1.0)", new Vector2(0, 80), new Vector2(350, 80));
		GameObject normalBtn = CreateButton(diffPanel.transform, "NormalButton", "普通\n(怪物伤害 x2.0)", new Vector2(0, -20), new Vector2(350, 80));
		GameObject hardBtn = CreateButton(diffPanel.transform, "HardButton", "困难\n(怪物伤害 x3.0)", new Vector2(0, -120), new Vector2(350, 80));
		
		// 创建选择信息文本
		GameObject infoText = CreateText(canvasObj.transform, "SelectedInfoText", "当前选择：毕加索（立体主义·红蓝绿） - 简单", 
			new Vector2(0, -250), new Vector2(1200, 60), 32, TextAlignmentOptions.Center);
		
		// 创建开始游戏按钮
		GameObject startBtn = CreateButton(canvasObj.transform, "StartGameButton", "开始游戏", new Vector2(-180, -350), new Vector2(300, 100));
		var startBtnText = startBtn.GetComponentInChildren<TextMeshProUGUI>();
		if (startBtnText != null)
			startBtnText.fontSize = 48;

		// 创建继续游戏按钮
		GameObject continueBtn = CreateButton(canvasObj.transform, "ContinueButton", "继续游戏", new Vector2(180, -350), new Vector2(300, 100));
		var continueBtnText = continueBtn.GetComponentInChildren<TextMeshProUGUI>();
		if (continueBtnText != null)
			continueBtnText.fontSize = 48;
		
		// 创建Manager对象
		GameObject managerObj = new GameObject("StartMenuManager");
		managerObj.transform.SetParent(canvasObj.transform);
		StartMenuManager manager = managerObj.AddComponent<StartMenuManager>();
		
		// 使用反射设置私有字段
		var type = typeof(StartMenuManager);
		SetField(manager, type, "picassoButton", picassoBtn.GetComponent<Button>());
		SetField(manager, type, "matisseButton", matisseBtn.GetComponent<Button>());
		SetField(manager, type, "monetButton", monetBtn.GetComponent<Button>());
		SetField(manager, type, "easyButton", easyBtn.GetComponent<Button>());
		SetField(manager, type, "normalButton", normalBtn.GetComponent<Button>());
		SetField(manager, type, "hardButton", hardBtn.GetComponent<Button>());
		SetField(manager, type, "startGameButton", startBtn.GetComponent<Button>());
		SetField(manager, type, "continueButton", continueBtn.GetComponent<Button>());
		
		SetField(manager, type, "picassoText", picassoBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "matisseText", matisseBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "monetText", monetBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "easyText", easyBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "normalText", normalBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "hardText", hardBtn.GetComponentInChildren<TextMeshProUGUI>());
		SetField(manager, type, "selectedInfoText", infoText.GetComponent<TextMeshProUGUI>());
		
		SetField(manager, type, "gameSceneName", "SampleScene");
		SetField(manager, type, "normalColor", Color.white);
		SetField(manager, type, "selectedColor", Color.yellow);
		
		// 创建EventSystem（如果不存在）
		if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
		{
			GameObject eventSystem = new GameObject("EventSystem");
			eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
			eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
		}
		
		Selection.activeGameObject = canvasObj;
		Debug.Log("启动界面UI创建完成！");
	}
	
	private static void SetField(object obj, System.Type type, string fieldName, object value)
	{
		var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (field != null)
			field.SetValue(obj, value);
	}
	
	private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
	{
		GameObject panel = new GameObject(name);
		panel.transform.SetParent(parent);
		
		RectTransform rect = panel.AddComponent<RectTransform>();
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		
		Image image = panel.AddComponent<Image>();
		image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
		
		return panel;
	}
	
	private static GameObject CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
	{
		GameObject button = new GameObject(name);
		button.transform.SetParent(parent);
		
		RectTransform rect = button.AddComponent<RectTransform>();
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		
		Image image = button.AddComponent<Image>();
		image.color = new Color(0.4f, 0.4f, 0.4f, 1f);
		
		Button btn = button.AddComponent<Button>();
		
		// 创建文本子对象
		GameObject textObj = new GameObject("Text (TMP)");
		textObj.transform.SetParent(button.transform);
		
		RectTransform textRect = textObj.AddComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.sizeDelta = Vector2.zero;
		textRect.anchoredPosition = Vector2.zero;
		
		TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.fontSize = 28;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.color = Color.white;
		
		return button;
	}
	
	private static GameObject CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject textObj = new GameObject(name);
		textObj.transform.SetParent(parent);
		
		RectTransform rect = textObj.AddComponent<RectTransform>();
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		
		TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.fontSize = fontSize;
		tmp.alignment = alignment;
		tmp.color = Color.white;
		
		return textObj;
	}
}
#endif
