using UnityEngine;

/// <summary>
/// 屏幕边界生成器
/// 挂载在场景中空物体上，自动计算摄像机视锥体边界并生成空气墙
/// </summary>
public class ScreenBoundary : MonoBehaviour
{
    private Camera mainCamera;
    private Vector3 screenBounds;

    private void Awake()
    {
        mainCamera = Camera.main;
        
        CalculateScreenBounds();
        CreateScreenColliders();
    }

    /// <summary>
    /// 计算屏幕边界（基于当前物体与摄像机的距离）
    /// </summary>
    private void CalculateScreenBounds()
    {
        // 计算从摄像机到当前平面（Transform所在深度）的投影距离
        float distance = Vector3.Dot(transform.position - mainCamera.transform.position, mainCamera.transform.forward);
        
        if (mainCamera.orthographic)
        {
            float y = mainCamera.orthographicSize;
            float x = y * mainCamera.aspect;
            screenBounds = new Vector3(x, y, distance);
        }
        else
        {
            float y = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float x = y * mainCamera.aspect;
            screenBounds = new Vector3(x, y, distance);
        }
    }

    /// <summary>
    /// 生成屏幕边缘的空气墙
    /// </summary>
    private void CreateScreenColliders()
    {
        float thickness = 2f; 
        float zDepth = 1000f; // 覆盖Z轴深度

        // 左墙 (增加高度以覆盖角落)
        CreateOneWall("LeftWall", 
            new Vector3(-screenBounds.x - thickness/2, 0, 0), 
            new Vector3(thickness, screenBounds.y * 2 + thickness * 2, zDepth));

        // 右墙
        CreateOneWall("RightWall", 
            new Vector3(screenBounds.x + thickness/2, 0, 0), 
            new Vector3(thickness, screenBounds.y * 2 + thickness * 2, zDepth));

        // 上墙 (增加宽度以覆盖角落)
        CreateOneWall("TopWall", 
            new Vector3(0, screenBounds.y + thickness/2, 0), 
            new Vector3(screenBounds.x * 2 + thickness * 2, thickness, zDepth));

        // 下墙
        CreateOneWall("BottomWall", 
            new Vector3(0, -screenBounds.y - thickness/2, 0), 
            new Vector3(screenBounds.x * 2 + thickness * 2, thickness, zDepth));
    }

    private void CreateOneWall(string name, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = localPosition;
        
        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (screenBounds != Vector3.zero)
        {
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(screenBounds.x * 2, screenBounds.y * 2, 1f));
        }
    }
}
