using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 启动界面管理器 - 管理骰子配置组和难度选择
/// </summary>
public class StartMenuManager : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField]
	private Button picassoButton;
	
	[SerializeField]
	private Button matisseButton;
	
	[SerializeField]
	private Button monetButton;
	
	[SerializeField]
	private Button easyButton;
	
	[SerializeField]
	private Button normalButton;
	
	[SerializeField]
	private Button hardButton;
	
	[SerializeField]
	private Button startGameButton;
	
	[SerializeField]
	private Button continueButton;
	
	[Header("Text References")]
	[SerializeField]
	private TextMeshProUGUI picassoText;
	
	[SerializeField]
	private TextMeshProUGUI matisseText;
	
	[SerializeField]
	private TextMeshProUGUI monetText;
	
	[SerializeField]
	private TextMeshProUGUI easyText;
	
	[SerializeField]
	private TextMeshProUGUI normalText;
	
	[SerializeField]
	private TextMeshProUGUI hardText;
	
	[SerializeField]
	private TextMeshProUGUI selectedInfoText;
	
	[Header("Settings")]
	[SerializeField]
	private Color normalColor = Color.white;
	
	[SerializeField]
	private Color selectedColor = Color.yellow;
	
	[Header("Run Config (ScriptableObject)")]
	[SerializeField]
	[Tooltip("运行时配置容器，拖入RunConfig资产")]
	private RunConfigSO runConfig;
	
	// 当前选择
	private string selectedDiceSet = "picasso";
	private string selectedDifficulty = "easy";
	
	private void Start()
	{
		// 设置按钮监听
		if (picassoButton != null)
			picassoButton.onClick.AddListener(() => SelectDiceSet("picasso"));
		
		if (matisseButton != null)
			matisseButton.onClick.AddListener(() => SelectDiceSet("matisse"));
		
		if (monetButton != null)
			monetButton.onClick.AddListener(() => SelectDiceSet("monet"));
		
		if (easyButton != null)
			easyButton.onClick.AddListener(() => SelectDifficulty("easy"));
		
		if (normalButton != null)
			normalButton.onClick.AddListener(() => SelectDifficulty("normal"));
		
		if (hardButton != null)
			hardButton.onClick.AddListener(() => SelectDifficulty("hard"));
		
		if (startGameButton != null)
			startGameButton.onClick.AddListener(StartGame);
		
		if (continueButton != null)
		{
			if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile())
			{
				continueButton.onClick.AddListener(ContinueGame);
				continueButton.interactable = true;
				continueButton.gameObject.SetActive(true);
			}
			else
			{
				continueButton.interactable = false;
				continueButton.gameObject.SetActive(false);
			}
		}

		// 初始化选择
		UpdateUI();
	}
	
	/// <summary>
	/// 选择骰子配置组
	/// </summary>
	private void SelectDiceSet(string setId)
	{
		selectedDiceSet = setId;
		UpdateUI();
	}
	
	/// <summary>
	/// 选择难度
	/// </summary>
	private void SelectDifficulty(string difficultyId)
	{
		selectedDifficulty = difficultyId;
		UpdateUI();
	}
	
	/// <summary>
	/// 更新UI显示
	/// </summary>
	private void UpdateUI()
	{
		// 更新骰子配置组按钮颜色
		UpdateButtonColor(picassoText, selectedDiceSet == "picasso");
		UpdateButtonColor(matisseText, selectedDiceSet == "matisse");
		UpdateButtonColor(monetText, selectedDiceSet == "monet");
		
		// 更新难度按钮颜色
		UpdateButtonColor(easyText, selectedDifficulty == "easy");
		UpdateButtonColor(normalText, selectedDifficulty == "normal");
		UpdateButtonColor(hardText, selectedDifficulty == "hard");
		
		// 更新信息显示
		if (selectedInfoText != null)
		{
			string setName = GetDiceSetName(selectedDiceSet);
			string diffName = GetDifficultyName(selectedDifficulty);
			selectedInfoText.text = $"当前选择：{setName} - {diffName}";
		}
	}
	
	/// <summary>
	/// 更新按钮文本颜色
	/// </summary>
	private void UpdateButtonColor(TextMeshProUGUI text, bool isSelected)
	{
		if (text != null)
		{
			text.color = isSelected ? selectedColor : normalColor;
		}
	}
	
	/// <summary>
	/// 获取骰子配置组名称
	/// </summary>
	private string GetDiceSetName(string setId)
	{
		switch (setId)
		{
			case "picasso":
				return "毕加索（立体主义·红蓝绿）";
			case "matisse":
				return "马蒂斯（野兽派·红绿）";
			case "monet":
				return "莫奈（印象派·蓝绿）";
			default:
				return "未知";
		}
	}
	
	/// <summary>
	/// 获取难度名称
	/// </summary>
	private string GetDifficultyName(string difficultyId)
	{
		switch (difficultyId)
		{
			case "easy":
				return "简单 (x1.0)";
			case "normal":
				return "普通 (x2.0)";
			case "hard":
				return "困难 (x3.0)";
			default:
				return "未知";
		}
	}
	
	/// <summary>
	/// 继续游戏
	/// </summary>
	private void ContinueGame()
	{
		if (SaveManager.Instance != null && SaveManager.Instance.LoadGame())
		{
			GameSceneManager.LoadMap();
		}
		else
		{
			Debug.LogWarning("无法加载存档或存档不存在。");
			// 如果加载失败，刷新UI状态
			if (continueButton != null)
			{
				continueButton.interactable = false;
				continueButton.gameObject.SetActive(false);
			}
		}
	}

	/// <summary>
	/// 开始游戏
	/// </summary>
	private void StartGame()
	{
		// 写入配置到RunConfigSO
		if (runConfig != null)
		{
			string setName = GetDiceSetName(selectedDiceSet);
			string diffName = GetDifficultyName(selectedDifficulty);
			float multiplier = GetDifficultyMultiplier(selectedDifficulty);
			
			runConfig.SetDiceSet(selectedDiceSet, setName);
			runConfig.SetDifficulty(selectedDifficulty, diffName, multiplier);
			
			Debug.Log($"[StartMenu] 配置已写入RunConfig: 骰子组={selectedDiceSet}, 难度={selectedDifficulty} (x{multiplier})");
		}
		else
		{
			Debug.LogError("[StartMenu] RunConfig未设置！请在Inspector中拖入RunConfig资产。");
		}
		
		// 初始化新存档
		if (SaveManager.Instance != null)
		{
			SaveManager.Instance.NewGame(runConfig);
		}

		GameSceneManager.LoadMap();
	}
	
	/// <summary>
	/// 获取难度倍率
	/// </summary>
	private float GetDifficultyMultiplier(string difficultyId)
	{
		switch (difficultyId)
		{
			case "easy": return 1.0f;
			case "normal": return 2.0f;
			case "hard": return 3.0f;
			default: return 1.0f;
		}
	}
}
