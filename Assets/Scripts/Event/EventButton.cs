using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class EventButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TextMeshProUGUI buttonText;
    private TextMeshProUGUI consequenceText;
    private GameObject consequenceTextObject;
    private GameObject consequenceTextPrefab;
    private Transform consequenceTextParent;
    
    private EventOption myOption;
    private System.Action<EventOption> onClickCallback;

    private void Awake()
    {
        AutoResolveButtonText();
    }

    public void SetConsequencePrefab(GameObject prefab, Transform parent)
    {
        consequenceTextPrefab = prefab;
        consequenceTextParent = parent != null ? parent : transform;
    }

    public void Setup(EventOption option, System.Action<EventOption> callback)
    {
        AutoResolveButtonText();

        myOption = option;
        onClickCallback = callback;
        
        if (buttonText != null)
        {
            buttonText.text = option.buttonText;
        }

        if (consequenceText != null)
        {
            consequenceText.text = "";
            consequenceText.gameObject.SetActive(false);
        }
    }

    public void OnClick()
    {
        // 点击时，通知管理器执行这个选项
        onClickCallback?.Invoke(myOption);
    }

    // (可选) 鼠标悬停显示具体后果
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (myOption == null || string.IsNullOrEmpty(myOption.hoverText))
        {
            return;
        }

        EnsureConsequenceTextInstance();
        if (consequenceText != null)
        {
            consequenceText.text = myOption.hoverText;
            consequenceText.gameObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (consequenceText != null)
        {
            consequenceText.text = "";
            consequenceText.gameObject.SetActive(false);
        }
    }

    private void AutoResolveButtonText()
    {
        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void EnsureConsequenceTextInstance()
    {
        if (consequenceText != null)
        {
            return;
        }

        if (consequenceTextPrefab == null)
        {
            Debug.LogWarning($"{name} 未配置后果文本预制体，无法显示hoverText。");
            return;
        }

        Transform parent = consequenceTextParent != null ? consequenceTextParent : transform;
        consequenceTextObject = Instantiate(consequenceTextPrefab, parent);
        consequenceText = consequenceTextObject.GetComponent<TextMeshProUGUI>();

        if (consequenceText == null)
        {
            consequenceText = consequenceTextObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (consequenceText == null)
        {
            Debug.LogWarning($"{consequenceTextPrefab.name} 预制体中未找到 TextMeshProUGUI 组件。");
            Destroy(consequenceTextObject);
            consequenceTextObject = null;
            return;
        }

        consequenceText.text = "";
        consequenceText.gameObject.SetActive(false);
    }
}