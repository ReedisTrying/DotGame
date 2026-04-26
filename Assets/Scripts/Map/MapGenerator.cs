using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class NodeTypeWeights
    {
        [Min(0f)] public float battle = 0.56f;
        [Min(0f)] public float eventNode = 0.24f;
        [Min(0f)] public float elite = 0.10f;
        [Min(0f)] public float shop = 0.06f;
        [Min(0f)] public float forge = 0.04f;
    }

    [Header("Settings")]
    public int totalFloors = 15;
    public int minWidth = 3;
    public int maxWidth = 5;
    public float layerHeight = 2.0f; // 层间距
    public float nodeSpacing = 1.5f; // 节点横向间距
    [Header("Orientation")]
    public float mapRotationX = 90f; // 整体绕X轴旋转角度

    [Header("Path Generation")]
    [Range(0f, 1f)]
    public float extraConnectionChance = 0.25f; // 生成额外连线的概率

    [Header("Visual Offset")]
    public float lineZOffset = 0.01f; // 让线条稍微沉到节点下方，避免重合
    public int backgroundSortingOrder = -2; // 地图背景层级（最下）
    public int lineSortingOrder = -1;  // 连线层级（中间）
    public int nodeSortingOrder = 0;   // 节点层级（最上）

    [Header("Bezier Curve")]
    [Min(1)] public int bezierIntermediatePoints = 10; // 中间插值点数量（不含起终点）
    public float bezierSCurveStrength = 0.15f; // S形横向偏移强度（按线段长度缩放）
    public float bezierSagStrength = 0.08f; // 下垂强度（按线段长度缩放）

    [Header("Node Type Rules")]
    [Range(0f, 1f)] public float firstShopProgress = 0.33f;
    [Range(0f, 1f)] public float secondShopProgress = 0.66f;
    [Min(1)] public int preBossForgeOffset = 2; // 距离Boss多少层时触发锻造偏置
    [Range(0f, 1f)] public float preBossForgeChance = 0.7f;
    [Range(0f, 1f)] public float earlyStageEndProgress = 0.33f;
    [Range(0f, 1f)] public float midStageEndProgress = 0.66f;

    [Header("Node Type Weights")]
    public NodeTypeWeights earlyStageWeights = new NodeTypeWeights
    {
        battle = 0.56f,
        eventNode = 0.24f,
        elite = 0.10f,
        shop = 0.06f,
        forge = 0.04f
    };

    public NodeTypeWeights midStageWeights = new NodeTypeWeights
    {
        battle = 0.42f,
        eventNode = 0.24f,
        elite = 0.20f,
        shop = 0.08f,
        forge = 0.06f
    };

    public NodeTypeWeights lateStageWeights = new NodeTypeWeights
    {
        battle = 0.30f,
        eventNode = 0.18f,
        elite = 0.30f,
        shop = 0.10f,
        forge = 0.12f
    };

    [Header("Background")]
    public SpriteRenderer mapBackgroundSprite;

    [Header("Prefabs")]
    public MapNode nodePrefab;
    public LineRenderer linePrefab;
    public Transform mapContainer;

    private List<List<MapNode>> mapGrid = new List<List<MapNode>>();
    public List<List<MapNode>> MapGrid => mapGrid;

    void Start()
    {
        // 如果未指定容器，默认使用当前对象，确保旋转生效
        if (mapContainer == null)
        {
            mapContainer = transform;
        }

        mapContainer.localRotation = Quaternion.Euler(mapRotationX, 0f, 0f);

        ApplySortingOrders();

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null)
        {
            Random.InitState(SaveManager.Instance.CurrentSaveData.mapSeed);
        }

        GenerateMap();
    }

    void GenerateMap()
    {
        // 1. 生成节点实体
        for (int y = 0; y < totalFloors; y++)
        {
            List<MapNode> currentLayer = new List<MapNode>();
            int layerWidth = y == 0 || y == totalFloors - 1
                ? 1
                : y == 1
                    ? 2
                    : Random.Range(minWidth, maxWidth + 1); // 起点终点只有1个；第二层固定2个；其余层随机
            
            // 居中偏移量计算
            float xOffset = -(layerWidth - 1) * 0.5f * nodeSpacing;

            for (int x = 0; x < layerWidth; x++)
            {
                Vector3 pos = new Vector3(xOffset + x * nodeSpacing, y * layerHeight, 0);
                MapNode newNode = Instantiate(nodePrefab, mapContainer);
                newNode.transform.localPosition = pos; // 使用本地坐标，方便整体旋转
                newNode.transform.localRotation = Quaternion.identity;
                newNode.gridX = x;
                newNode.gridY = y;
                newNode.nodeType = AssignNodeType(y, x, layerWidth); // 分配类型
                ApplyNodeSortingOrder(newNode);
                newNode.ApplyTypeSprite();
                currentLayer.Add(newNode);
            }
            mapGrid.Add(currentLayer);
        }

        // 2. 建立连线 (核心逻辑)
        for (int y = 0; y < totalFloors - 1; y++)
        {
            var currentLayer = mapGrid[y];
            var nextLayer = mapGrid[y + 1];

            ConnectLayerWithoutCrossing(currentLayer, nextLayer);
        }
        
        // 3. 绘制线条 (Visuals)
        DrawConnections();
    }

    void ApplySortingOrders()
    {
        if (mapBackgroundSprite != null)
        {
            mapBackgroundSprite.sortingOrder = backgroundSortingOrder;
        }
    }

    void ApplyNodeSortingOrder(MapNode node)
    {
        if (node == null)
        {
            return;
        }

        var renderers = node.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            sr.sortingOrder = nodeSortingOrder;
        }
    }

    void ConnectLayerWithoutCrossing(List<MapNode> currentLayer, List<MapNode> nextLayer)
    {
        if (currentLayer.Count == 0 || nextLayer.Count == 0)
        {
            return;
        }

        // 保持左右顺序，确保连接关系单调从而避免交叉
        var currentSorted = currentLayer.OrderBy(n => n.transform.localPosition.x).ToList();
        var nextSorted = nextLayer.OrderBy(n => n.transform.localPosition.x).ToList();

        void AddLink(MapNode source, MapNode target)
        {
            if (!source.outgoingPaths.Contains(target))
            {
                source.outgoingPaths.Add(target);
            }
            if (!target.incomingPaths.Contains(source))
            {
                target.incomingPaths.Add(source);
            }
        }

        int m = currentSorted.Count;
        int n = nextSorted.Count;

        // Pass A：保证下一层每个节点至少有一个入口（按横向顺序分配，不会交叉）
        for (int j = 0; j < n; j++)
        {
            int aIndex = (m == 1 || n == 1) ? 0 : Mathf.FloorToInt((float)j * (m - 1) / (float)(n - 1));
            AddLink(currentSorted[aIndex], nextSorted[j]);
        }

        // Pass B：保证当前层每个节点至少有一个出口，目标索引单调不减以避免交叉
        int lastTarget = 0;
        for (int i = 0; i < m; i++)
        {
            int targetIndex = (m == 1 || n == 1) ? 0 : Mathf.RoundToInt((float)i * (n - 1) / (float)(m - 1));
            targetIndex = Mathf.Clamp(targetIndex, lastTarget, n - 1);
            AddLink(currentSorted[i], nextSorted[targetIndex]);
            lastTarget = targetIndex;
        }

        // Pass C：随机添加额外连线以丰富路径，同时严格保持无交叉特性
        // 规则：对于节点 currentSorted[i]，其连接的目标索引 k 必须满足：
        // Max(currentSorted[i-1].Targets) <= k <= Min(currentSorted[i+1].Targets)
        for (int i = 0; i < m; i++)
        {
            var node = currentSorted[i];

            // 确定左边界：必须 >= 前一个节点连接的最大索引
             int leftBound = 0;
            if (i > 0)
            {
                var prevNode = currentSorted[i - 1];
                // 获取前一个节点所有连接目标在 nextSorted 中的最大索引
                if (prevNode.outgoingPaths.Count > 0)
                {
                    leftBound = prevNode.outgoingPaths.Max(target => nextSorted.IndexOf(target));
                }
            }

            // 确定右边界：必须 <= 后一个节点连接的最小索引
            int rightBound = n - 1;
            if (i < m - 1)
            {
                var nextNode = currentSorted[i + 1];
                // 获取后一个节点所有连接目标在 nextSorted 中的最小索引
                 if (nextNode.outgoingPaths.Count > 0)
                {
                    rightBound = nextNode.outgoingPaths.Min(target => nextSorted.IndexOf(target));
                }
            }

            // 在合法范围内尝试添加随机连线
            for (int k = leftBound; k <= rightBound; k++)
            {
                // 只有当概率满足且尚未连接时才添加
                if (Random.value < extraConnectionChance)
                {
                    AddLink(node, nextSorted[k]);
                }
            }
        }
    }

    NodeType AssignNodeType(int floor, int nodeIndex, int layerWidth)
    {
        // 第二层固定为一个战斗+一个精英（不随机）
        if (floor == 1)
        {
            if (layerWidth < 2)
            {
                return NodeType.Battle;
            }

            return nodeIndex == 0 ? NodeType.Battle : NodeType.Elite;
        }

        return AssignRandomType(floor);
    }

    NodeType AssignRandomType(int floor)
    {
        if (floor == 0) return NodeType.Start; // 第一层为玩家起点

        int bossFloor = totalFloors - 1;
        if (floor == bossFloor) return NodeType.Boss; // 顶层必定BOSS

        // 关键层保底：中前期/中后期给商店，Boss前一层更偏向锻造
        int shopFloorA = Mathf.Clamp(Mathf.RoundToInt(bossFloor * firstShopProgress), 1, bossFloor - 1);
        int shopFloorB = Mathf.Clamp(Mathf.RoundToInt(bossFloor * secondShopProgress), 1, bossFloor - 1);
        int preBossForgeFloor = Mathf.Clamp(bossFloor - preBossForgeOffset, 1, bossFloor - 1);

        if (floor == shopFloorA || floor == shopFloorB)
        {
            return NodeType.Shop;
        }

        if (floor == preBossForgeFloor && Random.value < preBossForgeChance)
        {
            return NodeType.Forge;
        }

        // 分阶段权重：越往后精英/锻造占比越高，战斗占比降低
        float progress = (float)floor / bossFloor;
        float clampedEarlyEnd = Mathf.Clamp01(earlyStageEndProgress);
        float clampedMidEnd = Mathf.Clamp(midStageEndProgress, clampedEarlyEnd, 1f);

        if (progress < clampedEarlyEnd)
        {
            return PickNodeTypeByWeights(earlyStageWeights);
        }

        if (progress < clampedMidEnd)
        {
            return PickNodeTypeByWeights(midStageWeights);
        }

        return PickNodeTypeByWeights(lateStageWeights);
    }

    NodeType PickNodeTypeByWeights(NodeTypeWeights weights)
    {
        if (weights == null)
        {
            return NodeType.Battle;
        }

        return PickNodeTypeByWeights(
            battleWeight: weights.battle,
            eventWeight: weights.eventNode,
            eliteWeight: weights.elite,
            shopWeight: weights.shop,
            forgeWeight: weights.forge);
    }

    NodeType PickNodeTypeByWeights(float battleWeight, float eventWeight, float eliteWeight, float shopWeight, float forgeWeight)
    {
        float total = battleWeight + eventWeight + eliteWeight + shopWeight + forgeWeight;
        if (total <= 0f)
        {
            return NodeType.Battle;
        }

        float roll = Random.value * total;

        if (roll < battleWeight) return NodeType.Battle;
        roll -= battleWeight;

        if (roll < eventWeight) return NodeType.Event;
        roll -= eventWeight;

        if (roll < eliteWeight) return NodeType.Elite;
        roll -= eliteWeight;

        if (roll < shopWeight) return NodeType.Shop;
        return NodeType.Forge;
    }
    
    void DrawConnections()
    {
        foreach (var layer in mapGrid)
        {
            foreach (var node in layer)
            {
                foreach (var child in node.outgoingPaths)
                {
                    LineRenderer lr = Instantiate(linePrefab, mapContainer);
                    lr.useWorldSpace = false; // 让线条跟随父级旋转
                    lr.transform.localPosition = Vector3.zero; // 确保不受预制体偏移影响
                    lr.transform.localRotation = Quaternion.identity; // 继承父级旋转
                    lr.sortingOrder = lineSortingOrder;

                    // 给线条一个轻微的Z偏移，避免与节点在同一平面产生覆盖
                    Vector3 zOffset = new Vector3(0f, 0f, lineZOffset);

                    var start = node.transform.localPosition + zOffset;
                    var end = child.transform.localPosition + zOffset;
                    var points = GenerateBezierPoints(start, end, bezierIntermediatePoints);

                    lr.positionCount = points.Count;
                    lr.SetPositions(points.ToArray());
                }
            }
        }
    }

    List<Vector3> GenerateBezierPoints(Vector3 start, Vector3 end, int intermediatePoints)
    {
        int totalPoints = Mathf.Max(2, intermediatePoints + 2); // 起点 + 中间点 + 终点
        var points = new List<Vector3>(totalPoints);

        Vector3 direction = end - start;
        float length = direction.magnitude;
        Vector3 tangent = length > 0.0001f ? direction / length : Vector3.up;
        Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0f);

        float sOffset = length * bezierSCurveStrength;
        float sagOffset = length * bezierSagStrength;
        Vector3 sag = Vector3.down * sagOffset;

        // 三次贝塞尔：通过相反横向偏移做出轻微S形，同时整体下垂
        Vector3 p0 = start;
        Vector3 p1 = start + direction * 0.33f + perpendicular * sOffset + sag;
        Vector3 p2 = start + direction * 0.66f - perpendicular * sOffset + sag;
        Vector3 p3 = end;

        for (int i = 0; i < totalPoints; i++)
        {
            float t = (float)i / (totalPoints - 1);
            points.Add(EvaluateCubicBezier(p0, p1, p2, p3, t));
        }

        return points;
    }

    Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * p0
             + 3f * uu * t * p1
             + 3f * u * tt * p2
             + ttt * p3;
    }
}