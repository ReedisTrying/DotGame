using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiceDisplay : MonoBehaviour
{
    [Header("Settings")]
    public GameObject diceBoxPrefab; // The box container for one dice
    public GameObject facePrefab;    // The prefab for displaying a single face
    public Transform container;      // The container for all dice boxes
    public float radius = 100f;      // Radius for positioning faces in a hexagon

    public void DisplayDice(List<RuntimeDice> diceList)
    {
        // Clear existing
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        if (diceList == null) return;

        foreach (var dice in diceList)
        {
            GameObject box = Instantiate(diceBoxPrefab, container);
            SetupDiceBox(box, dice);
        }
    }

    private void SetupDiceBox(GameObject box, RuntimeDice dice)
    {
        if (box == null || facePrefab == null || dice == null || dice.Faces == null || dice.Faces.Count == 0) return;

        // Find the center/pivot where faces will rotate around
        Transform pivot = box.transform.Find("Pivot"); 
        if (pivot == null) pivot = box.transform;

        int count = dice.Faces.Count;
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            DiceFace faceData = null;
            if (DiceManager.Instance != null)
            {
                faceData = DiceManager.Instance.GetFaceDataForRendererIndex(dice, i);
            }
            else
            {
                int fallbackFaceIndex = i < dice.Faces.Count ? i : 0;
                faceData = dice.Faces[fallbackFaceIndex];
            }

            if (faceData == null) continue;
            
            // Calculate position in a circle (or hexagon if 6 faces)
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0);

            GameObject faceObj = Instantiate(facePrefab, pivot);
            faceObj.transform.localPosition = pos;

            // Setup face visuals
            Image img = faceObj.GetComponent<Image>(); // Or SpriteRenderer depending on your prefab
            TextMeshProUGUI txt = faceObj.GetComponentInChildren<TextMeshProUGUI>();

            if (img != null)
            {
                // You might need a way to get sprites from DiceManager or a static helper
                if (DiceManager.Instance != null)
                {
                    img.sprite = DiceManager.Instance.GetSpriteForDice(faceData.color);
                }
            }

            if (txt != null)
            {
                txt.text = faceData.value.ToString();
            }
        }
        
        // Optional: Add a simple rotation script to the pivot
        pivot.gameObject.AddComponent<RotateUI>();
    }
}
