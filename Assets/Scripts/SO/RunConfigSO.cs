using UnityEngine;

/// <summary>
/// 运行时配置 - 使用ScriptableObject跨场景传递数据
/// 在Project中创建：右键 → Create → DotAlter → Run Config
/// </summary>
[CreateAssetMenu(fileName = "RunConfig", menuName = "DotAlter/Run Config")]
public class RunConfigSO : ScriptableObject
{
	[Header("本次游戏配置")]
	[Tooltip("当前选择的骰子配置组ID (picasso/matisse/monet)")]
	public string selectedDiceSetId = "picasso";
	
	[Tooltip("当前选择的难度ID (easy/normal/hard)")]
	public string selectedDifficultyId = "easy";
	
	[Header("运行时数据（自动填充）")]
	[Tooltip("难度倍率（从配置文件读取）")]
	public float difficultyMultiplier = 1.0f;
	
	[Tooltip("骰子配置组名称（显示用）")]
	public string diceSetName = "毕加索（立体主义·红蓝绿）";
	
	[Tooltip("难度名称（显示用）")]
	public string difficultyName = "简单";
	
	/// <summary>
	/// 重置为默认配置
	/// </summary>
	public void ResetToDefault()
	{
		selectedDiceSetId = "picasso";
		selectedDifficultyId = "easy";
		difficultyMultiplier = 1.0f;
		diceSetName = "毕加索（立体主义·红蓝绿）";
		difficultyName = "简单";
	}
	
	/// <summary>
	/// 设置骰子配置组
	/// </summary>
	public void SetDiceSet(string setId, string setName)
	{
		selectedDiceSetId = setId;
		diceSetName = setName;
	}
	
	/// <summary>
	/// 设置难度
	/// </summary>
	public void SetDifficulty(string difficultyId, string difficultyDisplayName, float multiplier)
	{
		selectedDifficultyId = difficultyId;
		difficultyName = difficultyDisplayName;
		difficultyMultiplier = multiplier;
	}
}
