using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MetaInfo
{
	public string version;
	public string design_doc;
	public string objective;
}

[Serializable]
public class GameRules
{
	public int hand_size;
	public int total_dice_count;
	public int base_action_points;
	public int player_max_hp;
	public string description;
}

[Serializable]
public class DifficultySetting
{
	public string id;
	public string name;
	public float enemy_damage_multiplier;
}

[Serializable]
public class DifficultySettings
{
	public string current_difficulty;
	public List<DifficultySetting> difficulties;
	
	/// <summary>
	/// 获取当前难度的敌人伤害倍率
	/// </summary>
	public float GetCurrentEnemyDamageMultiplier()
	{
		if (difficulties == null || difficulties.Count == 0)
			return 1.0f; // 默认值
		
		var currentDiff = difficulties.Find(d => d.id == current_difficulty);
		if (currentDiff != null)
			return currentDiff.enemy_damage_multiplier;
		
		// 如果找不到当前难度，返回第一个难度的倍率
		return difficulties[0].enemy_damage_multiplier;
	}
}

[Serializable]
public class ScoreMultipliers
{
	public float high_card;
	public float pair;
	public float two_pair;
	public float three_of_a_kind;
	public float three_pairs;
	public float straight;
	public float flush;
	public float full_house;
	public float two_three_of_a_kind;
	public float four_of_a_kind;
	public float five_of_a_kind;
	public float six_of_a_kind;
	public float straight_flush;
}

[Serializable]
public class ColorMechanicEntry
{
	public string effect;
	public float val;
	public string desc;
	public string visual_tag;
}

[Serializable]
public class ColorMechanics
{
	public ColorMechanicEntry red_beast;
	public ColorMechanicEntry blue_impressionist;
	public ColorMechanicEntry green_surreal;
}

[Serializable]
public class ReactionMechanicEntry
{
	public string name;
	public string effect;
	public float multiplier_bonus;
	public int val;
	public string condition;
}

[Serializable]
public class ReactionMechanics
{
	public ReactionMechanicEntry red_blue_clash;
	public ReactionMechanicEntry red_green_acid;
	public ReactionMechanicEntry blue_green_bloom;
}

[Serializable]
public class MotherDicePool
{
	public string notes;
	public List<DiceConfig> dice;
}

[Serializable]
public class EnemyTemplate
{
	public string name;
	public int hp;
	public List<DiceConfig> dice;
	public string description;
}

[Serializable]
public class DiceFaceConfig
{
	public int value;
	public string color;
	public int forge_points;
}

[Serializable]
public class DiceConfig
{
	public int id;
	public List<DiceFaceConfig> faces;
	public int active_face_index;
}

[Serializable]
public class InitialDiceSet
{
	public string name;
	public List<DiceConfig> dice;
}

[Serializable]
public class InitialDiceSets
{
	public string notes;
	public string current_set;
	public InitialDiceSetsCollection sets;
	
	/// <summary>
	/// 获取当前选中的骰子配置组
	/// </summary>
	public InitialDiceSet GetCurrentSet()
	{
		if (sets == null || string.IsNullOrEmpty(current_set))
			return null;
			
		switch (current_set)
		{
			case "picasso": return sets.picasso;
			case "matisse": return sets.matisse;
			case "monet": return sets.monet;
			case "warrior": return sets.warrior;
			default: return sets.picasso;
		}
	}
	
	/// <summary>
	/// 设置当前骰子配置组
	/// </summary>
	public void SetCurrentSet(string setId)
	{
		current_set = setId;
	}
}

[Serializable]
public class InitialDiceSetsCollection
{
	public InitialDiceSet picasso;
	public InitialDiceSet matisse;
	public InitialDiceSet monet;
	public InitialDiceSet warrior;
}

[Serializable]
public class GameConfig
{
	public MetaInfo meta_info;
	public GameRules game_rules;
	public DifficultySettings difficulty_settings;
	public ScoreMultipliers score_multipliers;
	public ColorMechanics color_mechanics;
	public ReactionMechanics reaction_mechanics;
	public MotherDicePool mother_dice_pool;
	public InitialDiceSets initial_dice_sets;
	public EnemyTemplate enemy_template_m0;
}

