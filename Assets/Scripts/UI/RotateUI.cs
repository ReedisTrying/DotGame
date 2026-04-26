using UnityEngine;
using DG.Tweening;

/// <summary>
/// 简单的UI组件，让物体持续旋转
/// </summary>
public class RotateUI : MonoBehaviour
{
    [Tooltip("每秒旋转的角度")]
    public float speed = 30f;

    private Tween rotateTween;

    private void OnEnable()
    {
        StartRotation();
    }

    private void OnDisable()
    {
        StopRotation();
    }

    private void OnDestroy()
    {
        StopRotation();
    }

    private void StartRotation()
    {
        // 防止重复启动
        if (rotateTween != null && rotateTween.IsActive()) return;

        if (Mathf.Abs(speed) < 0.01f) return;

        // 计算转一圈所需时间
        float duration = 360f / Mathf.Abs(speed);
        // 根据速度正负决定方向
        Vector3 rotation = new Vector3(0, 0, speed > 0 ? 360f : -360f);

        // 使用 DOTween 进行持续旋转
        rotateTween = transform
            .DORotate(rotation, duration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    private void StopRotation()
    {
        if (rotateTween != null)
        {
            rotateTween.Kill();
            rotateTween = null;
        }
    }
}