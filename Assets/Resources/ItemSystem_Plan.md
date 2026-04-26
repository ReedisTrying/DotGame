# 道具系统设计计划 (Item System Plan)

## 状态: ✅ Phase 1-8 全部完成 (All Core Systems Done)

---

## 一、需求概览

### 道具列表
| 道具名 | 类型 | 效果 | 价格 | 稀有度 |
|--------|------|------|------|--------|
| 龙血红 | 被动道具 | 你造成的额外伤害提升100% | 50 | 普通 |
| 红蛆 | 被动道具 | 对敌人造成额外伤害时，对相邻敌人造成本次伤害10%的额外伤害 | 80 | 稀有 |
| 庞贝红 | 被动道具 | 牌型中每包含一个红色骰子，给敌人附加10层流血 | 100 | 史诗 |
| 朱砂 | 被动道具 | 敌人每携带1层负面状态，额外造成本次伤害1%的额外伤害 | 60 | 普通 |
| 酒红 | 被动道具 | 造成额外伤害后，回复额外伤害30%的血量 | 70 | 稀有 |
| 铭刻-血性呼唤 | 铭刻(面效果) | 若牌型包含该面，额外伤害×该面点数 | - | - |
| 铭刻-地狱火 | 铭刻(面效果) | 若牌型包含该面，流血效果+50%，且本次流血推迟1回合移除 | - | - |

### 需要新增的系统
1. **道具系统** - 道具数据结构、背包管理、效果触发
2. **流血状态系统** - 回合开始减HP，回合结束移除层数
3. **敌人邻接系统** - 红蛆需要知道敌人的相邻关系（当前无此系统）
4. **铭刻系统** - 骰面上的特殊铭刻效果

---

## 二、现有代码分析

### 结算流程 (BattleManager.ResolutionPhase)
1. HandEvaluator.Evaluate() → 计算牌型、倍率、各色效果
2. 应用暴击(黄)
3. 应用连击(橙)
4. 应用红色额外伤害 (RedBonusDamage = redCount × 50)
5. 应用黑色真实伤害
6. 应用绿色回血
7. 计算敌人伤害 → 应用紫色闪避 → 应用蓝色护盾
8. 破碎黑色骰子

### 关键数据结构
- `EvaluationResult`: 包含 TotalDamage, RedBonusDamage, 各种效果值
- `RuntimeDice`: 骰子运行时数据，含6面(DiceFace)
- `DiceFace`: value, color, ForgeCoating
- `DiceColor`: Red/Yellow/Blue/Orange/Green/Purple/Black/None
- `ForgeCoatingType`: 6种镀层

### 缺失系统
- ✅ 道具/背包系统 → ItemManager.cs
- ✅ 持续状态效果系统（流血等） → StatusEffectManager.cs + EnemyInstance
- ✅ 敌人邻接关系 → EnemyInstance.GetAdjacentSlotIndices()
- ✅ 铭刻机制 → ForgeCoatingType扩展 (BloodCall, Hellfire)

---

## 三、实现计划 (待确认)

### Phase 1: 基础架构 ✅
- [x] 道具数据结构 → `ItemType` 枚举 (GameData.cs)
- [x] 道具背包管理 → `ItemManager.cs` (Systems/)
- [x] 道具效果枚举/接口
- [x] 状态效果系统 → `StatusEffectManager.cs` (Systems/)

### Phase 2: 结算流程集成 ✅
- [x] 在 BattleManager.ResolutionPhase 中插入道具触发点（链式处理）
- [x] 修改 HandEvaluator 支持铭刻效果（BloodCall, Hellfire检测）
- [x] 实现流血状态的回合开始/结束逻辑

### Phase 3: 具体道具实现 ✅
- [x] 龙血红 - 额外伤害×2
- [x] 红蛆 - 邻接溅射（需多敌人系统 → 已实现）
- [x] 庞贝红 - 流血附加
- [x] 朱砂 - 负面状态加伤
- [x] 酒红 - 额外伤害吸血
- [x] 铭刻-血性呼唤
- [x] 铭刻-地狱火

