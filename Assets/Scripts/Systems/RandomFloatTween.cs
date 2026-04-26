using DG.Tweening;
using UnityEngine;

public class RandomFloatTween : MonoBehaviour
{
    [Header("Target")]
    public bool useLocalPosition = true;

    [Header("Range")]
    [Min(0f)] public float xRange = 0.3f;
    [Min(0f)] public float yRange = 0.2f;
    [Min(0f)] public float zRange = 0f;

    [Header("Timing")]
    [Min(0.01f)] public float minDuration = 0.8f;
    [Min(0.01f)] public float maxDuration = 1.8f;
    public Ease ease = Ease.InOutSine;

    [Header("Behavior")]
    [Min(0f)] public float startDelay = 0f;
    public bool randomStartOffset = true;
    public bool returnToOriginOnDisable = true;

    private Tween moveTween;
    private Vector3 originPosition;

    private void Awake()
    {
        originPosition = GetCurrentPosition();

        if (randomStartOffset)
        {
            Vector3 offset = GetRandomOffset();
            SetCurrentPosition(originPosition + offset);
        }
    }

    private void OnEnable()
    {
        StartFloating();
    }

    private void OnDisable()
    {
        KillTween();

        if (returnToOriginOnDisable)
        {
            SetCurrentPosition(originPosition);
        }
    }

    private void OnDestroy()
    {
        KillTween();
    }

    [ContextMenu("Restart Floating")]
    public void StartFloating()
    {
        KillTween();

        if (maxDuration < minDuration)
        {
            maxDuration = minDuration;
        }

        if (startDelay > 0f)
        {
            DOVirtual.DelayedCall(startDelay, MoveToRandomPoint)
                .SetTarget(this)
                .SetUpdate(false);
            return;
        }

        MoveToRandomPoint();
    }

    private void MoveToRandomPoint()
    {
        Vector3 target = originPosition + GetRandomOffset();
        float duration = Random.Range(minDuration, maxDuration);

        if (useLocalPosition)
        {
            moveTween = transform.DOLocalMove(target, duration);
        }
        else
        {
            moveTween = transform.DOMove(target, duration);
        }

        moveTween
            .SetEase(ease)
            .SetTarget(this)
            .OnComplete(MoveToRandomPoint);
    }

    private Vector3 GetCurrentPosition()
    {
        return useLocalPosition ? transform.localPosition : transform.position;
    }

    private void SetCurrentPosition(Vector3 value)
    {
        if (useLocalPosition)
        {
            transform.localPosition = value;
        }
        else
        {
            transform.position = value;
        }
    }

    private Vector3 GetRandomOffset()
    {
        return new Vector3(
            Random.Range(-xRange, xRange),
            Random.Range(-yRange, yRange),
            Random.Range(-zRange, zRange));
    }

    private void KillTween()
    {
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill();
        }

        DOTween.Kill(this);
    }
}
