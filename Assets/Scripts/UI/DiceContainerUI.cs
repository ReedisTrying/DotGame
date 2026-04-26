using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 骰子展示容器 - 用于 DiceHexagonDisplay 布局
/// 负责单个骰子的6面围绕显示
/// </summary>
public class DiceContainerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("中心旋转轴（必须有，否则会尝试自动创建）")]
    public Transform pivot;
    
    [Tooltip("用于显示整个骰子背景的图像（可选）")]
    public Image backgroundImage;

    [Tooltip("用于显示的文本组件（可选）")]
    public TextMeshProUGUI label;

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }
}