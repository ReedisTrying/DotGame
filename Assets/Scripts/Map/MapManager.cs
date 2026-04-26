using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 负责地图节点的可达性、访问状态与交互入口。
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private Transform playerMarker;
    [SerializeField] private float markerMoveDuration = 0.2f;

    [Header("Events")]
    public UnityEvent<MapNode> OnNodeEntered;
    public UnityEvent OnMapCompleted;

    private readonly List<MapNode> allNodes = new List<MapNode>();
    private readonly HashSet<MapNode> reachableNodes = new HashSet<MapNode>();
    private readonly HashSet<MapNode> visitedNodes = new HashSet<MapNode>();

    private Coroutine markerRoutine;

    public MapNode CurrentNode { get; private set; }
    public IReadOnlyCollection<MapNode> ReachableNodes => reachableNodes;
    public IReadOnlyCollection<MapNode> VisitedNodes => visitedNodes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        // 等待一帧，确保 MapGenerator 在 Start 中完成生成
        yield return null;

        BuildNodeIndex();
        
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null && SaveManager.Instance.CurrentSaveData.currentNodeIndex != -1)
        {
            LoadStateFromSave();
        }
        else
        {
            InitializeReachableNodes();
        }
    }

    private void LoadStateFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.CurrentSaveData == null) return;
        
        var data = SaveManager.Instance.CurrentSaveData;
        
        reachableNodes.Clear();
        visitedNodes.Clear();
        
        // Restore visited
        foreach (var v in data.visitedNodes)
        {
            var node = allNodes.Find(n => n.gridY == v.x && n.gridX == v.y);
            if (node != null)
            {
                visitedNodes.Add(node);
                // Note: We don't set visual state here, UpdateNodeVisuals will do it
            }
        }
        
        // Restore current
        var currentNode = allNodes.Find(n => n.gridY == data.currentFloor && n.gridX == data.currentNodeIndex);
        if (currentNode != null)
        {
            CurrentNode = currentNode;
            
            // Calculate reachable nodes from current node
            foreach (var child in currentNode.outgoingPaths)
            {
                // Only add if not visited (though standard logic usually implies forward movement only)
                if (!visitedNodes.Contains(child))
                {
                    reachableNodes.Add(child);
                }
            }
            
            // Move marker
            if (playerMarker != null)
            {
                playerMarker.position = currentNode.transform.position;
            }
        }
        
        UpdateNodeVisuals();
    }

    /// <summary>
    /// 重新扫描场景中的 MapNode。
    /// </summary>
    public void BuildNodeIndex()
    {
        allNodes.Clear();
        var nodes = FindObjectsByType<MapNode>(FindObjectsSortMode.None);
        allNodes.AddRange(nodes);

        if (allNodes.Count == 0)
        {
            Debug.LogWarning("[MapManager] 未找到任何 MapNode，请确认地图已生成。");
        }
    }

    /// <summary>
    /// 重置为初始状态，只解锁起始层节点。
    /// </summary>
    public void InitializeReachableNodes()
    {
        reachableNodes.Clear();
        visitedNodes.Clear();
        CurrentNode = null;

        var startNodes = allNodes.Where(n => n.gridY == 0).OrderBy(n => n.gridX).ToList();
        if (startNodes.Count == 0 && allNodes.Count > 0)
        {
            // 兜底：没有标记为0层的节点，则选择最下层的一个
            var fallback = allNodes.OrderBy(n => n.gridY).ThenBy(n => n.gridX).First();
            startNodes.Add(fallback);
        }

        // Start 节点不可点击，开局应直接从 Start 出发解锁下一层，避免卡死。
        if (startNodes.Count > 0)
        {
            var startNode = startNodes[0];
            CurrentNode = startNode;
            visitedNodes.Add(startNode);

            foreach (var child in startNode.outgoingPaths)
            {
                if (child != null)
                {
                    reachableNodes.Add(child);
                }
            }

            // 兜底：若起点无出边，保留起点为可达，便于排查地图连接问题。
            if (reachableNodes.Count == 0)
            {
                reachableNodes.Add(startNode);
            }
        }

        UpdateNodeVisuals();

        if (playerMarker != null && startNodes.Count > 0)
        {
            playerMarker.position = startNodes[0].transform.position;
        }
    }

    /// <summary>
    /// 尝试进入指定节点，返回是否成功。
    /// </summary>
    public bool TryEnterNode(MapNode node)
    {
        if (node == null)
        {
            return false;
        }

        if (!reachableNodes.Contains(node) && !node.IsVisited)
        {
            Debug.Log("[MapManager] 该节点尚未解锁，无法进入: " + node.name);
            return false;
        }

        CurrentNode = node;

        if (!node.IsVisited)
        {
            visitedNodes.Add(node);
        }

        reachableNodes.Clear();
        foreach (var child in node.outgoingPaths)
        {
            if (child != null && !visitedNodes.Contains(child))
            {
                reachableNodes.Add(child);
            }
        }

        UpdateNodeVisuals();
        MoveMarker(node);
        OnNodeEntered?.Invoke(node);

        if (reachableNodes.Count == 0 && node.outgoingPaths.Count == 0)
        {
            OnMapCompleted?.Invoke();
        }
        
        // Save current position
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateMapPosition(node.gridY, node.gridX);
        }

        GameSceneManager.LoadByNodeType(node.nodeType);

        return true;
    }

    private void MoveMarker(MapNode target)
    {
        if (playerMarker == null || target == null)
        {
            return;
        }

        if (markerRoutine != null)
        {
            StopCoroutine(markerRoutine);
        }

        markerRoutine = StartCoroutine(MoveMarkerRoutine(target.transform.position));
    }

    private IEnumerator MoveMarkerRoutine(Vector3 targetPos)
    {
        if (markerMoveDuration <= 0f)
        {
            playerMarker.position = targetPos;
            yield break;
        }

        Vector3 startPos = playerMarker.position;
        float timer = 0f;
        while (timer < markerMoveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / markerMoveDuration);
            playerMarker.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        playerMarker.position = targetPos;
    }

    private void UpdateNodeVisuals()
    {
        foreach (var node in allNodes)
        {
            bool reachable = reachableNodes.Contains(node);
            bool visited = visitedNodes.Contains(node);
            node.SetState(reachable, visited);
        }
    }

    /// <summary>
    /// 返回地图场景
    /// </summary>
    public static void ReturnToMap()
    {
        GameSceneManager.LoadMap();
    }
}
