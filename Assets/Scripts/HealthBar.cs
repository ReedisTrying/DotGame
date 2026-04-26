using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Health Bar Objects")]
    [SerializeField]
    private GameObject fillObject;

    [SerializeField]
    private GameObject slotObject;

    private float fillBaseScaleX = 1f;
    private float fillBaseLocalPosX = 0f;
    private float fillBaseWidth = 1f;
    private float slotBaseWidth = 1f;

    private Transform fillTransform;
    private Transform slotTransform;
    private RectTransform fillRect;
    private RectTransform slotRect;
    private readonly Vector3[] cornerBuffer = new Vector3[4];

    private void Awake()
    {
        fillTransform = fillObject != null ? fillObject.transform : null;
        slotTransform = slotObject != null ? slotObject.transform : null;

        fillRect = fillObject != null ? fillObject.GetComponent<RectTransform>() : null;
        slotRect = slotObject != null ? slotObject.GetComponent<RectTransform>() : null;

        if (fillRect != null)
        {
            fillBaseScaleX = fillRect.localScale.x;
            fillBaseWidth = fillRect.rect.width;
        }
        else if (fillTransform != null)
        {
            fillBaseScaleX = fillTransform.localScale.x;
            fillBaseLocalPosX = fillTransform.localPosition.x;
            fillBaseWidth = GetSpriteLocalWidth(fillObject, 1f);
        }

        if (slotRect != null)
        {
            slotBaseWidth = slotRect.rect.width;
        }
        else if (slotTransform != null)
        {
            slotBaseWidth = GetSpriteLocalWidth(slotObject, fillBaseWidth);
        }
    }

    public void UpdateHealth(int current, int max)
    {
        float normalized = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        if (fillObject == null) return;

        float scaleX = fillBaseScaleX * normalized;

        if (fillRect != null)
        {
            var scale = fillRect.localScale;
            scale.x = scaleX;
            fillRect.localScale = scale;

            // 左对齐：如果是槽的子物体，用 pivot 计算左边对齐
            if (slotRect != null && fillRect.parent == slotRect)
            {
                float slotWidth = slotRect.rect.width;
                float desiredWidth = slotWidth * normalized;
                fillRect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0f, desiredWidth);
            }
        }
        else if (fillTransform != null)
        {
            var scale = fillTransform.localScale;
            scale.x = scaleX;
            fillTransform.localScale = scale;

            // 子物体左对齐，假设中心 pivot
            if (slotTransform != null && fillTransform.parent == slotTransform)
            {
                float slotWidth = slotBaseWidth > 0f ? slotBaseWidth : fillBaseWidth;
                float fillWidth = fillBaseWidth * (scale.x / Mathf.Max(0.0001f, fillBaseScaleX));
                float slotLeft = -slotWidth * 0.5f;
                float halfFill = fillWidth * 0.5f;
                Vector3 lp = fillTransform.localPosition;
                lp.x = slotLeft + halfFill; // 左对齐，填充居左并按宽度缩放
                fillTransform.localPosition = lp;
            }
            else
            {
                Vector3 lp = fillTransform.localPosition;
                lp.x = fillBaseLocalPosX;
                fillTransform.localPosition = lp;
            }
        }

        fillObject.SetActive(max > 0);
        if (slotObject != null)
        {
            slotObject.SetActive(max > 0);
        }
    }

    private float GetSpriteLocalWidth(GameObject go, float fallback)
    {
        if (go == null) return fallback;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            return sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
        }

        return fallback;
    }

    // 宽度相等场景：主要通过X缩放，RectTransform子物体使用SetInset保持左对齐
}
