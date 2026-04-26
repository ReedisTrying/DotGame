using UnityEngine;
using System.Collections.Generic;

public enum NodeType { Battle, Elite, Event, Shop, Forge, Boss, Start }

public class MapNode : MonoBehaviour
{
    public NodeType nodeType;
    public int gridX; // 在该层的横向索引
    public int gridY; // 层数 (Floor)
    
    // 存储连接关系
    public List<MapNode> incomingPaths = new List<MapNode>(); // 父节点
    public List<MapNode> outgoingPaths = new List<MapNode>(); // 子节点

    [Header("State")]
    public bool IsVisited { get; private set; }
    public bool IsReachable { get; private set; }

    [Header("Visuals")]
    public SpriteRenderer iconRenderer;
    [Header("Type Sprites")]
    public Sprite battleSprite;
    public Sprite eliteSprite;
    public Sprite eventSprite;
    public Sprite shopSprite;
    public Sprite forgeSprite;
    public Sprite bossSprite;

    private SphereCollider nodeCollider;

    // 点击事件
    private void Awake()
    {
        // 确保节点有碰撞体以便接收点击事件
        nodeCollider = GetComponent<SphereCollider>();
        if (nodeCollider == null)
        {
            nodeCollider = gameObject.AddComponent<SphereCollider>();
            nodeCollider.radius = 0.5f; // 设置合适的半径
            nodeCollider.isTrigger = true; // 设为触发器，避免物理碰撞
        }
    }

    private void OnMouseDown()
    {
        if (nodeType == NodeType.Start)
        {
            return;
        }

        // 只有当该节点是"可到达"状态时才能点击
        if (MapManager.Instance != null)
        {
            // 添加调试日志，以便确认点击事件是否被触发
            Debug.Log($"[MapNode] 点击了节点: {gameObject.name}, 类型: {nodeType}, 可达: {IsReachable}, 已访问: {IsVisited}");
            MapManager.Instance.TryEnterNode(this);
        }
        else
        {
            Debug.LogError("[MapNode] MapManager.Instance 为空！");
        }
    }

    public void ApplyTypeSprite()
    {
        if (iconRenderer == null)
        {
            return;
        }

        iconRenderer.sprite = nodeType switch
        {
            NodeType.Battle => battleSprite,
            NodeType.Elite => eliteSprite,
            NodeType.Event => eventSprite,
            NodeType.Shop => shopSprite,
            NodeType.Forge => forgeSprite,
            NodeType.Boss => bossSprite,
            NodeType.Start => null,
            _ => iconRenderer.sprite
        };
    }

    /// <summary>
    /// 由 MapManager 调用以更新解锁/访问状态与可视表现。
    /// </summary>
    public void SetState(bool reachable, bool visited)
    {
        IsReachable = reachable;
        IsVisited = visited;

        if (nodeCollider != null)
        {
            nodeCollider.enabled = nodeType != NodeType.Start;
        }

        if (iconRenderer != null)
        {
            // 已访问的节点降低亮度，可到达的保持正常，其他半透明
            if (visited)
            {
                iconRenderer.color = Color.gray;
            }
            else if (reachable)
            {
                iconRenderer.color = Color.white;
            }
            else
            {
                iconRenderer.color = new Color(1f, 1f, 1f, 0.9f);
            }
        }
    }
}