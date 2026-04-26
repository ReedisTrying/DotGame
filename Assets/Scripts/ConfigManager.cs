using UnityEngine;

/// <summary>
/// 配置管理器 - 加载并管理JSON游戏配置数据
/// 注意：此类不再是单例，只负责加载JSON配置文件
/// 跨场景数据传递请使用RunConfigSO
/// </summary>
public class ConfigManager : MonoBehaviour
{
	public GameConfig Config { get; private set; }

	[SerializeField]
	private string resourcePath = "M0_Data_Config";

	private void Awake()
	{
		LoadConfig();
	}

	public void LoadConfig()
	{
		var jsonAsset = Resources.Load<TextAsset>(resourcePath);

		if (jsonAsset == null)
		{
			Debug.LogError($"[ConfigManager] 无法加载配置文件: Resources/{resourcePath}");
			return;
		}

		Config = JsonUtility.FromJson<GameConfig>(jsonAsset.text);

		if (Config == null)
		{
			Debug.LogError("[ConfigManager] JSON解析失败！");
		}
	}
	
	/// <summary>
	/// 设置当前使用的骰子配置组（向后兼容，不推荐使用，请用RunConfigSO）
	/// </summary>
	public void SetDiceSet(string setId)
	{
		if (Config?.initial_dice_sets != null)
		{
			Config.initial_dice_sets.SetCurrentSet(setId);
		}
	}
	
	/// <summary>
	/// 设置当前难度（向后兼容，不推荐使用，请用RunConfigSO）
	/// </summary>
	public void SetDifficulty(string difficultyId)
	{
		if (Config?.difficulty_settings != null)
		{
			Config.difficulty_settings.current_difficulty = difficultyId;
		}
	}
}
