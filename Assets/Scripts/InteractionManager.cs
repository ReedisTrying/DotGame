using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// 交互管理器：负责管理拖拽状态、全局输入锁定以及骰子变动后的逻辑分发。
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Game State")]
    public bool IsInteractionLocked = false;

    [Header("Events")]
    // 当任何一个骰子数值或颜色发生改变时触发（用于更新伤害预览条）
    public UnityEvent OnDiceStateChanged;

    [Header("帧率设置")]
    [Tooltip("目标帧率 (30, 60, 120, 144等)，设为-1表示不限制")]
    [SerializeField]
    private int targetFrameRate = 60;

    [Tooltip("是否启用垂直同步 (VSync)")]
    [SerializeField]
    private bool useVSync = false;

    private int originalFrameRate;
    private int originalVSync;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保留单例对象
            InitializeFrameRateSettings(); // 初始化帧率设置
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 初始化帧率设置
    /// </summary>
    private void InitializeFrameRateSettings()
    {
        // 保存原始设置
        originalFrameRate = Application.targetFrameRate;
        originalVSync = QualitySettings.vSyncCount;
        
        ApplyFrameRateSettings();
    }

    /// <summary>
    /// 应用帧率设置
    /// </summary>
    private void ApplyFrameRateSettings()
    {
        // 设置VSync
        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        
        // 设置目标帧率
        if (targetFrameRate > 0)
        {
            Application.targetFrameRate = targetFrameRate;
        }
        else
        {
            // 如果设置为负数，则不限制帧率
            Application.targetFrameRate = -1;
        }
        
    }

    /// <summary>
    /// 设置新的目标帧率
    /// </summary>
    /// <param name="frameRate">目标帧率，-1表示不限制</param>
    public void SetTargetFrameRate(int frameRate)
    {
        targetFrameRate = frameRate;
        ApplyFrameRateSettings();
    }

    /// <summary>
    /// 启用或禁用VSync
    /// </summary>
    /// <param name="enable">是否启用VSync</param>
    public void SetVSync(bool enable)
    {
        useVSync = enable;
        ApplyFrameRateSettings();
    }

    /// <summary>
    /// 获取当前目标帧率
    /// </summary>
    /// <returns>当前目标帧率</returns>
    public int GetCurrentTargetFrameRate()
    {
        return Application.targetFrameRate;
    }

    /// <summary>
    /// 获取当前VSync状态
    /// </summary>
    /// <returns>当前VSync状态</returns>
    public bool GetCurrentVSyncStatus()
    {
        return QualitySettings.vSyncCount > 0;
    }

    /// <summary>
    /// 重置为原始设置
    /// </summary>
    public void ResetToOriginalSettings()
    {
        Application.targetFrameRate = originalFrameRate;
        QualitySettings.vSyncCount = originalVSync;
        
        Debug.Log($"帧率设置已重置 - 目标帧率: {Application.targetFrameRate}, VSync: {(QualitySettings.vSyncCount > 0 ? "开启" : "关闭")}");
    }

    private void OnDestroy()
    {
        // 在对象销毁时恢复原始设置
        if (Instance == this) // 确保只有当前实例才重置设置
        {
            ResetToOriginalSettings();
        }
    }

    /// <summary>
    /// 当DiceSlot完成了一次修改（点数被消耗）时调用
    /// </summary>
    public void NotifyDiceModified()
    {
        if (IsInteractionLocked) return;

        // 触发全局事件，通知 HandEvaluator 重新计算伤害
        // 例如: HandEvaluator.Instance.EvaluateCurrentHand();
        OnDiceStateChanged?.Invoke();
        
        // Debug.Log("Dice Modified! Recalculating Hand...");
    }

    /// <summary>
    /// 处理Dot作用于骰子：变色并调整点数，不触发选中动画或伤害计算。
    /// </summary>
    /// <returns>是否成功应用</returns>
    public bool ApplyDotToDice(Dot dot, Dice dice)
    {
        if (IsInteractionLocked || dot == null || dice == null)
            return false;

        if (dice.RuntimeData == null || dice.RuntimeData.ActiveFace == null)
            return false;

        var activeFace = dice.RuntimeData.ActiveFace;
        int newValue;
        DiceColor newColor;

        if (dot.DotType == DiceColor.Black)
        {
            // 黑色点：点数减1，颜色不变
            newValue = activeFace.value - 1;
            newColor = activeFace.color;
        }
        else
        {
            // 其他点：点数加1，颜色混合
            newValue = activeFace.value + 1;
            newColor = MixColors(activeFace.color, dot.DotType);
        }

        // 限制点数范围：下限为0，无上限
        newValue = Mathf.Max(0, newValue);

        DiceManager.Instance?.UpdateActiveFace(dice, newColor, newValue);

        NotifyDiceModified();
        return true;
    }

    /// <summary>
    /// 颜色混合规则：
    /// 无色 + N = N
    /// 红 + 黄 = 橙
    /// 红 + 蓝 = 紫
    /// 蓝 + 黄 = 绿
    /// 红 + 黄 + 蓝 = 黑
    /// </summary>
    private DiceColor MixColors(DiceColor current, DiceColor added)
    {
        // 将颜色分解为三原色分量（红、黄、蓝）
        bool hasRed, hasYellow, hasBlue;
        DecomposeColor(current, out hasRed, out hasYellow, out hasBlue);

        bool addR, addY, addB;
        DecomposeColor(added, out addR, out addY, out addB);

        hasRed |= addR;
        hasYellow |= addY;
        hasBlue |= addB;

        return ComposeColor(hasRed, hasYellow, hasBlue);
    }

    private void DecomposeColor(DiceColor color, out bool r, out bool y, out bool b)
    {
        r = false; y = false; b = false;
        switch (color)
        {
            case DiceColor.Red:    r = true; break;
            case DiceColor.Yellow: y = true; break;
            case DiceColor.Blue:   b = true; break;
            case DiceColor.Orange: r = true; y = true; break;
            case DiceColor.Green:  y = true; b = true; break;
            case DiceColor.Purple: r = true; b = true; break;
            case DiceColor.Black:  r = true; y = true; b = true; break;
            // None: all false
        }
    }

    private DiceColor ComposeColor(bool r, bool y, bool b)
    {
        if (r && y && b) return DiceColor.Black;
        if (r && y)      return DiceColor.Orange;
        if (r && b)      return DiceColor.Purple;
        if (y && b)      return DiceColor.Black;  // 特殊规则：绿色融合时变黑
        if (r)           return DiceColor.Red;
        if (y)           return DiceColor.Yellow;
        if (b)           return DiceColor.Blue;
        return DiceColor.None;
    }

    /// <summary>
    /// 检查是否允许拖拽（例如在怪物攻击动画播放时禁止操作）
    /// </summary>
    public bool CanInteract()
    {
        return !IsInteractionLocked;
    }
}