using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 骰子物理投掷组件（3D版本）
/// 控制骰子的投掷运动和边界反弹
/// </summary>
public class Dice : MonoBehaviour
{
    [Tooltip("最小水平投掷力度X")]
    [SerializeField]
    private float minThrowForceX = 300f;
    
    [Tooltip("最大水平投掷力度X")]
    [SerializeField]
    private float maxThrowForceX = 600f;
    
    [Tooltip("垂直投掷力度Y")]
    [SerializeField]
    private float throwForceY = -40f;

    [Tooltip("最小深度投掷力度Z")]
    [SerializeField]
    private float minThrowForceZ = 300f;
    
    [Tooltip("最大深度投掷力度Z")]
    [SerializeField]
    private float maxThrowForceZ = 600f;
    
    [Tooltip("旋转力矩强度")]
    [SerializeField]
    private float torqueStrength = 200f;
    
    [Tooltip("反弹系数 (0-1)")]
    [SerializeField] 
    private float bounciness = 0.2f;
    
    [Tooltip("摩擦力系数")]
    [SerializeField]
    private float friction = 1.0f;
    
    private Rigidbody rb;
    private Collider col;

    [Header("视觉组件缓存")]
    [SerializeField]
    private List<SpriteRenderer> faceSpriteRenderers = new List<SpriteRenderer>();

    [SerializeField]
    private List<TextMeshPro> faceTextMeshes = new List<TextMeshPro>();

    [Tooltip("选中时的Z轴偏移")]
    [SerializeField]
    private float selectionOffsetZ = 0.2f;

    public RuntimeDice RuntimeData { get; private set; }
    public bool IsInContainer { get; set; }
    public bool IsSelected => isSelected;

    private bool isSelected = false;
    private Tween selectionTween;
    private float originalZ;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        SetupDiceCollider();
        CacheFaceComponents();
    }

    private void SetupDiceCollider()
    {
        if (col == null) col = GetComponent<Collider>();
        if (col != null)
        {
            col.material = CreateDiceMaterial();
        }
    }

    private PhysicsMaterial CreateDiceMaterial()
    {
        PhysicsMaterial mat = new PhysicsMaterial("DiceMaterial");
        mat.bounciness = bounciness;
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounceCombine = PhysicsMaterialCombine.Maximum;
        mat.frictionCombine = PhysicsMaterialCombine.Average;
        return mat;
    }
    
    public void SetGhostMode(bool enabled)
    {
        if (col != null)
        {
            col.enabled = !enabled;
        }

        float alpha = enabled ? 0.5f : 1f;

        foreach (var sr in faceSpriteRenderers)
        {
            if (sr != null)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        foreach (var tmp in faceTextMeshes)
        {
            if (tmp != null)
            {
                Color c = tmp.color;
                c.a = alpha;
                tmp.color = c;
            }
        }
    }

    /// <summary>
    /// 停止骰子运动
    /// </summary>
    public void StopDice()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        IsThrown = false;
    }
    
    /// <summary>
    /// 重置骰子到初始位置
    /// </summary>
    public void ResetPosition(Vector3 position)
    {
        StopDice();
        transform.position = position;
        transform.rotation = Quaternion.identity;
        IsInContainer = false;
        isSelected = false;
    }

    public void SetRuntimeData(RuntimeDice data)
    {
        RuntimeData = data;
    }

    private void OnMouseDown()
    {
        if (BattleManager.Instance != null &&
            BattleManager.Instance.CurrentState == BattleManager.GameState.PlayerTurn &&
            IsInContainer)
        {
            // 如果当前有已选择的Dot，则优先消费Dot并触发后续逻辑
            if (Dot.TryConsumeSelected(this))
            {
                return;
            }

            ToggleSelection();
        }
    }

    private void ToggleSelection()
    {
        bool isAnimating = selectionTween != null && selectionTween.IsActive();
        if (isAnimating) selectionTween.Kill();

        if (!isSelected)
        {
            // 选中
            // 如果正在动画中（意味着正在取消选中回到原位），则保留原有的originalZ，不要覆盖
            if (!isAnimating)
            {
                originalZ = transform.position.z;
            }
            
            isSelected = true;
            if (RuntimeData != null) RuntimeData.isSelected = true;
            selectionTween = transform.DOMoveZ(originalZ + selectionOffsetZ, 0.2f);
            
            if (BattleManager.Instance != null)
                BattleManager.Instance.OnDiceSelected(this);
        }
        else
        {
            // 取消选中
            isSelected = false;
            if (RuntimeData != null) RuntimeData.isSelected = false;
            // 确保返回到记录的原始Z轴位置
            selectionTween = transform.DOMoveZ(originalZ, 0.2f);
            
            if (BattleManager.Instance != null)
                BattleManager.Instance.OnDiceDeselected(this);
        }
    }


    
    /// <summary>
    /// 获取当前是否正在运动
    /// </summary>
    public bool IsThrown { get; set; }

    public float MinThrowForceX => minThrowForceX;
    public float MaxThrowForceX => maxThrowForceX;
    public float MinThrowForceZ => minThrowForceZ;
    public float MaxThrowForceZ => maxThrowForceZ;
    public float ThrowForceY => throwForceY;
    public float TorqueStrength => torqueStrength;

    public IReadOnlyList<SpriteRenderer> FaceSpriteRenderers => faceSpriteRenderers;
    public IReadOnlyList<TextMeshPro> FaceTextMeshes => faceTextMeshes;

    private void CacheFaceComponents()
    {
        if (faceSpriteRenderers == null)
        {
            faceSpriteRenderers = new List<SpriteRenderer>();
        }
        else
        {
            faceSpriteRenderers.RemoveAll(item => item == null);
        }

        if (faceTextMeshes == null)
        {
            faceTextMeshes = new List<TextMeshPro>();
        }
        else
        {
            faceTextMeshes.RemoveAll(item => item == null);
        }

        if (faceSpriteRenderers.Count == 0)
        {
            // 查找所有子物体的SpriteRenderer，排除自身
            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in sprites)
            {
                if (sr.gameObject != gameObject)
                {
                    faceSpriteRenderers.Add(sr);
                }
            }
        }

        if (faceTextMeshes.Count == 0)
        {
            var texts = GetComponentsInChildren<TextMeshPro>(true);
            foreach (var tmp in texts)
            {
                if (tmp.gameObject != gameObject)
                {
                    faceTextMeshes.Add(tmp);
                }
            }
        }

        const int expectedFaceCount = 6;
        if (faceSpriteRenderers.Count != expectedFaceCount)
        {
            Debug.LogWarning($"Dice: 期望找到 {expectedFaceCount} 个面 SpriteRenderer，当前 {faceSpriteRenderers.Count}", this);
        }
        if (faceTextMeshes.Count != expectedFaceCount)
        {
            Debug.LogWarning($"Dice: 期望找到 {expectedFaceCount} 个面 TextMeshPro，当前 {faceTextMeshes.Count}", this);
        }
    }
}
