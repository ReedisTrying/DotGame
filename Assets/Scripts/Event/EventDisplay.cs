using UnityEngine;
using TMPro;

public class EventDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Transform optionsContainer; // 放按钮的父节点
    [SerializeField] private EventButton[] fixedButtons = new EventButton[3];
    [SerializeField] private GameObject consequenceTextPrefab;
    [SerializeField] private Transform consequenceTextRoot;

    private const int MaxButtonCount = 3;

    private void Awake()
    {
        if (fixedButtons == null || fixedButtons.Length != MaxButtonCount)
        {
            fixedButtons = new EventButton[MaxButtonCount];
        }

        for (int i = 0; i < fixedButtons.Length; i++)
        {
            if (fixedButtons[i] == null && optionsContainer != null && i < optionsContainer.childCount)
            {
                fixedButtons[i] = optionsContainer.GetChild(i).GetComponent<EventButton>();
            }

            if (fixedButtons[i] != null)
            {
                Transform textRoot = consequenceTextRoot != null ? consequenceTextRoot : optionsContainer;
                fixedButtons[i].SetConsequencePrefab(consequenceTextPrefab, textRoot);
            }
        }
    }

    // 初始化界面
    public void ShowEvent(GameEventSO eventData, System.Action<EventOption> onOptionSelected)
    {
        if (eventData == null)
        {
            return;
        }

        // 1. 设置文本
        titleText.text = eventData.title;
        descText.text = eventData.description;

        // 2. 固定复用3个按钮，不再销毁或新建
        for (int i = 0; i < MaxButtonCount; i++)
        {
            if (fixedButtons == null || i >= fixedButtons.Length || fixedButtons[i] == null)
            {
                continue;
            }

            bool shouldShow = eventData.options != null && i < eventData.options.Count;
            fixedButtons[i].gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                fixedButtons[i].Setup(eventData.options[i], onOptionSelected);
            }
        }

        if (eventData.options != null && eventData.options.Count > MaxButtonCount)
        {
            Debug.LogWarning($"事件选项数量({eventData.options.Count})超过{MaxButtonCount}，仅显示前{MaxButtonCount}个。");
        }
    }
}