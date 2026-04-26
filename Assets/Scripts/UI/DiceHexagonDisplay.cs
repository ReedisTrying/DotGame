using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiceHexagonDisplay : MonoBehaviour
{
    [Header("Settings")]
    public float rotateSpeed = 30f;
    public float scale = 1f; // 缩放倍率，用于调整3D物体在UI中的显示大小
    public Vector3 initialRotation = new Vector3(35.3f, 45f, 0f); // 初始旋转角度 (Orthographic Isometric: 35.3, 45, 0)

    [Header("Face Settings")]
    [Tooltip("用于在骰子周围展示的六个面的预制体")]
    public GameObject facePrefab;
    [Tooltip("面距离中心的距离")]
    public float faceDistance = 150f;
    [Tooltip("面的缩放")]
    public float faceScale = 1f;

    [Header("World Generation Settings")]
    [Tooltip("3D 骰子生成的父节点（如果不填则默认生成的物体仍在 UI 之下）")]
    public Transform worldContentParent;
    [Tooltip("渲染 UI 的摄像机（如果是 Overlay 模式可不填）")]
    public Camera uiCamera;
    [Tooltip("渲染 3D 骰子的摄像机（如果不填默认使用 MainCamera）")]
    public Camera worldCamera;
    [Tooltip("3D 骰子距离摄像机的深度")]
    public float zDepth = 5f;
    [Tooltip("生成的 3D 骰子层级")]
    public string targetLayer = "Default";

    [Header("Prefabs")]
    [Tooltip("UI 容器预制体 (Box)")]
    public GameObject diceContainerPrefab;
    
    [Tooltip("3D 骰子预制体 (必须包含 Dice 组件)")]
    public GameObject dice3DPrefab;

    [Header("UI Parent")]
    public Transform contentParent;

    [Header("Debug")]
    [Tooltip("如果没有传入骰子数据，是否使用预制体默认数据进行展示（用于测试）")]
    public bool usePrefabDataIfEmpty = true;

    private void Start()
    {
        // 如果开始时没有任何子物体，且开启了调试模式，则显示默认骰子
        if (contentParent.childCount == 0 && usePrefabDataIfEmpty)
        {
            ShowDiceList(null);
        }
    }

    public void ShowDiceList(List<RuntimeDice> diceList)
    {
        // 1. 清空当前显示 (包括 UI 和 3D 骰子)
        foreach (var pair in spawnedPairs)
        {
            if (pair.dice != null) Destroy(pair.dice.gameObject);
        }
        spawnedPairs.Clear();

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 如果没有数据且处于调试模式，生成一组空的 RuntimeDice 以触发预制体默认展示
        if ((diceList == null || diceList.Count == 0) && usePrefabDataIfEmpty)
        {
            diceList = new List<RuntimeDice>();
            for (int i = 0; i < 6; i++) diceList.Add(null);
        }

        if (diceList == null) return;

        // 2. 为每个骰子生成一个展示容器
        foreach (var diceData in diceList)
        {
            // 允许 diceData 为 null，此时将显示预制体默认状态

            // 实例化 UI 容器 (Box)
            GameObject containerObj = Instantiate(diceContainerPrefab, contentParent);

            // 确保容器有一个挂载 3D 物体的节点（如 Pivot，或者直接挂在容器下）
            Transform pivot = containerObj.transform.Find("Pivot");
            if (pivot == null) pivot = containerObj.transform;

            List<GameObject> generatedFaceObjects = new List<GameObject>();
            
            // 实例化 6 个面围绕容器中心
            if (facePrefab != null)
            {
                var faces = diceData != null ? diceData.Faces : null;
                int faceCount = 6;
                float angleStep = 360f / faceCount;
                float startAngle = 90f; // 从正上方开始 (12点钟方向)

                for (int i = 0; i < faceCount; i++)
                {
                    GameObject faceObj = Instantiate(facePrefab, pivot);
                    generatedFaceObjects.Add(faceObj);
                    faceObj.transform.localScale = Vector3.one * faceScale;

                    float angle = startAngle - (i * angleStep); // 顺时针排列
                    float rad = angle * Mathf.Deg2Rad;
                    Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * faceDistance;
                    
                    RectTransform rt = faceObj.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = pos;
                    }
                    else
                    {
                        faceObj.transform.localPosition = pos;
                    }

                    // 设置数据
                    if (faces != null && faces.Count > 0)
                    {
                        DiceFace faceData = null;
                        if (DiceManager.Instance != null)
                        {
                            faceData = DiceManager.Instance.GetFaceDataForRendererIndex(diceData, i);
                        }
                        else
                        {
                            int fallbackFaceIndex = i < faces.Count ? i : 0;
                            faceData = faces[fallbackFaceIndex];
                        }

                        SetupFaceVisual(faceObj, faceData);
                    }
                }
            }

            // 实例化 3D 骰子
            if (dice3DPrefab != null)
            {
                // 如果未指定 worldContentParent，动态创建一个以确保骰子不挂在 UI 下
                if (worldContentParent == null)
                {
                    var container = GameObject.Find("DiceWorldContainer");
                    if (container == null) container = new GameObject("DiceWorldContainer");
                    worldContentParent = container.transform;
                }
                
                Transform parent = worldContentParent;
                GameObject dice3DObj = Instantiate(dice3DPrefab, parent);
                
                // 总是添加到 pair list 以便跟随 UI
                spawnedPairs.Add(new DicePair { dice = dice3DObj.transform, target = pivot });

                // 彻底移除物理组件，使其成为纯视觉物体
                var rbs = dice3DObj.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in rbs) Destroy(rb);
                
                var cols = dice3DObj.GetComponentsInChildren<Collider>();
                foreach (var col in cols) Destroy(col);
                
                // 初始化骰子视觉
                Dice diceComponent = dice3DObj.GetComponent<Dice>();
                if (diceComponent != null)
                {
                    // 只有当提供了 diceData 时才覆盖视觉，否则保持预制体原样
                    if (diceData != null)
                    {
                        if (DiceManager.Instance != null)
                        {
                            DiceManager.Instance.ApplyRuntimeVisualsToDice(diceComponent, diceData);
                        }
                        else
                        {
                            SetupDiceVisualsFallback(diceComponent, diceData);
                        }
                    }

                    for (int i = 0; i < generatedFaceObjects.Count; i++)
                    {
                        SetupFaceVisualFromDice(generatedFaceObjects[i], diceComponent, i, diceData);
                    }
                }

                // 设置Transform
                //dice3DObj.transform.localPosition = positionOffset; // 已被用户注释
                // 注意：3D物体在Canvas下的缩放通常需要很大，或者根据Canvas模式调整
                dice3DObj.transform.localScale = Vector3.one * scale; 
                dice3DObj.transform.localRotation = Quaternion.Euler(initialRotation);

                // 添加持续旋转
                var rotator = dice3DObj.AddComponent<RotateUI>();
                rotator.speed = rotateSpeed;
                
                // 设置层级
                string layer = targetLayer; 
                SetLayerRecursively(dice3DObj, LayerMask.NameToLayer(layer));
            }
        }
        
        StartCoroutine(UpdateDicePositions());
    }

    private System.Collections.IEnumerator UpdateDicePositions()
    {
        // Wait for UI layout
        yield return new WaitForEndOfFrame();
        
        if (worldCamera == null) worldCamera = Camera.main;

        foreach (var pair in spawnedPairs)
        {
            if (pair.dice != null && pair.target != null && worldCamera != null)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, pair.target.position);
                Vector3 targetScreenPos = new Vector3(screenPoint.x, screenPoint.y, zDepth);
                pair.dice.position = worldCamera.ScreenToWorldPoint(targetScreenPos);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0) return; // Layer invalid
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void SetupFaceVisual(GameObject faceObj, DiceFace faceData)
    {
        if (faceObj == null || faceData == null) return;

        Image img = FindFaceImage(faceObj.transform);
        if (img != null)
        {
            if (DiceManager.Instance != null)
            {
                img.sprite = DiceManager.Instance.GetSpriteForDice(faceData.color);
            }
        }

        TextMeshProUGUI txt = FindFaceText(faceObj.transform);
        if (txt != null)
        {
            txt.text = faceData.value.ToString();
        }
    }

    private void SetupFaceVisualFromDice(GameObject faceObj, Dice dice, int faceIndex, RuntimeDice runtimeDice)
    {
        if (faceObj == null) return;

        Image img = FindFaceImage(faceObj.transform);
        TextMeshProUGUI txt = FindFaceText(faceObj.transform);

        bool hasCopiedFromDice = false;

        if (dice != null)
        {
            int mappedRuntimeFaceIndex = faceIndex;
            if (DiceManager.Instance != null)
            {
                mappedRuntimeFaceIndex = DiceManager.Instance.GetMappedRuntimeFaceIndex(dice, faceIndex);
            }

            var renderers = dice.FaceSpriteRenderers;
            if (img != null && renderers != null && faceIndex >= 0 && faceIndex < renderers.Count && renderers[faceIndex] != null)
            {
                img.sprite = renderers[faceIndex].sprite;
                hasCopiedFromDice = true;
            }

            var texts = dice.FaceTextMeshes;
            if (txt != null && texts != null && faceIndex >= 0 && faceIndex < texts.Count && texts[faceIndex] != null)
            {
                txt.text = texts[faceIndex].text;
                hasCopiedFromDice = true;
            }

            if (!hasCopiedFromDice && runtimeDice != null && runtimeDice.Faces != null && mappedRuntimeFaceIndex >= 0 && mappedRuntimeFaceIndex < runtimeDice.Faces.Count)
            {
                SetupFaceVisual(faceObj, runtimeDice.Faces[mappedRuntimeFaceIndex]);
                hasCopiedFromDice = true;
            }
        }

        if (!hasCopiedFromDice && runtimeDice != null && runtimeDice.Faces != null && runtimeDice.Faces.Count > 0)
        {
            DiceFace fallbackFaceData = null;
            if (DiceManager.Instance != null)
            {
                fallbackFaceData = DiceManager.Instance.GetFaceDataForRendererIndex(runtimeDice, faceIndex);
            }
            else
            {
                int fallbackFaceIndex = faceIndex < runtimeDice.Faces.Count ? faceIndex : 0;
                fallbackFaceData = runtimeDice.Faces[fallbackFaceIndex];
            }

            SetupFaceVisual(faceObj, fallbackFaceData);
        }
    }

    private Image FindFaceImage(Transform root)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("icon") || lowerName.Contains("image") || lowerName.Contains("sprite"))
            {
                Image namedImage = child.GetComponent<Image>();
                if (namedImage != null) return namedImage;
            }
        }
        Debug.LogWarning($"No named Image found in {root.name}, falling back to any Image component.");

        return root.GetComponentInChildren<Image>(true);
    }

    private TextMeshProUGUI FindFaceText(Transform root)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("value") || lowerName.Contains("text") || lowerName.Contains("num"))
            {
                TextMeshProUGUI namedText = child.GetComponent<TextMeshProUGUI>();
                if (namedText != null) return namedText;
            }
        }

        return root.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void SetupDiceVisualsFallback(Dice dice, RuntimeDice runtimeDice)
    {
        if (dice == null || runtimeDice == null) return;

        dice.SetRuntimeData(runtimeDice);

        var faces = runtimeDice.Faces;
        if (faces == null || faces.Count == 0) return;
        var renderers = dice.FaceSpriteRenderers;
        var texts = dice.FaceTextMeshes;

        for (int i = 0; i < renderers.Count; i++)
        {
            DiceFace faceData;
            if (DiceManager.Instance != null)
            {
                faceData = DiceManager.Instance.GetFaceDataForRendererIndex(runtimeDice, i);
            }
            else
            {
                int fallbackFaceIndex = i < faces.Count ? i : 0;
                faceData = faces[fallbackFaceIndex];
            }

            if (faceData == null) continue;
            
            if (renderers[i] != null && DiceManager.Instance != null)
            {
                renderers[i].sprite = DiceManager.Instance.GetSpriteForDice(faceData.color);
            }

            if (i < texts.Count && texts[i] != null)
            {
                texts[i].text = faceData.value.ToString();
            }
        }
    }

    private class DicePair
    {
        public Transform dice;
        public Transform target;
    }
    
    private List<DicePair> spawnedPairs = new List<DicePair>();

}
