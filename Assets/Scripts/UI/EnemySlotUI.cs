using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 单个敌人槽位UI - 显示HP/流血/名称，可点击选择为攻击目标
/// </summary>
public class EnemySlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI bleedingText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private GameObject selectedIndicator;

    private int slotIndex;
    private bool isAlive = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.8f, 0.3f, 0.1f, 0.9f);
    [SerializeField] private Color deadColor = new Color(0.1f, 0.1f, 0.1f, 0.4f);

    public void Initialize(int index)
    {
        slotIndex = index;
        SetSelected(false);
    }

    public void UpdateDisplay(EnemyInstance enemy)
    {
        if (enemy == null) return;

        isAlive = enemy.IsAlive;

        if (nameText != null)
            nameText.text = enemy.Name;

        if (hpText != null)
        {
            hpText.text = isAlive ? $"{enemy.CurrentHP}/{enemy.MaxHP}" : "已击败";
            hpText.color = isAlive ? Color.white : Color.gray;
        }

        if (healthFillImage != null)
        {
            float ratio = enemy.MaxHP > 0 ? (float)enemy.CurrentHP / enemy.MaxHP : 0f;
            healthFillImage.fillAmount = ratio;
            healthFillImage.color = isAlive ? Color.Lerp(Color.red, Color.green, ratio) : Color.gray;
        }

        if (bleedingText != null)
        {
            bool hasBleeding = enemy.BleedingStacks > 0 && isAlive;
            bleedingText.gameObject.SetActive(hasBleeding);
            if (hasBleeding)
                bleedingText.text = $"流血 {enemy.BleedingStacks}";
        }

        if (backgroundImage != null && !IsSelected())
        {
            backgroundImage.color = isAlive ? normalColor : deadColor;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);

        if (backgroundImage != null)
        {
            if (!isAlive)
                backgroundImage.color = deadColor;
            else
                backgroundImage.color = selected ? selectedColor : normalColor;
        }
    }

    private bool IsSelected()
    {
        return selectedIndicator != null && selectedIndicator.activeSelf;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isAlive) return;

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnEnemyTargetSelected(slotIndex);
        }
    }
}