### Phase 4: 存档兼容 ✅
- [x] SaveData 增加 ownedItems 字段
- [x] SaveManager 增加 SaveItems/LoadItems
- [x] BattleManager.SetupPhase 加载道具

### Phase 5: UI 实现 ✅
- [x] ItemSlotUI.cs - 单个道具图标（支持tooltip悬浮/点击）
- [x] ItemDisplayUI.cs - 道具栏容器（自动读取ItemManager生成槽位）
- [x] EnemyStatusUI.cs - 敌人流血层数显示
- [x] GameUIManager 集成（RefreshItemDisplay/RefreshEnemyStatus）
- [x] BattleManager.UpdateAllUI 调用刷新

### Phase 6: 多敌人系统 ✅
- [x] EnemyInstance.cs - 单敌人数据容器（HP/流血/手牌/邻接）
- [x] BattleManager重构 - GameState新增TargetSelection/EnemyAttack
- [x] 多次出牌机制 - PlayerTurnPhase循环（出牌→选目标→结算→继续）
- [x] ResolveSinglePlay - 对指定敌人的完整道具链结算
- [x] EnemyAttackPhase - 所有存活敌人依次攻击
- [x] 辅助方法 - AreAllEnemiesDead/GetFirstAliveEnemyIndex/RollAllEnemyDice等
- [x] EnemySlotUI.cs - 单敌人槽位（可点击选目标）
- [x] EnemyPanelUI.cs - 多敌人面板容器
- [x] GameUIManager集成 - 出牌按钮/敌人面板/选目标高亮

### Phase 7: 道具获取系统 ✅
- [x] ItemCatalog.cs - 道具目录（价格/稀有度/权重/加权随机）
- [x] StoreManager.cs - 商店场景管理（3个随机道具/金粉购买）
- [x] StoreItemUI.cs - 商店道具卡片
- [x] BattleRewardUI.cs - 战斗奖励选择（3选1或跳过+20金粉）
- [x] RewardItemUI.cs - 奖励道具卡片
- [x] BattleManager集成 - 胜利后先显示奖励再跳转
- [x] EffectType.GainItem - 事件系统获取道具（intValue=ItemType枚举值）
- [x] EventManager.ApplyEffect处理GainItem

### Phase 8: 待做（场景配置）
- [ ] Store场景搭建（添加StoreManager+道具卡片预制体）
- [ ] Battle场景添加BattleRewardUI面板和RewardItem预制体
- [ ] Battle场景添加敌人面板（3个EnemySlotUI+EnemyPanelUI）
- [ ] Battle场景添加"出牌"按钮并拖入GameUIManager
- [ ] 创建含GainItem效果的事件SO资产

---

## 四、已确认设计决策

### Q1: 敌人数量与邻接
- **答**: 当前只有单敌人，需要新增多敌人系统
- 红蛆的"相邻敌人"依赖多敌人系统

### Q2: "额外伤害"定义
- **答**: 仅指 RedBonusDamage（红色骰子产生的额外50×N伤害）
- 龙血红、朱砂、酒红、血性呼唤 均只作用于此值

### Q3: 道具获取途径
- **答**: 商店购买 + 事件奖励 + 战斗掉落；先实现效果逻辑

### Q4: 道具叠加
- **答**: 不可叠加，每种只能持有一个

### Q5: 铭刻附着方式
- **答**: 类似ForgeCoating，作为骰面属性（DiceFace的新字段）

## 五、待确认问题
> (继续对话确认)

### 第二轮已确认:
- **多敌人**: 一排3-5个槽位，左右相邻；每个敌人独立HP和骰子；玩家手动选择目标
- **铭刻与镀层**: 铭刻和Forge是同一件事 → 直接扩展ForgeCoatingType枚举
- **触发顺序**: 固定优先级链式触发 (龙血红→朱砂→血性呼唤→红蛆→酒红→庞贝红)
- **流血作用域**: 仅当前战斗，战斗结束清除
- **庞贝红统计**: 所有被打出的红色骰子(activeFaces)，不限牌型内
- **道具UI**: 战斗界面常驻图标栏
- **实现优先级**: 先做单敌人道具(龙血红/庞贝红/朱砂/酒红/铭刻)，后做多敌人+红蛆

