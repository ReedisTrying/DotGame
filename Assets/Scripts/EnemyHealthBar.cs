using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Slider slider;

    [Tooltip("Hide slider when no HP available.")]
    [SerializeField]
    private bool hideWhenEmpty = true;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>();
        }
    }

    public void UpdateHealth(int current, int max)
    {
        if (slider == null) return;

        int safeMax = Mathf.Max(0, max);
        int clampedCurrent = Mathf.Clamp(current, 0, safeMax);

        // Slider expects a positive max value to avoid division issues when normalized internally.
        slider.maxValue = safeMax > 0 ? safeMax : 1;
        slider.value = safeMax > 0 ? clampedCurrent : 0;

        if (hideWhenEmpty)
        {
            bool shouldShow = safeMax > 0;
            if (slider.gameObject.activeSelf != shouldShow)
            {
                slider.gameObject.SetActive(shouldShow);
            }
        }
    }
}