public enum DiceColor
{
	None,
	Red,
	Yellow,
	Blue,
	Orange,
	Green,
	Purple,
	Black
}

public enum ForgeCoatingType
{	None = 0,
	Gilded = 1,
	DoubleExposure = 2,
	Fluorescent = 3,
	Impasto = 4,
	Pearlescent = 5,
	Etched = 6,
	// 铭刻类型（与镀层共用槽位）
	BloodCall = 7,    // 血性呼唤：额外伤害×该面点数
	Hellfire = 8      // 地狱火：流血+50%，推迟移除
}

/// <summary>
/// 道具类型枚举
/// </summary>
public enum ItemType
{
	None = 0,
	DragonBloodRed = 1,   // 龙血红：额外伤害+100%
	RedMaggot = 2,        // 红蛆：额外伤害溅射相邻敌人10%
	PompeiiRed = 3,       // 庞贝红：红色骰子附加流血
	Cinnabar = 4,         // 朱砂：敌人负面状态加伤
	WineRed = 5           // 酒红：额外伤害吸血30%
}

[Serializable]
public class DiceFace
{
	public int value;
	public DiceColor color;
	public int forgePoints;

	// 记录原始状态，用于回合结束重置
	public int originalValue;
	public DiceColor originalColor;
	public int originalForgePoints;

	public DiceFace(int value, DiceColor color, int forgePoints = 0)
	{
		this.value = value;
		this.color = color;
		this.forgePoints = NormalizeForgePoints(forgePoints);
		this.originalValue = value;
		this.originalColor = color;
		this.originalForgePoints = this.forgePoints;
	}

	public ForgeCoatingType ForgeCoating => (ForgeCoatingType)forgePoints;

	/// <summary>
	/// 重置为原始状态
	/// </summary>
	public void Reset()
	{
		this.value = originalValue;
		this.color = originalColor;
		this.forgePoints = NormalizeForgePoints(originalForgePoints);
	}

	private int NormalizeForgePoints(int rawForgePoints)
	{
		if (rawForgePoints < (int)ForgeCoatingType.None || rawForgePoints > (int)ForgeCoatingType.Hellfire)
		{
			return (int)ForgeCoatingType.None;
		}

		return rawForgePoints;
	}
}

[Serializable]
public class RuntimeDice
{
	[SerializeField]
	private List<DiceFace> faces = new List<DiceFace>();

	[SerializeField]
	private int activeFaceIndex;

	public bool isSelected = false;

	public IReadOnlyList<DiceFace> Faces => faces;

	public int ActiveFaceIndex
	{
		get => activeFaceIndex;
		set => activeFaceIndex = Mathf.Clamp(value, 0, Mathf.Max(0, faces.Count - 1));
	}

	public DiceFace ActiveFace => faces.Count > 0 && activeFaceIndex >= 0 && activeFaceIndex < faces.Count
		? faces[activeFaceIndex]
		: null;

	/// <summary>
	/// 从配置创建RuntimeDice
	/// </summary>
	public static RuntimeDice FromConfig(DiceConfig config)
	{
		if (config == null) return null;

		RuntimeDice dice = new RuntimeDice();
		dice.activeFaceIndex = config.active_face_index;

		foreach (var faceConfig in config.faces)
		{
			DiceColor color = ParseDiceColor(faceConfig.color);
			dice.faces.Add(new DiceFace(faceConfig.value, color, faceConfig.forge_points));
		}

		return dice;
	}

	/// <summary>
	/// 解析字符串为DiceColor枚举
	/// </summary>
	private static DiceColor ParseDiceColor(string colorStr)
	{
		if (string.IsNullOrEmpty(colorStr)) return DiceColor.None;

		switch (colorStr)
		{
			case "Red": return DiceColor.Red;
			case "Yellow": return DiceColor.Yellow;
			case "Blue": return DiceColor.Blue;
			case "Orange": return DiceColor.Orange;
			case "Green": return DiceColor.Green;
			case "Purple": return DiceColor.Purple;
			case "Black": return DiceColor.Black;
			default: return DiceColor.None;
		}
	}
}