### 仍待确认:
- ~~多敌人系统：每个敌人独立HP、独立骰子？~~ ✅ 已确认并实现：3槽位，独立HP/骰子
- ~~伤害分配：玩家伤害打哪个敌人？~~ ✅ 已确认：手动点击选目标
- ~~道具触发链具体顺序~~ ✅ 已确认：龙血红→朱砂→血性呼唤→红蛆→酒红→庞贝红
- ~~铭刻扩展ForgeCoatingType枚举~~ ✅ 已确认并实现

---

## 六、实现文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Scripts/GameData.cs` | 修改 | 新增 ItemType 枚举，扩展 ForgeCoatingType (BloodCall=7, Hellfire=8) |
| `Scripts/Systems/ItemManager.cs` | 新增 | 道具背包管理器（单例，DontDestroyOnLoad） |
| `Scripts/Systems/StatusEffectManager.cs` | 新增 | 流血状态效果管理器 |
| `Scripts/Systems/ItemCatalog.cs` | 新增 | 道具目录（价格/稀有度/权重/加权随机选取） |
| `Scripts/Systems/SaveData.cs` | 修改 | 新增 ownedItems 字段 |
| `Scripts/Systems/SaveManager.cs` | 修改 | 新增 SaveItems/LoadItems 方法 |
| `Scripts/HandEvaluator.cs` | 修改 | EvaluationResult 新增字段，检测铭刻效果 |
| `Scripts/EnemyInstance.cs` | 新增 | 单敌人数据容器（HP/流血/手牌/邻接） |
| `Scripts/BattleManager.cs` | 大改 | 多敌人系统、多次出牌、道具链、敌人攻击阶段、战斗奖励 |
| `Scripts/StoreManager.cs` | 新增 | 商店场景管理器 |
| `Scripts/UI/ItemSlotUI.cs` | 新增 | 单个道具图标+tooltip |
| `Scripts/UI/ItemDisplayUI.cs` | 新增 | 道具栏容器 |
| `Scripts/UI/EnemyStatusUI.cs` | 新增 | 敌人流血层数显示 |
| `Scripts/UI/EnemySlotUI.cs` | 新增 | 单敌人槽位（可点击选目标） |
| `Scripts/UI/EnemyPanelUI.cs` | 新增 | 多敌人面板容器 |
| `Scripts/UI/StoreItemUI.cs` | 新增 | 商店道具卡片 |
| `Scripts/UI/BattleRewardUI.cs` | 新增 | 战斗胜利奖励选择面板 |
| `Scripts/UI/RewardItemUI.cs` | 新增 | 奖励道具卡片 |
| `Scripts/GameUIManager.cs` | 修改 | 增加道具栏/敌人状态/敌人面板/出牌按钮 |
| `Scripts/SO/GameEventSO.cs` | 修改 | EffectType新增GainItem |
| `Scripts/Event/EventManager.cs` | 修改 | ApplyEffect处理GainItem |

## 七、额外伤害链式处理流程

```
RedBonusDamage (redCount × 50)  
  → 龙血红 (×2)  
  → 朱砂 (+current × debuffStacks × 1%)  
  → 血性呼唤铭刻 (×face.value)  
  → 最终RedBonusDamage替换到TotalDamage中  
  → 暴击/连击等正常处理  
  → [对敌人造成伤害]  
  → 酒红 (heal 30% of modifiedRedBonus)  
  → 庞贝红 (redCountInHand × 10层流血, 地狱火×1.5+推迟移除)
```

## 八、设计决策记录

| 决策 | 结论 |
|------|------|
| 额外伤害定义 | 仅 RedBonusDamage |
| 道具叠加 | 不可叠加，每种一个 |
| 铭刻机制 | 扩展 ForgeCoatingType 枚举 |
| 流血作用域 | 仅当前战斗 |
| 链式触发 | 逐步传递放大 |
| 流血独立性 | 流血 DOT 不触发道具链 |
| 庞贝红统计 | 仅牌型内 (handIndices) |
| 实现优先级 | 单敌人道具优先，多敌人后续 |

