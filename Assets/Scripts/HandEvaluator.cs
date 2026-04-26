using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 评估结果结构体
/// </summary>
[Serializable]
public struct EvaluationResult
{
	public float TotalDamage;      // 总伤害（含红色额外伤害，不含黑色真实伤害）
	public float BaseDamage;       // 基础伤害（应用倍率前）
	public float ShieldValue;      // 护盾值（蓝色骰子）
	public string HandName;        // 牌型名称
	public float FinalMultiplier;  // 最终倍率
	public List<string> ActiveEffects; // 激活的特殊效果描述
	public int BonusMoney; // 额外金粉奖励（镀层效果）
	public int GeneratedConsumables; // 生成的消耗品数量

	// 新颜色效果字段
	public float RedBonusDamage;     // 红色额外伤害（不含在倍率中）
	public float CritRate;           // 暴击率加成（黄色）
	public float CritDamage;         // 暴击伤害加成（黄色）
	public int ComboHits;            // 连击次数（橙色）
	public float HealAmount;         // 回复血量（绿色）
	public float DodgeChance;        // 闪避概率（紫色）
	public float TrueDamage;         // 真实伤害（黑色）
	public List<int> ShatteredDiceIndices; // 需要破碎的骰子索引（黑色）

	// 铭刻效果字段
	public int BloodCallMultiplier;  // 血性呼唤：额外伤害乘以该面点数（0=未触发）
	public bool HasHellfire;         // 地狱火：流血效果+50%，推迟移除
	public int RedCountInHand;       // 牌型内红色骰子数量（庞贝红用）

	public EvaluationResult(float totalDamage, float baseDamage, float shieldValue, string handName, float finalMultiplier, List<string> activeEffects = null, int bonusMoney = 0, int generatedConsumables = 0)
	{
		TotalDamage = totalDamage;
		BaseDamage = baseDamage;
		ShieldValue = shieldValue;
		HandName = handName;
		FinalMultiplier = finalMultiplier;
		ActiveEffects = activeEffects ?? new List<string>();
		BonusMoney = bonusMoney;
		GeneratedConsumables = generatedConsumables;

		RedBonusDamage = 0f;
		CritRate = 0f;
		CritDamage = 0f;
		ComboHits = 0;
		HealAmount = 0f;
		DodgeChance = 0f;
		TrueDamage = 0f;
		ShatteredDiceIndices = new List<int>();
		BloodCallMultiplier = 0;
		HasHellfire = false;
		RedCountInHand = 0;
	}
}

/// <summary>
/// 手牌评估器 - 计算7个骰子的得分
/// </summary>
public class HandEvaluator
{
	private readonly ScoreMultipliers multipliers;
	private readonly Dictionary<string, float> permanentHandBonuses;

	public HandEvaluator(ScoreMultipliers multipliers, Dictionary<string, float> permanentHandBonuses = null)
	{
		this.multipliers = multipliers;
		this.permanentHandBonuses = permanentHandBonuses ?? new Dictionary<string, float>();
	}

