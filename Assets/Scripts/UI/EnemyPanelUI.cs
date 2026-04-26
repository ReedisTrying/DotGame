using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 多敌人面板UI - 管理所有敌人槽位的容器
/// 在场景中放置3个EnemySlotUI子物体，并拖入slots数组
/// </summary>
public class EnemyPanelUI : MonoBehaviour
{
    [Tooltip("敌人槽位UI列表（按左→右顺序）")]
    [SerializeField] private List<EnemySlotUI> slots = new List<EnemySlotUI>();

    private int currentSelectedIndex = -1;

    private void Awake()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Initialize(i);
        }
    }

    /// <summary>
    /// 刷新所有敌人槽位显示
    /// </summary>
    public void Refresh(List<EnemyInstance> enemies)
    {
        if (enemies == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i < enemies.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].UpdateDisplay(enemies[i]);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        UpdateSelection(currentSelectedIndex);
    }

    /// <summary>
    /// 更新选中状态高亮
    /// </summary>
    public void UpdateSelection(int selectedIndex)
    {
        currentSelectedIndex = selectedIndex;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetSelected(i == selectedIndex);
        }
    }
}
