using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 可拖动的点（Dot）- 代表各颜色点
/// </summary>
public class Dot : MonoBehaviour, IPointerClickHandler
{
    [System.Serializable]
    public class DotDropEvent : UnityEvent<Dot, Dice> {}

    [Header("Dot Configuration")]
    [SerializeField]
    private DiceColor dotType = DiceColor.Red;

    [Header("Dot Sprites")]
    [SerializeField]
    private Sprite redDotSprite;
    [SerializeField]
    private Sprite yellowDotSprite;
    [SerializeField]
    private Sprite blueDotSprite;
    [SerializeField]
    private Sprite orangeDotSprite;
    [SerializeField]
    private Sprite greenDotSprite;
    [SerializeField]
    private Sprite purpleDotSprite;
    [SerializeField]
    private Sprite blackDotSprite;



    [Header("Events")]
    [SerializeField]
    private DotDropEvent onDroppedOnDice = new DotDropEvent();

    public event Action<Dot, Dice> DroppedOnDice;

    private CanvasGroup canvasGroup;
    private Image dotImage;

    private RectTransform rectTransform;
    private bool hasAppliedEffect;
    private bool isSelected;

    private static Dot selectedDot;

    public DiceColor DotType => dotType;
    public bool HasAppliedEffect => hasAppliedEffect;
    public DotDropEvent OnDroppedOnDice => onDroppedOnDice;
    public static bool HasSelectedDot => selectedDot != null;
    public static Dot SelectedDot => selectedDot;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        dotImage = GetComponent<Image>();

        // 检查并处理可能干扰颜色的组件
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
        }

        // 检查是否有Animator可能在修改颜色
        Animator anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController == null)
        {
            anim.enabled = false;
        }
    }

    private void Start()
    {
        // 根据DotType设置颜色
        UpdateDotVisual();
    }

    private void UpdateDotVisual()
    {
        if (dotImage == null) return;

        Sprite targetSprite = null;
        switch (dotType)
        {
            case DiceColor.Red:
                targetSprite = redDotSprite;
                break;
            case DiceColor.Yellow:
                targetSprite = yellowDotSprite;
                break;
            case DiceColor.Blue:
                targetSprite = blueDotSprite;
                break;
            case DiceColor.Orange:
                targetSprite = orangeDotSprite;
                break;
            case DiceColor.Green:
                targetSprite = greenDotSprite;
                break;
            case DiceColor.Purple:
                targetSprite = purpleDotSprite;
                break;
            case DiceColor.Black:
                targetSprite = blackDotSprite;
                break;
        }

        dotImage.sprite = targetSprite;
    }



    public void OnPointerClick(PointerEventData eventData)
    {
        if (InteractionManager.Instance != null && !InteractionManager.Instance.CanInteract())
            return;

        if (selectedDot != null && selectedDot != this)
        {
            selectedDot.Deselect();
        }

        if (isSelected)
        {
            Deselect();
        }
        else
        {
            Select();
        }
    }

    /// <summary>
    /// 设置Dot类型（可用于动态生成Dot）
    /// </summary>
    public void SetDotType(DiceColor type)
    {
        dotType = type;
        UpdateDotVisual();
    }

    /// <summary>
    /// 记录点数效果已生效，避免重复应用
    /// </summary>
    public void MarkEffectApplied()
    {
        hasAppliedEffect = true;
    }

    public static bool TryConsumeSelected(Dice dice)
    {
        var dotRef = selectedDot;
        if (dotRef == null || dice == null)
            return false;

        // 交由 InteractionManager 处理Dot作用逻辑，避免骰子选中动画与伤害计算。
        bool applied = InteractionManager.Instance != null && InteractionManager.Instance.ApplyDotToDice(dotRef, dice);

        if (applied)
        {
            dotRef.InvokeDrop(dice);

            // 监听回调可能在事件里销毁 Dot，需要逐步判空。
            if (dotRef != null) dotRef.Deselect();
            if (dotRef != null) dotRef.DestroySelf();

            // 确保静态引用被清理
            selectedDot = null;
        }

        return applied;
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void Select()
    {
        isSelected = true;
        selectedDot = this;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.9f;
        }
    }

    private void Deselect()
    {
        isSelected = false;
        if (selectedDot == this)
        {
            selectedDot = null;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    private void InvokeDrop(Dice dice)
    {
        onDroppedOnDice?.Invoke(this, dice);
        DroppedOnDice?.Invoke(this, dice);
    }


}