	/// <summary>
	/// 评估手牌
	/// </summary>
	public EvaluationResult Evaluate(List<RuntimeDice> hand)
	{
		if (hand == null || hand.Count == 0)
		{
			return new EvaluationResult(0, 0, 0, "无效手牌", 1.0f);
		}

		// 获取所有被打出的激活面
		List<DiceFace> playedFaces = hand
			.Where(d => d.ActiveFace != null)
			.Select(d => d.ActiveFace)
			.ToList();

		if (playedFaces.Count == 0)
		{
			return new EvaluationResult(0, 0, 0, "无效手牌", 1.0f);
		}

		// 重影镀层：该面的点数与颜色统计2次
		List<DiceFace> activeFaces = new List<DiceFace>(playedFaces.Count * 2);
		foreach (var face in playedFaces)
		{
			activeFaces.Add(face);
			if (face.ForgeCoating == ForgeCoatingType.DoubleExposure)
			{
				activeFaces.Add(face);
			}
		}

		List<string> activeEffects = new List<string>();

		// 获取原始值列表（用于牌型判断）
		List<int> originalValues = activeFaces.Select(f => f.value).ToList();
		// 创建可修改的值列表（用于伤害计算）
		List<int> modifiedValues = new List<int>(originalValues);

		// Step 1 - 识别牌型
		var (handName, baseMultiplier, handIndices) = IdentifyBestHand(activeFaces, originalValues);

		// 应用永久加成到基础倍率
		if (permanentHandBonuses.ContainsKey(handName))
		{
			baseMultiplier += permanentHandBonuses[handName];
		}

		float shieldValue = 0f;
		int bonusMoney = 0;
		int generatedConsumables = 0;

		// 新颜色效果变量
		float redBonusDamage = 0f;
		float critRate = 0f;
		float critDamage = 0f;
		int comboHits = 0;
		float healAmount = 0f;
		float dodgeChance = 0f;
		float trueDamage = 0f;
		List<int> shatteredDiceIndices = new List<int>();

		// 镀层统计
		int gildedCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.Gilded);
		int fluorescentCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.Fluorescent);
		int impastoCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.Impasto);
		int pearlescentCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.Pearlescent);
		int etchedCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.Etched);

		// Step 2 - 统计各颜色骰子数量（所有activated faces）
		int redCount = activeFaces.Count(f => FaceHasColor(f, DiceColor.Red));
		int yellowCount = activeFaces.Count(f => FaceHasColor(f, DiceColor.Yellow));
		int blueCount = activeFaces.Count(f => FaceHasColor(f, DiceColor.Blue));
		int orangeCount = 0;
		int greenCount = 0;
		int purpleCount = 0;
		bool hasBlack = false;

		// 橙、绿、紫只计算牌型中包含的骰子
		foreach (int index in handIndices)
		{
			var face = activeFaces[index];
			if (FaceHasColor(face, DiceColor.Orange)) orangeCount++;
			if (FaceHasColor(face, DiceColor.Green)) greenCount++;
			if (FaceHasColor(face, DiceColor.Purple)) purpleCount++;
			if (FaceHasColor(face, DiceColor.Black)) hasBlack = true;
		}

		// 同时检查所有played faces中的黑色骰子（用于确定破碎）
		for (int i = 0; i < hand.Count; i++)
		{
			if (hand[i].ActiveFace != null && FaceHasColor(hand[i].ActiveFace, DiceColor.Black))
			{
				if (!shatteredDiceIndices.Contains(i))
				{
					shatteredDiceIndices.Add(i);
				}
			}
		}

		// Step 3 - 计算最终倍率（基础倍率 + 荧光加成）
		float finalMultiplier = baseMultiplier;
		if (fluorescentCount > 0)
		{
			float fluorescentBonus = 0.5f * fluorescentCount;
			finalMultiplier += fluorescentBonus;
			activeEffects.Add($"荧光: 最终倍率额外+{fluorescentBonus:F1}");
		}

		// Step 4 - 计算基础伤害（只计算牌型内的有色骰子，无色骰子不计入伤害）
		float baseDamage = 0f;
		foreach (int index in handIndices)
		{
			if (activeFaces[index].color != DiceColor.None)
			{
				baseDamage += modifiedValues[index];
			}
		}

		float totalDamage = Mathf.Ceil(baseDamage * finalMultiplier);

		// Step 5 - 应用各颜色效果

		// 红色：每个红色骰子额外造成50点伤害（不包含在倍率中）
		if (redCount > 0)
		{
			redBonusDamage = redCount * 50f;
			totalDamage += redBonusDamage;
			activeEffects.Add($"红色-烈焰: {redCount}个红色骰子，额外伤害+{redBonusDamage:F0}");
		}

		// 黄色：每个黄色骰子暴击率和暴击伤害+10%
		if (yellowCount > 0)
		{
			critRate = yellowCount * 0.10f;
			critDamage = yellowCount * 0.10f;
			activeEffects.Add($"黄色-雷光: {yellowCount}个黄色骰子，暴击率+{critRate * 100:F0}%，暴击伤害+{critDamage * 100:F0}%");
		}

		// 蓝色：每个蓝色骰子获得30点护盾
		if (blueCount > 0)
		{
			shieldValue = blueCount * 30f;
			activeEffects.Add($"蓝色-冰盾: {blueCount}个蓝色骰子，护盾+{shieldValue:F0}");
		}

		// 橙色：牌型中每包含一个橙色骰子，连击次数+1×倍率次
		if (orangeCount > 0)
		{
			comboHits = orangeCount;
			activeEffects.Add($"橙色-连击: 牌型中{orangeCount}个橙色骰子，连击{comboHits}x{finalMultiplier:F1}次");
		}

		// 绿色：牌型中每包含一个绿色骰子，回复10×倍率点血量
		if (greenCount > 0)
		{
			healAmount = greenCount * 10f * finalMultiplier;
			activeEffects.Add($"绿色-生机: 牌型中{greenCount}个绿色骰子，回复{healAmount:F0}点血量");
		}

		// 紫色：牌型中每包含一个紫色骰子，闪避下次攻击概率+10%
		if (purpleCount > 0)
		{
			dodgeChance = purpleCount * 0.10f;
			activeEffects.Add($"紫色-幻影: 牌型中{purpleCount}个紫色骰子，闪避概率+{dodgeChance * 100:F0}%");
		}

		// 黑色：若包含黑色骰子，造成额外100×倍率点真实伤害，该骰子破碎
		if (hasBlack)
		{
			int blackInHandCount = 0;
			foreach (int index in handIndices)
			{
				if (FaceHasColor(activeFaces[index], DiceColor.Black))
					blackInHandCount++;
			}
			trueDamage = blackInHandCount * 100f * finalMultiplier;
			activeEffects.Add($"黑色-毁灭: {blackInHandCount}个黑色骰子破碎，真实伤害+{trueDamage:F0}");
		}

		// 厚涂镀层：每个被打出的厚涂面额外+50（不参与牌型构成）
		if (impastoCount > 0)
		{
			float impastoDamage = impastoCount * 50f;
			totalDamage += impastoDamage;
			activeEffects.Add($"厚涂: 额外无色伤害+{impastoDamage:F0}");
		}

		// 鎏金镀层
		if (gildedCount > 0)
		{
			int gildedMoney = gildedCount * 30;
			bonusMoney += gildedMoney;
			activeEffects.Add($"鎏金: 获得 {gildedMoney} 金粉");
		}

		// 蚀刻镀层
		if (etchedCount > 0)
		{
			generatedConsumables += etchedCount;
			int etchedFallbackMoney = etchedCount * 10;
			bonusMoney += etchedFallbackMoney;
			activeEffects.Add($"蚀刻: 生成 {etchedCount} 件消耗品（当前折算 {etchedFallbackMoney} 金粉）");
		}

		if (pearlescentCount > 0)
		{
			activeEffects.Add($"珠光: {pearlescentCount} 面视为全色");
		}

		int doubleExposureCount = playedFaces.Count(f => f.ForgeCoating == ForgeCoatingType.DoubleExposure);
		if (doubleExposureCount > 0)
		{
			activeEffects.Add($"重影: {doubleExposureCount} 面点数与颜色双重计入");
		}

		// 铭刻检测：血性呼唤 & 地狱火（在牌型内的骰子面上检测）
		int bloodCallMultiplier = 0;
		bool hasHellfire = false;
		int redCountInHand = 0;

		foreach (int index in handIndices)
		{
			var face = activeFaces[index];
			if (face.ForgeCoating == ForgeCoatingType.BloodCall)
			{
				bloodCallMultiplier += face.value;
				activeEffects.Add($"铭刻-血性呼唤: 额外伤害×{face.value}（点数{face.value}）");
			}
			if (face.ForgeCoating == ForgeCoatingType.Hellfire)
			{
				hasHellfire = true;
				activeEffects.Add($"铭刻-地狱火: 流血效果+50%，推迟移除");
			}
			if (FaceHasColor(face, DiceColor.Red))
			{
				redCountInHand++;
			}
		}

		var result = new EvaluationResult(totalDamage, baseDamage, shieldValue, handName, finalMultiplier, activeEffects, bonusMoney, generatedConsumables);
		result.RedBonusDamage = redBonusDamage;
		result.CritRate = critRate;
		result.CritDamage = critDamage;
		result.ComboHits = comboHits;
		result.HealAmount = healAmount;
		result.DodgeChance = dodgeChance;
		result.TrueDamage = trueDamage;
		result.ShatteredDiceIndices = shatteredDiceIndices;
		result.BloodCallMultiplier = bloodCallMultiplier;
		result.HasHellfire = hasHellfire;
		result.RedCountInHand = redCountInHand;

		return result;
	}

	/// <summary>
	/// 识别最佳牌型并返回参与牌型的骰子索引
	/// </summary>
	private (string handName, float multiplier, List<int> handIndices) IdentifyBestHand(List<DiceFace> faces, List<int> values)
	{
		List<int> indices;

		// 按优先级从高到低检查牌型
		if (TryGetStraightFlush(faces, values, out indices))
			return ("大师杰作", multipliers.straight_flush, indices);

		if (TryGetNOfAKind(values, 6, out indices))
			return ("完美统一", multipliers.six_of_a_kind, indices);

		if (TryGetNOfAKind(values, 5, out indices))
			return ("五重和声", multipliers.five_of_a_kind, indices);

		if (TryGetNOfAKind(values, 4, out indices))
			return ("四重奏", multipliers.four_of_a_kind, indices);

		if (TryGetTwoThreeOfAKind(values, out indices))
			return ("双重三角", multipliers.two_three_of_a_kind, indices);

		if (TryGetFullHouse(values, out indices))
			return ("黄金比例", multipliers.full_house, indices);

		if (TryGetFlush(faces, out indices))
			return ("纯色平涂", multipliers.flush, indices);

		if (TryGetThreePairs(values, out indices))
			return ("三重双影", multipliers.three_pairs, indices);

		if (TryGetStraight(values, out indices))
			return ("完美渐变", multipliers.straight, indices);

		if (TryGetNOfAKind(values, 3, out indices))
			return ("三角构图", multipliers.three_of_a_kind, indices);

		if (TryGetTwoPair(values, out indices))
			return ("双重对比", multipliers.two_pair, indices);

		if (TryGetNOfAKind(values, 2, out indices))
			return ("双重影", multipliers.pair, indices);

		// 高牌：只取最大值的那个
		indices = new List<int> { values.IndexOf(values.Max()) };
		return ("杂乱笔触", multipliers.high_card, indices);
	}

	/// <summary>
	/// 尝试获取N条（N个相同数字），返回参与的骰子索引
	/// </summary>
	private bool TryGetNOfAKind(List<int> values, int n, out List<int> indices)
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

	/// <summary>
	/// 尝试获取双三条，返回参与的骰子索引
	/// </summary>
	private bool TryGetTwoThreeOfAKind(List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		var groups = values.Select((v, i) => new { Value = v, Index = i })
			.GroupBy(x => x.Value)
			.Where(g => g.Count() >= 3)
			.OrderByDescending(g => g.Count())
			.Take(2)
			.ToList();

		if (groups.Count >= 2)
		{
			foreach (var group in groups)
			{
				indices.AddRange(group.Select(x => x.Index));
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// 尝试获取葫芦（三条+一对），返回参与的骰子索引
	/// </summary>
	private bool TryGetFullHouse(List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		var groups = values.Select((v, i) => new { Value = v, Index = i })
			.GroupBy(x => x.Value)
			.OrderByDescending(g => g.Count())
			.ToList();

		if (groups.Count >= 2 && groups[0].Count() >= 3 && groups[1].Count() >= 2)
		{
			indices.AddRange(groups[0].Select(x => x.Index));
			indices.AddRange(groups[1].Take(2).Select(x => x.Index));
			return true;
		}
		return false;
	}

	/// <summary>
	/// 尝试获取三对，返回参与的骰子索引
	/// </summary>
	private bool TryGetThreePairs(List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		var groups = values.Select((v, i) => new { Value = v, Index = i })
			.GroupBy(x => x.Value)
			.Where(g => g.Count() >= 2)
			.OrderByDescending(g => g.Count())
			.Take(3)
			.ToList();

		if (groups.Count >= 3)
		{
			foreach (var group in groups)
			{
				indices.AddRange(group.Take(2).Select(x => x.Index));
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// 尝试获取两对，返回参与的骰子索引
	/// </summary>
	private bool TryGetTwoPair(List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		var groups = values.Select((v, i) => new { Value = v, Index = i })
			.GroupBy(x => x.Value)
			.Where(g => g.Count() >= 2)
			.OrderByDescending(g => g.Count())
			.Take(2)
			.ToList();

		if (groups.Count >= 2)
		{
			foreach (var group in groups)
			{
				indices.AddRange(group.Take(2).Select(x => x.Index));
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// 尝试获取同花（5+个相同颜色），返回参与的骰子索引
	/// </summary>
	private bool TryGetFlush(List<DiceFace> faces, out List<int> indices)
	{
		indices = new List<int>();
		if (faces.Count < 5) return false;

		DiceColor[] targetColors = { DiceColor.Red, DiceColor.Yellow, DiceColor.Blue, DiceColor.Orange, DiceColor.Green, DiceColor.Purple, DiceColor.Black };
		foreach (var targetColor in targetColors)
		{
			var colorGroup = faces.Select((f, i) => new { Face = f, Index = i })
				.Where(x => FaceHasColor(x.Face, targetColor))
				.ToList();

			if (colorGroup.Count >= 5)
			{
				indices = colorGroup.Select(x => x.Index).ToList();
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 尝试获取顺子（5+个连续数字），返回参与的骰子索引
	/// </summary>
	private bool TryGetStraight(List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		if (values.Count < 5) return false;

		// 创建值和索引的映射
		var valueIndexPairs = values.Select((v, i) => new { Value = v, Index = i })
			.OrderBy(x => x.Value)
			.ToList();

		// 查找连续序列
		List<int> currentIndices = new List<int> { valueIndexPairs[0].Index };
		int lastValue = valueIndexPairs[0].Value;

		for (int i = 1; i < valueIndexPairs.Count; i++)
		{
			if (valueIndexPairs[i].Value == lastValue + 1)
			{
				currentIndices.Add(valueIndexPairs[i].Index);
				lastValue = valueIndexPairs[i].Value;

				if (currentIndices.Count >= 5)
				{
					indices = currentIndices;
					return true;
				}
			}
			else if (valueIndexPairs[i].Value != lastValue)
			{
				currentIndices.Clear();
				currentIndices.Add(valueIndexPairs[i].Index);
				lastValue = valueIndexPairs[i].Value;
			}
		}

		return false;
	}

	/// <summary>
	/// 尝试获取同花顺（5+个连续数字且同色），返回参与的骰子索引
	/// </summary>
	private bool TryGetStraightFlush(List<DiceFace> faces, List<int> values, out List<int> indices)
	{
		indices = new List<int>();
		if (faces.Count < 5) return false;

		DiceColor[] targetColors = { DiceColor.Red, DiceColor.Yellow, DiceColor.Blue, DiceColor.Orange, DiceColor.Green, DiceColor.Purple, DiceColor.Black };
		foreach (var targetColor in targetColors)
		{
			var group = faces.Select((face, index) => new { Face = face, Value = values[index], Index = index })
				.Where(item => FaceHasColor(item.Face, targetColor))
				.ToList();

			if (group.Count >= 5)
			{
				var sorted = group.OrderBy(x => x.Value).ToList();
				List<int> currentIndices = new List<int> { sorted[0].Index };
				int lastValue = sorted[0].Value;

				for (int i = 1; i < sorted.Count; i++)
				{
					if (sorted[i].Value == lastValue + 1)
					{
						currentIndices.Add(sorted[i].Index);
						lastValue = sorted[i].Value;

						if (currentIndices.Count >= 5)
						{
							indices = currentIndices;
							return true;
						}
					}
					else if (sorted[i].Value != lastValue)
					{
						currentIndices.Clear();
						currentIndices.Add(sorted[i].Index);
						lastValue = sorted[i].Value;
					}
				}
			}
		}

		return false;
	}

	private bool FaceHasColor(DiceFace face, DiceColor targetColor)
	{
		if (face == null)
		{
			return false;
		}

		if (face.color == targetColor)
		{
			return true;
		}

		if (face.ForgeCoating == ForgeCoatingType.Pearlescent)
		{
			return targetColor == DiceColor.Red || targetColor == DiceColor.Blue || targetColor == DiceColor.Green
				|| targetColor == DiceColor.Yellow || targetColor == DiceColor.Orange || targetColor == DiceColor.Purple;
		}

		return false;
	}
}
