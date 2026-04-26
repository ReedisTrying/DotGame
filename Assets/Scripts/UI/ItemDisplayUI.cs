using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏UI容器 - 在玩家信息栏旁显示所有持有道具的图标
/// 挂载在一个带有 HorizontalLayoutGroup 的 UI 容器上
/// </summary>
public class ItemDisplayUI : MonoBehaviour
{
    [SerializeField] private GameObject itemSlotPrefab;
    [SerializeField] private Transform slotContainer;

    private List<ItemSlotUI> activeSlots = new List<ItemSlotUI>();

    /// <summary>
    /// 刷新道具显示（从ItemManager读取当前道具）
    /// </summary>
    public void Refresh()
    {
        // 清除旧槽位
        foreach (var slot in activeSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        activeSlots.Clear();

        if (ItemManager.Instance == null) return;

        Transform parent = slotContainer != null ? slotContainer : transform;

        foreach (var item in ItemManager.Instance.OwnedItems)
        {
            GameObject slotObj;
            if (itemSlotPrefab != null)
            {
                slotObj = Instantiate(itemSlotPrefab, parent);
            }
            else
            {
                slotObj = CreateDefaultSlot(parent);
            }

            var slotUI = slotObj.GetComponent<ItemSlotUI>();
            if (slotUI == null)
                slotUI = slotObj.AddComponent<ItemSlotUI>();

            slotUI.SetItem(item);
            activeSlots.Add(slotUI);
        }
    }

    /// <summary>
    /// 无预制体时创建默认道具槽
    /// </summary>
    private GameObject CreateDefaultSlot(Transform parent)
    {
        var slotObj = new GameObject("ItemSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ItemSlotUI));
        slotObj.transform.SetParent(parent, false);

        var rect = slotObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40, 40);

        return slotObj;
    }
}
