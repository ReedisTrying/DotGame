using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 地图镜头控制：
/// 1) 鼠标滚轮平移（仅沿地图延伸方向）
/// 2) 鼠标左键拖拽平移（仅沿地图延伸方向）
/// 3) 平移下限为初始状态，上限为可看到最后一个节点
/// 4) 自动跟随玩家标记到指定节点
/// </summary>
public class MapCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MapGenerator mapGenerator;

    [Header("Scroll Move")]
    [SerializeField] private float scrollMoveStep = 1.2f;

    [Header("Drag")]
    [SerializeField] private float dragWorldPerPixel = 0.01f;
    [SerializeField] private float dragSensitivity = 1f;

    [Header("Auto Follow")]
    [SerializeField] private float followDuration = 0.5f; // 跟随动画持续时间
    [SerializeField] private bool useSmoothFollow = true; // 是否使用平滑跟随

    [Header("Axis Constraint")]
    [SerializeField] private bool keepHeightWhileMoving = true;

    private Vector3 moveAxisWorld = Vector3.up;

    private bool isDragging;
    private Vector2 dragStartMouse;
    private float dragStartOffset;

    private Vector3 initialCameraPosition;
    private float currentOffset;
    private float minOffset;
    private float maxOffset;

    private Coroutine followCoroutine;

    private IEnumerator Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        // 等待地图生成
        yield return null;

        InitializeLimits();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            return;
        }

        HandleScrollMove();
        HandleDrag();
    }

    private void InitializeLimits()
    {
        var allNodes = FindObjectsByType<MapNode>(FindObjectsSortMode.None)
            .OrderBy(n => n.gridY)
            .ThenBy(n => n.gridX)
            .ToArray();

        if (allNodes.Length == 0)
        {
            initialCameraPosition = targetCamera.transform.position;
            moveAxisWorld = Vector3.up;
            minOffset = 0f;
            maxOffset = 0f;
            currentOffset = 0f;
            return;
        }

        initialCameraPosition = targetCamera.transform.position;

        MapNode lastNode = allNodes[allNodes.Length - 1];

        Vector3 firstNodePos = allNodes[0].transform.position;
        Vector3 axisCandidate = lastNode.transform.position - firstNodePos;
        if (axisCandidate.sqrMagnitude < 0.0001f)
        {
            if (mapGenerator != null && mapGenerator.mapContainer != null)
            {
                axisCandidate = mapGenerator.mapContainer.TransformDirection(Vector3.up);
            }
            else
            {
                axisCandidate = Vector3.forward;
            }
        }

        if (keepHeightWhileMoving)
        {
            axisCandidate = Vector3.ProjectOnPlane(axisCandidate, Vector3.up);
            if (axisCandidate.sqrMagnitude < 0.0001f)
            {
                axisCandidate = Vector3.forward;
            }
        }

        moveAxisWorld = axisCandidate.normalized;

        minOffset = 0f;
        float offsetToLast = Vector3.Dot(lastNode.transform.position - initialCameraPosition, moveAxisWorld);

        // 若方向相反，反转轴向，保证正方向指向地图末端
        if (offsetToLast < 0f)
        {
            moveAxisWorld = -moveAxisWorld;
            offsetToLast = -offsetToLast;
        }

        maxOffset = Mathf.Max(0f, offsetToLast);
        currentOffset = 0f;
        ApplyOffset(currentOffset);
    }

    private void HandleScrollMove()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.0001f)
        {
            return;
        }

        float delta = scroll * scrollMoveStep;
        ApplyOffset(currentOffset + delta);
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = false;
                return;
            }

            isDragging = true;
            dragStartMouse = Input.mousePosition;
            dragStartOffset = currentOffset;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (!isDragging || !Input.GetMouseButton(0))
        {
            return;
        }

        float pixelDeltaY = ((Vector2)Input.mousePosition - dragStartMouse).y;
        float delta = pixelDeltaY * dragWorldPerPixel * dragSensitivity;
        ApplyOffset(dragStartOffset + delta);
    }

    private void ApplyOffset(float targetOffset)
    {
        currentOffset = Mathf.Clamp(targetOffset, minOffset, maxOffset);
        targetCamera.transform.position = initialCameraPosition + moveAxisWorld * currentOffset;
    }

    /// <summary>
    /// 让相机跟随到指定位置
    /// </summary>
    public void FollowToPosition(Vector3 targetPosition, bool instantly = false)
    {
        if (followCoroutine != null)
        {
            StopCoroutine(followCoroutine);
        }

        if (instantly)
        {
            // 如果立即移动，直接设置相机位置
            targetCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, targetCamera.transform.position.z);
        }
        else
        {
            // 否则启动平滑跟随协程
            followCoroutine = StartCoroutine(FollowToPositionCoroutine(targetPosition));
        }
    }

    private IEnumerator FollowToPositionCoroutine(Vector3 targetPosition)
    {
        if (!useSmoothFollow)
        {
            // 如果不使用平滑跟随，直接设置位置
            targetCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, targetCamera.transform.position.z);
            yield break;
        }

        Vector3 startPosition = targetCamera.transform.position;
        float elapsedTime = 0f;

        // 只更新 X 和 Y 坐标，保持 Z 坐标不变
        Vector3 startXY = new Vector3(startPosition.x, startPosition.y, 0);
        Vector3 targetXY = new Vector3(targetPosition.x, targetPosition.y, 0);

        while (elapsedTime < followDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / followDuration);

            Vector3 currentXY = Vector3.Lerp(startXY, targetXY, t);
            targetCamera.transform.position = new Vector3(currentXY.x, currentXY.y, targetCamera.transform.position.z);

            yield return null;
        }

        // 确保最终位置准确
        targetCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, targetCamera.transform.position.z);
    }
}
