using UnityEngine;
using TMPro;

/// <summary>
/// 敌人状态效果UI - 显示敌人身上的流血等负面状态层数
/// 挂载在敌人血条旁的 UI 元素上
/// </summary>
public class EnemyStatusUI : MonoBehaviour
{
    [Tooltip("流血层数文本")]
    [SerializeField] private TextMeshProUGUI bleedingText;

    [Tooltip("流血图标")]
    [SerializeField] private GameObject bleedingIcon;

    /// <summary>
    /// 更新流血层数显示
    /// </summary>
    public void UpdateBleeding(int stacks)
    {
        bool show = stacks > 0;

        if (bleedingIcon != null)
            bleedingIcon.SetActive(show);

        if (bleedingText != null)
        {
            bleedingText.gameObject.SetActive(show);
            if (show)
            {
                bleedingText.text = $"流血 {stacks}";
                bleedingText.color = new Color(0.8f, 0.1f, 0.1f);
            }
        }
    }

    /// <summary>
    /// 从StatusEffectManager自动刷新
    /// </summary>
    public void RefreshFromManager()
    {
        int stacks = 0;
        if (StatusEffectManager.Instance != null)
            stacks = StatusEffectManager.Instance.EnemyBleedingStacks;
        UpdateBleeding(stacks);
    }
}
