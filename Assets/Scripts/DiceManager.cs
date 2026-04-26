using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 骰子管理器 - 统一管理游戏中所有的骰子对象
/// </summary>
public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance { get; private set; }

    [Header("Dice Settings")]
    [Tooltip("骰子预制体")]
    [SerializeField]
    private GameObject dicePrefab;
    [SerializeField] private Sprite redDiceSprite;
    [SerializeField] private Sprite yellowDiceSprite;
    [SerializeField] private Sprite blueDiceSprite;
    [SerializeField] private Sprite orangeDiceSprite;
    [SerializeField] private Sprite greenDiceSprite;
    [SerializeField] private Sprite purpleDiceSprite;
    [SerializeField] private Sprite blackDiceSprite;
    [SerializeField] private Sprite enemyDiceSprite;

    [Header("Face Swap Transition")]
    [SerializeField] private float faceSwapFadeDuration = 0.25f;


    [Header("Throw Settings")]
    [SerializeField] private float minThrowForceX = 300f;
    [SerializeField] private float maxThrowForceX = 600f;
    [SerializeField] private float throwForceY = -40f;
    [SerializeField] private float minThrowForceZ = 300f;
    [SerializeField] private float maxThrowForceZ = 600f;
    [SerializeField] private float torqueStrength = 200f;

    [Header("Spawn & Cleanup")]
    [SerializeField] private Transform defaultThrowSpawnPoint;

    [Header("Arrange Settings")]
    [SerializeField] private List<Transform> diceSlots = new List<Transform>();
    [SerializeField] private List<Transform> enemyDiceSlots = new List<Transform>();
    [SerializeField] private float slotYOffset = 0.5f;
    [SerializeField] private Vector3 preThrowEuler = new Vector3(25f, 35f, -15f);
    [SerializeField] private float preThrowDelay = 0.6f; // 抛掷前等待时间，期间保持旋转
    [SerializeField] private float preThrowSpinSpeed = 450f; // 指定旋转角速度（度/秒），>0 时优先生效
    [SerializeField] private float settleLinearSpeed = 0.05f;
    [SerializeField] private float settleAngularSpeed = 1.5f;
    [SerializeField] private float settleStableTime = 0.75f;
    [SerializeField] private float settlePollInterval = 0.1f;
    [SerializeField] private float snapRotationDuration = 0.2f;
    [SerializeField] private float moveDuration = 0.8f;
    [SerializeField] private float moveDelayBetweenDice = 0.05f;
    [SerializeField] private Ease moveEase = Ease.InOutSine;
    [SerializeField] private float preThrowArcSideOffset = 1.2f;
    [SerializeField] private bool tiltTowardCamera = true;
    [SerializeField] private float slotTiltDegrees = 5f;
    [SerializeField] private Transform overrideCamera;

    public float MoveDelayBetweenDice => moveDelayBetweenDice;

    [Header("RuntimeInfo")]
    [SerializeField]
    private List<Dice> activeDice = new List<Dice>();
    [SerializeField]
    private List<Dice> activeEnemyDice = new List<Dice>();
    private List<GameObject> thrownDiceObjects = new List<GameObject>();
    private List<GameObject> thrownEnemyDiceObjects = new List<GameObject>();
    private Coroutine arrangeRoutine;
    private Coroutine enemyArrangeRoutine;
    public bool IsEnemyThrowing { get; private set; }
    
    public event System.Action OnEnemyDiceArranged;

    // 记录骰子应该回到的目标槽位索引（用于重新投掷时保持原位）
    private Dictionary<Dice, int> diceTargetSlotMap = new Dictionary<Dice, int>();
    private readonly Dictionary<Dice, List<int>> rendererToRuntimeFaceIndexMap = new Dictionary<Dice, List<int>>();

    private readonly Dictionary<DiceColor, Sprite> gradientCache = new Dictionary<DiceColor, Sprite>();

    private void Awake()
    {
        // 确保单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 创建一个骰子
    /// </summary>
    public Dice CreateDice(Vector3 position, Quaternion rotation = default)
    {
        if (dicePrefab == null)
        {
            Debug.LogError("骰子预制体未设置！");
            return null;
        }

        GameObject diceObj = Instantiate(dicePrefab, position, rotation == default ? Quaternion.identity : rotation);
        Dice dice = diceObj.GetComponent<Dice>();
        
        if (dice != null)
        {
            activeDice.Add(dice);
        }
        else
        {
            Debug.LogError("骰子预制体上没有Dice组件！");
        }

        return dice;
    }

    /// <summary>
    /// 设置骰子预制体
    /// </summary>
    public void SetDicePrefab(GameObject prefab)
    {
        dicePrefab = prefab;
    }

    /// <summary>
    /// 在指定位置创建并投掷骰子（可选附带运行时数据）
    /// </summary>
    public Dice ThrowDiceAt(Vector3 position, Quaternion rotation = default, RuntimeDice runtimeDice = null)
    {
        EnsureDicePool(1, position, rotation == default ? Quaternion.identity : rotation);
        if (activeDice.Count == 0 || activeDice[0] == null)
        {
            Debug.LogError("DiceManager: 无法创建或获取骰子实例");
            return null;
        }

        Dice dice = activeDice[0];
        ResetDiceStateForThrow(dice, position, rotation == default ? Quaternion.identity : rotation);
        SetupDiceVisuals(dice.gameObject, runtimeDice);
        ApplyThrowForces(dice);
        RegisterThrownDice(dice.gameObject);
        return dice;
    }

    /// <summary>
    /// 批量投掷一组运行时骰子
    /// </summary>
    public void ThrowDiceBatch(List<RuntimeDice> runtimeDiceList, Transform spawnPoint = null)
    {
        if (runtimeDiceList == null || runtimeDiceList.Count == 0)
        {
            Debug.LogWarning("DiceManager: 没有需要投掷的骰子");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(spawnPoint);

        EnsureDicePool(runtimeDiceList.Count, spawnPos, Quaternion.identity);

        // 重置上一轮抛掷状态
        StopAllThrownDice();
        thrownDiceObjects.Clear();

        for (int i = 0; i < runtimeDiceList.Count; i++)
        {
            Dice dice = activeDice[i];
            if (dice == null) continue;

            ResetDiceStateForThrow(dice, spawnPos, Quaternion.identity);
            SetupDiceVisuals(dice.gameObject, runtimeDiceList[i]);
            dice.transform.position = spawnPos;
            dice.transform.rotation = Quaternion.identity;
        }

        StartCoroutine(MoveDiceToSlotsThenThrow(runtimeDiceList));
    }

    /// <summary>
    /// 批量投掷敌方运行时骰子
    /// </summary>
    public void ThrowEnemyDiceBatch(List<RuntimeDice> runtimeDiceList, Transform spawnPoint = null)
    {
        if (runtimeDiceList == null || runtimeDiceList.Count == 0)
        {
            Debug.LogWarning("DiceManager: 没有需要投掷的敌方骰子");
            return;
        }

        IsEnemyThrowing = true;

        EnsureDiceSlots();
        int desiredCount = Mathf.Min(runtimeDiceList.Count, enemyDiceSlots.Count);
        if (desiredCount <= 0)
        {
            Debug.LogWarning("DiceManager: 敌方槽位数量不足，无法抛掷敌方骰子");
            IsEnemyThrowing = false;
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(spawnPoint);
        EnsureEnemyDicePool(desiredCount, spawnPos, Quaternion.identity);

        StopAllThrownEnemyDice();
        thrownEnemyDiceObjects.Clear();

        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeEnemyDice[i];
            if (dice == null) continue;

            ResetDiceStateForThrow(dice, spawnPos, Quaternion.identity);
            SetupDiceVisuals(dice.gameObject, runtimeDiceList[i]);
            dice.transform.position = spawnPos;
            dice.transform.rotation = Quaternion.identity;
        }

        StartCoroutine(MoveEnemyDiceToSlotsThenThrow(desiredCount));
    }

    /// <summary>
    /// 播放骰子打出动画（飞向屏幕中心）
    /// </summary>
    public void PlayDicePlayedAnimation(List<int> playedIndices)
    {
        if (playedIndices == null || playedIndices.Count == 0) return;

        List<Dice> playedDiceList = new List<Dice>();
        foreach (int index in playedIndices)
        {
            if (index >= 0 && index < activeDice.Count && index < diceSlots.Count)
            {
                Dice dice = activeDice[index];
                if (dice != null)
                {
                    playedDiceList.Add(dice);
                }
            }
        }

        if (playedDiceList.Count > 0)
        {
            StartCoroutine(AnimateDiceToScreenCenter(playedDiceList));
        }
    }

    private IEnumerator AnimateDiceToScreenCenter(List<Dice> playedDiceList)
    {
        // 骰子先砸向屏幕中心
        List<Tween> hitTweens = new List<Tween>(playedDiceList.Count);
        Vector3 screenCenterWorldPos = Vector3.zero;
        
        // 获取屏幕中心的世界坐标
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                screenCenterWorldPos = ray.GetPoint(enter);
            }
            else
            {
                screenCenterWorldPos = ray.GetPoint(10f); // 兜底
            }
        }
        
        // 稍微抬高一点，避免穿模
        screenCenterWorldPos.y = 2f;

        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;

            PrepareDiceForTween(dice);
            
            // 随机一点偏移，避免完全重叠
            Vector3 targetPos = screenCenterWorldPos + new Vector3(
                Random.Range(-1f, 1f), 
                Random.Range(-0.5f, 0.5f), 
                Random.Range(-1f, 1f)
            );

            // 快速砸向中心
            hitTweens.Add(dice.transform.DOMove(targetPos, 0.2f).SetEase(Ease.InBack));
        }

        while (AnyTweenRunning(hitTweens))
        {
            yield return null;
        }

        // 飞向生成点
        List<Tween> moveTweens = new List<Tween>(playedDiceList.Count);
        Vector3 spawnPos = GetSpawnPosition(null); // 获取默认生成点

        for (int i = 0; i < playedDiceList.Count; i++)
        {
            Dice dice = playedDiceList[i];
            if (dice == null) continue;

            moveTweens.Add(dice.transform.DOMove(spawnPos, 0.2f).SetEase(Ease.OutQuad));
        }

        while (AnyTweenRunning(moveTweens))
        {
            yield return null;
        }
    }

    /// <summary>
    /// 重新投掷被打出的骰子：从原位置投掷后再落回原位置，没打出的保持不动
    /// </summary>
    public void ReplacePlayedDice(List<RuntimeDice> runtimeDiceList, List<int> playedIndices, Transform spawnPoint = null)
    {
        if (runtimeDiceList == null || runtimeDiceList.Count == 0)
        {
            Debug.LogWarning("DiceManager: 没有需要处理的骰子");
            return;
        }

        if (playedIndices == null || playedIndices.Count == 0)
        {
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(spawnPoint);

        // 获取被打出的骰子，从原位置重新投掷
        List<Dice> playedDiceList = new List<Dice>();
        List<int> playedSlotIndices = new List<int>();

        // 清空之前的目标槽位映射
        diceTargetSlotMap.Clear();

        foreach (int index in playedIndices)
        {
            if (index >= 0 && index < activeDice.Count && index < diceSlots.Count)
            {
                Dice dice = activeDice[index];
                if (dice != null)
                {
                    // 记录原位置（槽位位置）
                    Transform slot = diceSlots[index];
                    if (slot != null)
                    {
                        // 记录这个骰子应该回到哪个槽位
                        diceTargetSlotMap[dice] = index;

                        // 仅更新视觉和数据，不重置位置
                        SetupDiceVisuals(dice.gameObject, runtimeDiceList[index]);
                        
                        // 确保物理状态是静止的，以便动画控制
                        dice.StopDice();

                        playedDiceList.Add(dice);
                        playedSlotIndices.Add(index);
                    }
                }
            }
        }

        if (playedDiceList.Count > 0)
        {
            foreach (var dice in activeDice)
            {
                if (dice != null && !playedDiceList.Contains(dice))
                {
                    dice.SetGhostMode(true);
                }
            }
            StartCoroutine(ThrowPlayedDiceBackToSlots(playedDiceList, playedSlotIndices, spawnPos));
        }
    }

    private IEnumerator ThrowPlayedDiceBackToSlots(List<Dice> playedDiceList, List<int> playedSlotIndices, Vector3 spawnPos)
    {
        if (playedDiceList.Count == 0 || playedSlotIndices.Count == 0)
        {
            yield break;
        }

        // 先砸向屏幕中心
        List<Tween> hitTweens = new List<Tween>(playedDiceList.Count);
        Vector3 screenCenterWorldPos = Vector3.zero;
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                screenCenterWorldPos = ray.GetPoint(enter);
            }
            else
            {
                screenCenterWorldPos = ray.GetPoint(10f);
            }
        }
        screenCenterWorldPos.y = 2f;

        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;
            PrepareDiceForTween(dice);
            Vector3 targetPos = screenCenterWorldPos + new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-1f, 1f)
            );
            hitTweens.Add(dice.transform.DOMove(targetPos, 0.2f).SetEase(Ease.InBack));
        }

        while (AnyTweenRunning(hitTweens))
        {
            yield return null;
        }

        // 再回到生成点
        List<Tween> toSpawnTweens = new List<Tween>(playedDiceList.Count);
        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;
            toSpawnTweens.Add(dice.transform.DOMove(spawnPos, 0.2f).SetEase(Ease.OutQuad));
        }

        while (AnyTweenRunning(toSpawnTweens))
        {
            yield return null;
        }

        // 与初次投掷一致：从生成点飞向槽位准备投掷
        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;
            ResetDiceStateForThrow(dice, spawnPos, Quaternion.identity);
            dice.transform.position = spawnPos;
            dice.transform.rotation = Quaternion.identity;
        }

        List<Tween> moveTweens = new List<Tween>(playedDiceList.Count);
        
        for (int i = 0; i < playedDiceList.Count && i < playedSlotIndices.Count; i++)
        {
            Dice dice = playedDiceList[i];
            int slotIndex = playedSlotIndices[i];
            Transform slot = diceSlots[slotIndex];
            if (dice == null || slot == null) continue;

            PrepareDiceForTween(dice);
            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;
            Vector3 startPos = dice.transform.position;
            Vector3 xzDir = targetPos - startPos;
            xzDir.y = 0f;
            if (xzDir.sqrMagnitude < 1e-4f)
            {
                xzDir = Vector3.right;
            }
            Vector3 perpXZ = new Vector3(-xzDir.z, 0f, xzDir.x).normalized;
            Vector3 midPos = (startPos + targetPos) * 0.5f + perpXZ * preThrowArcSideOffset;
            midPos.y = startPos.y;
            moveTweens.Add(dice.transform
                .DOPath(new[] { startPos, midPos, targetPos }, moveDuration, PathType.CatmullRom, PathMode.Full3D)
                .SetEase(moveEase));
        }

        while (AnyTweenRunning(moveTweens))
        {
            yield return null;
        }

        // 骰子进行旋转准备（在槽位上）
        List<Tween> spinTweens = new List<Tween>(playedDiceList.Count);
        float spinDuration = 0;
        if (preThrowSpinSpeed > 0f)
        {
            // 将角速度转换为完成360度所需时间，确保旋转速度明确可控
            spinDuration = 360f / preThrowSpinSpeed;
        }

        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;

            dice.transform.rotation = Quaternion.Euler(preThrowEuler);
            if (preThrowDelay > 0f && spinDuration > 0f)
            {
                var spin = dice.transform
                    .DORotate(new Vector3(0f, 0f, 360f), spinDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);
                spinTweens.Add(spin);
            }
        }

        if (preThrowDelay > 0f)
        {
            yield return new WaitForSeconds(preThrowDelay);
        }

        // 结束旋转但保留当前姿态
        foreach (var spin in spinTweens)
        {
            spin.Kill(false);
        }

        // 投掷骰子（它们会落回原来的槽位位置）
        thrownDiceObjects.Clear();
        foreach (var dice in playedDiceList)
        {
            if (dice == null) continue;

            ResetDiceStateForThrow(dice, dice.transform.position, dice.transform.rotation);
            
            ApplyThrowForces(dice);
            thrownDiceObjects.Add(dice.gameObject);
        }

        RestartArrangeRoutine();
    }

    private IEnumerator MoveDiceToSlotsThenThrow(List<RuntimeDice> runtimeDiceList)
    {
        int desiredCount = Mathf.Min(runtimeDiceList.Count, diceSlots.Count);
        if (desiredCount == 0)
        {
            Debug.LogWarning("DiceManager: 槽位数量不足，无法抛掷骰子");
            yield break;
        }

        List<Tween> tweens = new List<Tween>(desiredCount);

        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeDice[i];
            Transform slot = diceSlots[i];
            if (dice == null || slot == null) continue;

            PrepareDiceForTween(dice);
            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;
            Vector3 startPos = dice.transform.position;
            Vector3 xzDir = targetPos - startPos;
            xzDir.y = 0f;
            if (xzDir.sqrMagnitude < 1e-4f)
            {
                xzDir = Vector3.right;
            }
            Vector3 perpXZ = new Vector3(-xzDir.z, 0f, xzDir.x).normalized;
            Vector3 midPos = (startPos + targetPos) * 0.5f + perpXZ * preThrowArcSideOffset;
            midPos.y = startPos.y;
            tweens.Add(dice.transform
                .DOPath(new[] { startPos, midPos, targetPos }, moveDuration, PathType.CatmullRom, PathMode.Full3D)
                .SetEase(moveEase));
        }

        while (AnyTweenRunning(tweens))
        {
            yield return null;
        }

        // 定位后立即摆到预设角度，并原地持续旋转 preThrowDelay 时长
        List<Tween> spinTweens = new List<Tween>(desiredCount);
        float spinDuration = 0;
        if (preThrowSpinSpeed > 0f)
        {
            spinDuration = 360f / preThrowSpinSpeed;
        }
        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeDice[i];
            if (dice == null) continue;

            dice.transform.rotation = Quaternion.Euler(preThrowEuler);
            if (preThrowDelay > 0f && spinDuration > 0f)
            {
                var spin = dice.transform
                    .DORotate(new Vector3(0f, 0f, 360f), spinDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);
                spinTweens.Add(spin);
            }
        }

        if (preThrowDelay > 0f)
        {
            yield return new WaitForSeconds(preThrowDelay);
        }

        // 结束旋转但保留当前姿态
        foreach (var spin in spinTweens)
        {
            spin.Kill(false);
        }

        thrownDiceObjects.Clear();
        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeDice[i];
            if (dice == null) continue;

            ApplyThrowForces(dice);
            thrownDiceObjects.Add(dice.gameObject);
        }

        RestartArrangeRoutine();
    }

    private IEnumerator MoveEnemyDiceToSlotsThenThrow(int desiredCount)
    {
        if (desiredCount == 0)
        {
            yield break;
        }

        List<Tween> tweens = new List<Tween>(desiredCount);

        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeEnemyDice[i];
            Transform slot = enemyDiceSlots[i];
            if (dice == null || slot == null) continue;

            PrepareDiceForTween(dice);
            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;
            Vector3 startPos = dice.transform.position;
            Vector3 xzDir = targetPos - startPos;
            xzDir.y = 0f;
            if (xzDir.sqrMagnitude < 1e-4f)
            {
                xzDir = Vector3.right;
            }
            Vector3 perpXZ = new Vector3(-xzDir.z, 0f, xzDir.x).normalized;
            Vector3 midPos = (startPos + targetPos) * 0.5f + perpXZ * preThrowArcSideOffset;
            midPos.y = startPos.y;
            tweens.Add(dice.transform
                .DOPath(new[] { startPos, midPos, targetPos }, moveDuration, PathType.CatmullRom, PathMode.Full3D)
                .SetEase(moveEase));
        }

        while (AnyTweenRunning(tweens))
        {
            yield return null;
        }

        List<Tween> spinTweens = new List<Tween>(desiredCount);
        float spinDuration = 0;
        if (preThrowSpinSpeed > 0f)
        {
            spinDuration = 360f / preThrowSpinSpeed;
        }

        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeEnemyDice[i];
            if (dice == null) continue;

            dice.transform.rotation = Quaternion.Euler(preThrowEuler);
            if (preThrowDelay > 0f && spinDuration > 0f)
            {
                var spin = dice.transform
                    .DORotate(new Vector3(0f, 0f, 360f), spinDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);
                spinTweens.Add(spin);
            }
        }

        if (preThrowDelay > 0f)
        {
            yield return new WaitForSeconds(preThrowDelay);
        }

        foreach (var spin in spinTweens)
        {
            spin.Kill(false);
        }

        thrownEnemyDiceObjects.Clear();
        for (int i = 0; i < desiredCount; i++)
        {
            Dice dice = activeEnemyDice[i];
            if (dice == null) continue;

            ApplyThrowForces(dice);
            thrownEnemyDiceObjects.Add(dice.gameObject);
        }

        RestartEnemyArrangeRoutine();
    }

    /// <summary>
    /// 清空现有骰子并根据给定的运行时数据生成一批3D骰子
    /// </summary>
    public void SpawnRuntimeDiceHand(List<RuntimeDice> runtimeDiceList, Transform spawnPoint = null)
    {
        if (runtimeDiceList == null || runtimeDiceList.Count == 0)
        {
            Debug.LogWarning("DiceManager: 运行时骰子列表为空，跳过生成");
            return;
        }

        ThrowDiceBatch(runtimeDiceList, spawnPoint);
    }

    /// <summary>
    /// 停止所有骰子的运动
    /// </summary>
    public void StopAllDice()
    {
        foreach (Dice dice in activeDice)
        {
            if (dice != null)
            {
                dice.StopDice();
            }
        }
    }

    /// <summary>
    /// 重置所有骰子到指定位置
    /// </summary>
    public void ResetAllDice(Vector3 position)
    {
        foreach (Dice dice in activeDice)
        {
            if (dice != null)
            {
                dice.ResetPosition(position);
            }
        }
    }

    /// <summary>
    /// 销毁指定的骰子
    /// </summary>
    public void DestroyDice(Dice dice)
    {
        if (dice != null && activeDice.Contains(dice))
        {
            dice.StopDice();
            dice.IsThrown = false;
            dice.IsInContainer = false;
            dice.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 销毁所有骰子
    /// </summary>
    public void DestroyAllDice()
    {
        // 不再销毁骰子，改为复用同一批实例
        StopAllThrownDice();
        thrownDiceObjects.Clear();
        if (arrangeRoutine != null)
        {
            StopCoroutine(arrangeRoutine);
            arrangeRoutine = null;
        }
    }

    /// <summary>
    /// 获取所有活动的骰子
    /// </summary>
    public List<Dice> GetAllActiveDice()
    {
        // 返回副本以防止外部修改
        return new List<Dice>(activeDice);
    }

    /// <summary>
    /// 获取骰子总数
    /// </summary>
    public int GetDiceCount()
    {
        return activeDice.Count;
    }

    /// <summary>
    /// 检查是否有骰子仍在运动
    /// </summary>
    public bool AreAnyDiceMoving()
    {
        foreach (Dice dice in activeDice)
        {
            if (dice != null && dice.IsThrown)
            {
                return true;
            }
        }
        return false;
    }

    public void ClearThrownDice()
    {
        StopAllThrownDice();
        thrownDiceObjects.Clear();
    }

    public void StopAllThrownDice()
    {
        foreach (var obj in thrownDiceObjects)
        {
            if (obj != null)
            {
                Dice dice = obj.GetComponent<Dice>();
                if (dice != null)
                {
                    dice.StopDice();
                }
            }
        }
    }

    public void StopAllThrownEnemyDice()
    {
        foreach (var obj in thrownEnemyDiceObjects)
        {
            if (obj == null)
            {
                continue;
            }

            Dice dice = obj.GetComponent<Dice>();
            if (dice != null)
            {
                dice.StopDice();
            }
        }
    }

    /// <summary>
    /// 清理无效的骰子引用
    /// </summary>
    public void CleanupInvalidDice()
    {
        activeDice.RemoveAll(dice => dice == null);
        activeEnemyDice.RemoveAll(dice => dice == null);
        thrownDiceObjects.RemoveAll(obj => obj == null);
        thrownEnemyDiceObjects.RemoveAll(obj => obj == null);

        var toRemove = new List<Dice>();
        foreach (var kv in rendererToRuntimeFaceIndexMap)
        {
            if (kv.Key == null)
            {
                toRemove.Add(kv.Key);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            rendererToRuntimeFaceIndexMap.Remove(toRemove[i]);
        }
    }

    private void EnsureDicePool(int desiredCount, Vector3 position, Quaternion rotation)
    {
        while (activeDice.Count < desiredCount)
        {
            var created = CreateDice(position, rotation);
            if (created == null)
            {
                break;
            }
        }

        for (int i = 0; i < activeDice.Count; i++)
        {
            var dice = activeDice[i];
            if (dice == null) continue;

            if (i < desiredCount)
            {
                dice.gameObject.SetActive(true);
                ResetDiceStateForThrow(dice, position, rotation);
            }
            else
            {
                dice.StopDice();
                dice.IsThrown = false;
                dice.IsInContainer = false;
                dice.gameObject.SetActive(false);
            }
        }
    }

    private void EnsureEnemyDicePool(int desiredCount, Vector3 position, Quaternion rotation)
    {
        while (activeEnemyDice.Count < desiredCount)
        {
            var created = CreateEnemyDice(position, rotation);
            if (created == null)
            {
                break;
            }
        }

        for (int i = 0; i < activeEnemyDice.Count; i++)
        {
            var dice = activeEnemyDice[i];
            if (dice == null) continue;

            if (i < desiredCount)
            {
                dice.gameObject.SetActive(true);
                ResetDiceStateForThrow(dice, position, rotation);
            }
            else
            {
                dice.StopDice();
                dice.IsThrown = false;
                dice.IsInContainer = false;
                dice.gameObject.SetActive(false);
            }
        }
    }

    private Dice CreateEnemyDice(Vector3 position, Quaternion rotation = default)
    {
        if (dicePrefab == null)
        {
            Debug.LogError("骰子预制体未设置！");
            return null;
        }

        GameObject diceObj = Instantiate(dicePrefab, position, rotation == default ? Quaternion.identity : rotation);
        Dice dice = diceObj.GetComponent<Dice>();

        if (dice != null)
        {
            activeEnemyDice.Add(dice);
        }
        else
        {
            Debug.LogError("骰子预制体上没有Dice组件！");
        }

        return dice;
    }

    private void ResetDiceStateForThrow(Dice dice, Vector3 position, Quaternion rotation)
    {
        if (dice == null) return;

        dice.SetGhostMode(false);
        dice.ResetPosition(position);
        dice.transform.rotation = rotation;

        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        dice.IsThrown = false;
        dice.IsInContainer = false;
    }

    private void ApplyThrowForces(Dice dice)
    {
        if (dice == null) return;

        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("DiceManager: Dice 缺少 Rigidbody 组件，无法投掷");
            return;
        }

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float randomForceX = Random.Range(minThrowForceX, maxThrowForceX) * (Random.value > 0.5f ? -1f : 1f);
        float randomForceZ = Random.Range(minThrowForceZ, maxThrowForceZ) * (Random.value > 0.5f ? -1f : 1f);

        Vector3 throwForce = new Vector3(randomForceX, throwForceY, randomForceZ);
        rb.AddForce(throwForce, ForceMode.Impulse);

        Vector3 randomTorque = new Vector3(
            Random.Range(-torqueStrength, torqueStrength),
            Random.Range(-torqueStrength, torqueStrength),
            Random.Range(-torqueStrength, torqueStrength)
        );
        rb.AddTorque(randomTorque);

        dice.IsThrown = true;

        // Debug.Log($"DiceManager 投掷: 力度 {throwForce}, 力矩 {randomTorque}");
    }

    private Vector3 GetSpawnPosition(Transform overrideSpawn)
    {
        if (overrideSpawn != null)
        {
            return overrideSpawn.position;
        }

        if (defaultThrowSpawnPoint != null)
        {
            return defaultThrowSpawnPoint.position;
        }

        return Vector3.zero;
    }

    private void SetupDiceVisuals(GameObject diceObj, RuntimeDice runtimeDice)
    {
        if (diceObj == null || runtimeDice == null || runtimeDice.Faces == null || runtimeDice.Faces.Count == 0) 
        {
            Debug.LogWarning("SetupDiceVisuals: Invalid input or empty faces.");
            return;
        }

        Dice dice = diceObj.GetComponent<Dice>();
        if (dice == null) return;
        
        dice.SetRuntimeData(runtimeDice);

        var faces = runtimeDice.Faces;
        var renderers = dice.FaceSpriteRenderers;
        var texts = dice.FaceTextMeshes;

        if (!rendererToRuntimeFaceIndexMap.TryGetValue(dice, out var faceIndexMap) || faceIndexMap == null)
        {
            faceIndexMap = new List<int>();
            rendererToRuntimeFaceIndexMap[dice] = faceIndexMap;
        }
        faceIndexMap.Clear();

        if (renderers.Count == 0)
        {
            Debug.LogWarning($"SetupDiceVisuals: No SpriteRenderers found on Dice {dice.name}");
        }

        // 遍历所有视觉面，将RuntimeDice的数据应用到每一面上
        for (int i = 0; i < renderers.Count; i++)
        {
            // 如果数据面不够，循环使用或保持默认
            int runtimeFaceIndex = (i < faces.Count) ? i : 0;
            var faceData = faces[runtimeFaceIndex];
            faceIndexMap.Add(runtimeFaceIndex);
            
            // 设置Sprite
            if (renderers[i] != null)
            {
                Sprite s = GetSpriteForDice(faceData.color);
                if (s == null)
                {
                    Debug.LogWarning($"SetupDiceVisuals: Missing sprite for color {faceData.color}");
                }
                renderers[i].sprite = s;
            }

            // 设置Text
            if (i < texts.Count && texts[i] != null)
            {
                texts[i].text = faceData.value.ToString();
            }
        }
    }

    public void ApplyRuntimeVisualsToDice(Dice dice, RuntimeDice runtimeDice)
    {
        if (dice == null || runtimeDice == null) return;
        SetupDiceVisuals(dice.gameObject, runtimeDice);
    }

    public int GetMappedRuntimeFaceIndex(Dice dice, int rendererIndex)
    {
        if (dice == null || rendererIndex < 0) return rendererIndex;

        if (rendererToRuntimeFaceIndexMap.TryGetValue(dice, out var faceIndexMap) &&
            faceIndexMap != null &&
            rendererIndex < faceIndexMap.Count)
        {
            return faceIndexMap[rendererIndex];
        }

        return rendererIndex;
    }

    public int GetRuntimeFaceIndexForRenderer(int rendererIndex, int runtimeFaceCount)
    {
        if (rendererIndex < 0) return 0;
        if (runtimeFaceCount <= 0) return 0;
        return rendererIndex < runtimeFaceCount ? rendererIndex : 0;
    }

    public DiceFace GetFaceDataForRendererIndex(RuntimeDice runtimeDice, int rendererIndex)
    {
        if (runtimeDice == null || runtimeDice.Faces == null || runtimeDice.Faces.Count == 0)
        {
            return null;
        }

        int runtimeIndex = GetRuntimeFaceIndexForRenderer(rendererIndex, runtimeDice.Faces.Count);
        return runtimeDice.Faces[runtimeIndex];
    }

    /// <summary>
    /// 更新指定骰子当前面（ActiveFace）的颜色和值，并同步刷新对应的贴图和文本。
    /// </summary>
    public void UpdateActiveFace(Dice dice, DiceColor newColor, int newValue)
    {
        if (dice == null) return;

        var runtime = dice.RuntimeData;
        if (runtime == null) return;

        DiceFace activeFace = runtime.ActiveFace;
        if (activeFace == null) return;

        // 覆盖当前面数据（同时更新原始值，避免后续Reset还原回旧值）
        activeFace.color = newColor;
        activeFace.value = newValue;
        activeFace.originalColor = newColor;
        activeFace.originalValue = newValue;

        // 仅更新当前面的可见贴图和文本
        int faceIndex = runtime.ActiveFaceIndex;
        var renderers = dice.FaceSpriteRenderers;
        var texts = dice.FaceTextMeshes;

        if (faceIndex >= 0 && faceIndex < renderers.Count && renderers[faceIndex] != null)
        {
            CrossfadeFaceSprite(renderers[faceIndex], GetSpriteForDice(newColor));
        }

        if (faceIndex >= 0 && faceIndex < texts.Count && texts[faceIndex] != null)
        {
            texts[faceIndex].text = newValue.ToString();
        }
    }

    /// <summary>
    /// 将敌方场景骰子的物理朝上面同步回 RuntimeDice.ActiveFaceIndex。
    /// 在伤害结算前调用可确保读取到最新顶部点数。
    /// </summary>
    public void SyncEnemyDiceTopFacesToRuntime()
    {
        for (int i = 0; i < activeEnemyDice.Count; i++)
        {
            Dice dice = activeEnemyDice[i];
            if (dice == null || !dice.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (TryGetTopFaceRuntimeIndex(dice, out int topFaceIndex))
            {
                dice.RuntimeData.ActiveFaceIndex = topFaceIndex;
            }
        }
    }

    /// <summary>
    /// 获取当前场景中的敌方骰子运行时数据（按敌方骰子列表顺序）。
    /// 返回的是引用列表，不做深拷贝。
    /// </summary>
    public List<RuntimeDice> GetEnemyRuntimeDiceFromScene()
    {
        List<RuntimeDice> result = new List<RuntimeDice>();

        for (int i = 0; i < activeEnemyDice.Count; i++)
        {
            Dice dice = activeEnemyDice[i];
            if (dice == null || !dice.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (dice.RuntimeData != null)
            {
                result.Add(dice.RuntimeData);
            }
        }

        return result;
    }

    /// <summary>
    /// 根据骰子颜色获取对应的Sprite（用于UI或贴图）
    /// </summary>
    public Sprite GetSpriteForDice(DiceColor diceColor)
    {
        switch (diceColor)
        {
            case DiceColor.Red:
                return redDiceSprite;
            case DiceColor.Yellow:
                return yellowDiceSprite;
            case DiceColor.Blue:
                return blueDiceSprite;
            case DiceColor.Orange:
                return orangeDiceSprite;
            case DiceColor.Green:
                return greenDiceSprite;
            case DiceColor.Purple:
                return purpleDiceSprite;
            case DiceColor.Black:
                return blackDiceSprite;
            default:
                return enemyDiceSprite;
        }
    }

    private void CrossfadeFaceSprite(SpriteRenderer target, Sprite newSprite)
    {
        if (target == null)
            return;

        if (newSprite == null)
        {
            target.sprite = null;
            return;
        }

        // 若已在淡变中，清理旧Tween
        target.DOKill();

        // 如果没有现有精灵或淡变时长为0，直接替换
        if (target.sprite == null || faceSwapFadeDuration <= 0f)
        {
            target.color = new Color(target.color.r, target.color.g, target.color.b, 1f);
            target.sprite = newSprite;
            return;
        }

        // 创建一个临时层用于淡入新图
        var tempObj = new GameObject("FaceSwapTemp");
        tempObj.transform.SetParent(target.transform, false);

        var tempSr = tempObj.AddComponent<SpriteRenderer>();
        tempSr.sprite = newSprite;
        tempSr.sortingLayerID = target.sortingLayerID;
        tempSr.sortingOrder = target.sortingOrder + 1; // 叠在原图之上
        tempSr.color = new Color(target.color.r, target.color.g, target.color.b, 0f);

        // 动画：旧图淡出，新图淡入
        var seq = DOTween.Sequence();
        seq.Join(target.DOFade(0f, faceSwapFadeDuration));
        seq.Join(tempSr.DOFade(1f, faceSwapFadeDuration));
        seq.OnComplete(() =>
        {
            // 完成后将目标替换为新图并复原透明度
            target.sprite = newSprite;
            target.color = new Color(target.color.r, target.color.g, target.color.b, 1f);
            Destroy(tempObj);
        });
    }

    // 渐变贴图生成已弃用，保留空位以便未来恢复

    private void RegisterThrownDice(GameObject diceObj)
    {
        if (diceObj == null) return;
        thrownDiceObjects.Add(diceObj);
        RestartArrangeRoutine();
    }

    private void RestartArrangeRoutine()
    {
        if (arrangeRoutine != null)
        {
            StopCoroutine(arrangeRoutine);
        }
        arrangeRoutine = StartCoroutine(WaitForDiceToSettleAndArrange());
    }

    private void RestartEnemyArrangeRoutine()
    {
        if (enemyArrangeRoutine != null)
        {
            StopCoroutine(enemyArrangeRoutine);
        }
        enemyArrangeRoutine = StartCoroutine(WaitForEnemyDiceToSettleAndArrange());
    }

    private IEnumerator WaitForDiceToSettleAndArrange()
    {
        CleanupInvalidDice();
        EnsureDiceSlots();

        if (diceSlots.Count == 0 || thrownDiceObjects.Count == 0)
        {
            arrangeRoutine = null;
            yield break;
        }

        float stableTimer = 0f;
        WaitForSeconds wait = new WaitForSeconds(settlePollInterval);

        while (true)
        {
            if (AreAllDiceStable())
            {
                stableTimer += settlePollInterval;
                if (stableTimer >= settleStableTime)
                {
                    break;
                }
            }
            else
            {
                stableTimer = 0f;
            }

            yield return wait;
        }

        foreach (var dice in activeDice)
        {
            if (dice != null) dice.SetGhostMode(false);
        }

        List<Dice> orderedDice = GetAliveThrownDice();
        yield return MoveDiceToSlotsSimultaneous(orderedDice);

        arrangeRoutine = null;
    }

    private IEnumerator WaitForEnemyDiceToSettleAndArrange()
    {
        CleanupInvalidDice();
        EnsureDiceSlots();

        if (enemyDiceSlots.Count == 0 || thrownEnemyDiceObjects.Count == 0)
        {
            enemyArrangeRoutine = null;
            IsEnemyThrowing = false;
            OnEnemyDiceArranged?.Invoke();
            yield break;
        }

        float stableTimer = 0f;
        WaitForSeconds wait = new WaitForSeconds(settlePollInterval);

        while (true)
        {
            if (AreAllEnemyDiceStable())
            {
                stableTimer += settlePollInterval;
                if (stableTimer >= settleStableTime)
                {
                    break;
                }
            }
            else
            {
                stableTimer = 0f;
            }

            yield return wait;
        }

        List<Dice> orderedDice = GetAliveThrownEnemyDice();
        yield return MoveEnemyDiceToSlotsSimultaneous(orderedDice);

        enemyArrangeRoutine = null;
        IsEnemyThrowing = false;
        OnEnemyDiceArranged?.Invoke();
    }

    private bool AreAllDiceStable()
    {
        List<Dice> diceList = GetAliveThrownDice();
        if (diceList.Count == 0)
        {
            return false;
        }

        float linearThresholdSqr = settleLinearSpeed * settleLinearSpeed;
        float angularThresholdSqr = settleAngularSpeed * settleAngularSpeed;

        foreach (Dice dice in diceList)
        {
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb == null) continue;

            if (rb.linearVelocity.sqrMagnitude > linearThresholdSqr) return false;
            if (rb.angularVelocity.sqrMagnitude > angularThresholdSqr) return false;
        }

        return true;
    }

    private bool AreAllEnemyDiceStable()
    {
        List<Dice> diceList = GetAliveThrownEnemyDice();
        if (diceList.Count == 0)
        {
            return false;
        }

        float linearThresholdSqr = settleLinearSpeed * settleLinearSpeed;
        float angularThresholdSqr = settleAngularSpeed * settleAngularSpeed;

        foreach (Dice dice in diceList)
        {
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb == null) continue;

            if (rb.linearVelocity.sqrMagnitude > linearThresholdSqr) return false;
            if (rb.angularVelocity.sqrMagnitude > angularThresholdSqr) return false;
        }

        return true;
    }

    private List<Dice> GetAliveThrownDice()
    {
        List<Dice> diceList = new List<Dice>();
        foreach (var obj in thrownDiceObjects)
        {
            if (obj == null) continue;
            Dice dice = obj.GetComponent<Dice>();
            if (dice != null)
            {
                diceList.Add(dice);
            }
        }

        return diceList;
    }

    private List<Dice> GetAliveThrownEnemyDice()
    {
        List<Dice> diceList = new List<Dice>();
        foreach (var obj in thrownEnemyDiceObjects)
        {
            if (obj == null) continue;
            Dice dice = obj.GetComponent<Dice>();
            if (dice != null)
            {
                diceList.Add(dice);
            }
        }

        return diceList;
    }

    private IEnumerator SnapDiceToTopFace(Dice dice)
    {
        if (dice == null) yield break;

        Transform face = FindTopFace(dice);
        if (face == null) yield break;

        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 先把顶面法线对准世界Y，再绕Y轴校正文字，使文字的本地Y方向与世界Z平行
        Quaternion targetRotation = CalculateTargetRotation(dice, face);

        Tween rotateTween = dice.transform.DORotateQuaternion(targetRotation, snapRotationDuration).SetEase(Ease.OutSine);
        yield return rotateTween.WaitForCompletion();

        dice.IsThrown = false;
    }

    private Transform FindTopFace(Dice dice)
    {
        Transform bestFace = null;
        float bestDot = float.NegativeInfinity;

        foreach (var sr in dice.FaceSpriteRenderers)
        {
            if (sr == null) continue;
            Vector3 normal = GetFaceNormal(dice, sr.transform);
            float dot = Vector3.Dot(normal, Vector3.up);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestFace = sr.transform;
            }
        }

        return bestFace;
    }

    private bool TryGetTopFaceRuntimeIndex(Dice dice, out int faceIndex)
    {
        faceIndex = -1;
        if (dice == null || dice.RuntimeData == null || dice.RuntimeData.Faces == null || dice.RuntimeData.Faces.Count == 0)
        {
            return false;
        }

        Transform topFace = FindTopFace(dice);
        if (topFace == null)
        {
            return false;
        }

        for (int i = 0; i < dice.FaceSpriteRenderers.Count; i++)
        {
            var sr = dice.FaceSpriteRenderers[i];
            if (sr != null && sr.transform == topFace)
            {
                if (rendererToRuntimeFaceIndexMap.TryGetValue(dice, out var faceIndexMap) &&
                    faceIndexMap != null &&
                    i >= 0 && i < faceIndexMap.Count)
                {
                    faceIndex = Mathf.Clamp(faceIndexMap[i], 0, dice.RuntimeData.Faces.Count - 1);
                    return true;
                }

                faceIndex = Mathf.Clamp(i, 0, dice.RuntimeData.Faces.Count - 1);
                return true;
            }
        }

        return false;
    }

    private Quaternion CalculateTargetRotation(Dice dice, Transform face)
    {
        Vector3 faceNormal = GetFaceNormal(dice, face);
        Quaternion alignUpRotation = Quaternion.FromToRotation(faceNormal, Vector3.up) * dice.transform.rotation;

        Transform faceText = FindTextOnFace(face);
        Quaternion yawFix;

        if (faceText != null)
        {
            Vector3 localTextUp = dice.transform.InverseTransformDirection(faceText.up);
            Vector3 textUpAfterAlign = alignUpRotation * localTextUp;
            Vector3 projectedTextUp = Vector3.ProjectOnPlane(textUpAfterAlign, Vector3.up);
            if (projectedTextUp.sqrMagnitude < 1e-4f)
            {
                projectedTextUp = Vector3.forward;
            }

            Vector3 desiredTextUp = Vector3.forward; // 世界Z方向
            yawFix = Quaternion.FromToRotation(projectedTextUp.normalized, desiredTextUp);
        }
        else
        {
            Vector3 desiredForward = GetDesiredForwardOnPlane();
            Vector3 currentForwardOnPlane = Vector3.ProjectOnPlane(alignUpRotation * Vector3.forward, Vector3.up).normalized;
            if (currentForwardOnPlane.sqrMagnitude < 1e-4f)
            {
                currentForwardOnPlane = Vector3.forward;
            }
            yawFix = Quaternion.FromToRotation(currentForwardOnPlane, desiredForward);
        }

        return yawFix * alignUpRotation;
    }

    private Vector3 GetFaceNormal(Dice dice, Transform face)
    {
        // 使用面心相对骰子中心的位置向量推导法线方向
        return (face.position - dice.transform.position).normalized;
    }

    private Transform FindTextOnFace(Transform face)
    {
        if (face == null) return null;
        return face.GetComponentInChildren<TextMeshPro>(true)?.transform;
    }

    private Vector3 GetDesiredForwardOnPlane()
    {
        Vector3 forward;
        if (diceSlots.Count > 0 && diceSlots[0] != null)
        {
            forward = diceSlots[0].forward;
        }
        else
        {
            forward = Vector3.forward;
        }

        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 1e-4f)
        {
            forward = Vector3.forward;
        }
        return forward;
    }

    private IEnumerator MoveDiceToSlotsSimultaneous(List<Dice> diceList)
    {
        int count = Mathf.Min(diceList.Count, diceSlots.Count);
        if (count == 0) yield break;

        List<Tween> tweens = new List<Tween>(count);

        for (int i = 0; i < count; i++)
        {
            Dice dice = diceList[i];
            if (dice == null) continue;

            // 如果有目标槽位映射，使用映射的槽位；否则按顺序分配
            int slotIndex = i;
            if (diceTargetSlotMap.ContainsKey(dice))
            {
                slotIndex = diceTargetSlotMap[dice];
            }

            if (slotIndex < 0 || slotIndex >= diceSlots.Count) continue;
            Transform slot = diceSlots[slotIndex];
            if (slot == null) continue;

            PrepareDiceForTween(dice);

            Transform face = FindTopFace(dice);

            // 根据物理结果更新RuntimeData的ActiveFaceIndex
            if (TryGetTopFaceRuntimeIndex(dice, out int topFaceIndex))
            {
                dice.RuntimeData.ActiveFaceIndex = topFaceIndex;
            }

            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;

            Quaternion targetRot = face != null ? CalculateTargetRotation(dice, face) : dice.transform.rotation;
            if (tiltTowardCamera && slotTiltDegrees != 0f)
            {
                targetRot = ApplyCameraTilt(targetRot, targetPos);
            }

            Sequence seq = DOTween.Sequence();
            seq.Join(dice.transform.DOMove(targetPos, moveDuration).SetEase(moveEase));
            seq.Join(dice.transform.DORotateQuaternion(targetRot, moveDuration).SetEase(Ease.OutSine));
            tweens.Add(seq);
        }

        if (tweens.Count == 0) yield break;

        while (AnyTweenRunning(tweens))
        {
            yield return null;
        }

        // 落定后清空目标槽位映射
        diceTargetSlotMap.Clear();

        foreach (Dice dice in diceList)
        {
            if (dice != null)
            {
                dice.IsThrown = false;
                dice.IsInContainer = true;
            }
        }
    }

    private IEnumerator MoveEnemyDiceToSlotsSimultaneous(List<Dice> diceList)
    {
        int count = Mathf.Min(diceList.Count, enemyDiceSlots.Count);
        if (count == 0) yield break;

        List<Tween> tweens = new List<Tween>(count);

        for (int i = 0; i < count; i++)
        {
            Dice dice = diceList[i];
            Transform slot = enemyDiceSlots[i];
            if (dice == null || slot == null) continue;

            PrepareDiceForTween(dice);

            Transform face = FindTopFace(dice);

            if (TryGetTopFaceRuntimeIndex(dice, out int topFaceIndex))
            {
                dice.RuntimeData.ActiveFaceIndex = topFaceIndex;
            }

            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;

            Quaternion targetRot = face != null ? CalculateTargetRotation(dice, face) : dice.transform.rotation;
            if (tiltTowardCamera && slotTiltDegrees != 0f)
            {
                targetRot = ApplyCameraTilt(targetRot, targetPos);
            }

            Sequence seq = DOTween.Sequence();
            seq.Join(dice.transform.DOMove(targetPos, moveDuration).SetEase(moveEase));
            seq.Join(dice.transform.DORotateQuaternion(targetRot, moveDuration).SetEase(Ease.OutSine));
            tweens.Add(seq);
        }

        if (tweens.Count == 0) yield break;

        while (AnyTweenRunning(tweens))
        {
            yield return null;
        }

        foreach (Dice dice in diceList)
        {
            if (dice != null)
            {
                dice.IsThrown = false;
                dice.IsInContainer = false;
            }
        }
    }

    private bool AnyTweenRunning(List<Tween> tweens)
    {
        foreach (var t in tweens)
        {
            if (t != null && t.IsActive() && t.IsPlaying())
            {
                return true;
            }
        }
        return false;
    }

    private void PrepareDiceForTween(Dice dice)
    {
        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // 先设置为非 kinematic 才能设置 velocity
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private Vector3 GetSlotWorldPosition(Transform slot)
    {
        // 槽位已改为3D物体，直接使用世界坐标
        if (slot == null)
        {
            return Vector3.zero;
        }

        return slot.position;
    }

    private Quaternion ApplyCameraTilt(Quaternion baseRotation, Vector3 targetPos)
    {
        Transform cam = overrideCamera != null ? overrideCamera : Camera.main?.transform;
        if (cam == null)
        {
            return baseRotation;
        }

        Vector3 toCam = cam.position - targetPos;
        Vector3 toCamFlat = Vector3.ProjectOnPlane(toCam, Vector3.up);
        if (toCamFlat.sqrMagnitude < 1e-6f)
        {
            toCamFlat = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        }

        if (toCamFlat.sqrMagnitude < 1e-6f)
        {
            return baseRotation;
        }

        Vector3 tiltAxis = Vector3.Cross(Vector3.up, toCamFlat).normalized;
        Quaternion tilt = Quaternion.AngleAxis(slotTiltDegrees, tiltAxis);
        return tilt * baseRotation;
    }

    private void EnsureDiceSlots()
    {
        // 槽位改为手动配置，不再从容器子物体自动收集
        diceSlots.RemoveAll(t => t == null);
        enemyDiceSlots.RemoveAll(t => t == null);

        // 如果enemyDiceSlots为空，则根据diceSlots自动生成对称的槽位
        if (enemyDiceSlots.Count == 0 && diceSlots.Count > 0)
        {
            GameObject enemySlotsContainer = GameObject.Find("EnemyDiceSlots");
            if (enemySlotsContainer == null)
            {
                enemySlotsContainer = new GameObject("EnemyDiceSlots");
                // 沿原点对Z轴进行镜像
                for (int i = 0; i < diceSlots.Count; i++)
                {
                    Transform playerSlot = diceSlots[i];
                    GameObject eSlot = new GameObject("EnemySlot_" + i);
                    eSlot.transform.SetParent(enemySlotsContainer.transform);
                    // 假设屏幕中心Z为某值，这里简单根据Z轴取反，并调整一点高度或其他轴如果需要
                    // 如果相机是从上往下看（俯视），可能只是Z相反
                    Vector3 pos = playerSlot.position;
                    // 以z=0为中心对称，或者根据实际场景调整
                    // 假设玩家骰子在屏幕下方(z < 0)，怪物在上方(z > 0)
                    pos.z = -pos.z; 
                    pos.y = playerSlot.position.y;
                    eSlot.transform.position = pos;
                    eSlot.transform.rotation = playerSlot.rotation;
                    enemyDiceSlots.Add(eSlot.transform);
                }
            }
        }
    }

    #region 排序功能

    /// <summary>
    /// 按颜色排序（同色内部按值升序）
    /// </summary>
    public void SortDiceByColor()
    {
        SortDice((a, b) =>
        {
            var faceA = a.RuntimeData?.ActiveFace;
            var faceB = b.RuntimeData?.ActiveFace;
            if (faceA == null || faceB == null) return 0;

            int colorCompare = faceA.color.CompareTo(faceB.color);
            if (colorCompare != 0) return colorCompare;

            return faceA.value.CompareTo(faceB.value);
        });
    }

    /// <summary>
    /// 按值排序（同值内部按颜色升序）
    /// </summary>
    public void SortDiceByValue()
    {
        SortDice((a, b) =>
        {
            var faceA = a.RuntimeData?.ActiveFace;
            var faceB = b.RuntimeData?.ActiveFace;
            if (faceA == null || faceB == null) return 0;

            int valueCompare = faceA.value.CompareTo(faceB.value);
            if (valueCompare != 0) return valueCompare;

            return faceA.color.CompareTo(faceB.color);
        });
    }

    /// <summary>
    /// 通用排序方法
    /// </summary>
    private void SortDice(System.Comparison<Dice> comparison)
    {
        if (activeDice == null || activeDice.Count == 0)
        {
            Debug.LogWarning("DiceManager: 没有可排序的骰子");
            return;
        }

        // 过滤出有效的、在容器中的骰子
        List<Dice> diceInContainer = activeDice.FindAll(d => d != null && d.IsInContainer);
        if (diceInContainer.Count == 0)
        {
            Debug.LogWarning("DiceManager: 没有可排序的骰子（骰子必须在容器中）");
            return;
        }

        // 排序
        diceInContainer.Sort(comparison);

        // 更新 activeDice 中的顺序
        int containerIndex = 0;
        for (int i = 0; i < activeDice.Count; i++)
        {
            if (activeDice[i] != null && activeDice[i].IsInContainer)
            {
                activeDice[i] = diceInContainer[containerIndex];
                containerIndex++;
            }
        }

        // 重新排列到槽位
        StartCoroutine(RearrangeDiceToSlots(diceInContainer));
    }

    /// <summary>
    /// 将骰子重新排列到槽位
    /// </summary>
    private IEnumerator RearrangeDiceToSlots(List<Dice> sortedDice)
    {
        int count = Mathf.Min(sortedDice.Count, diceSlots.Count);
        if (count == 0) yield break;

        List<Tween> tweens = new List<Tween>(count);

        for (int i = 0; i < count; i++)
        {
            Dice dice = sortedDice[i];
            Transform slot = diceSlots[i];
            if (dice == null || slot == null) continue;

            PrepareDiceForTween(dice);

            Vector3 targetPos = GetSlotWorldPosition(slot) + Vector3.up * slotYOffset;

            // 保持当前旋转，只移动位置
            Quaternion currentRot = dice.transform.rotation;
            if (tiltTowardCamera && slotTiltDegrees != 0f)
            {
                currentRot = ApplyCameraTilt(currentRot, targetPos);
            }

            Sequence seq = DOTween.Sequence();
            seq.Join(dice.transform.DOMove(targetPos, moveDuration * 0.5f).SetEase(moveEase));
            seq.Join(dice.transform.DORotateQuaternion(currentRot, moveDuration * 0.5f).SetEase(Ease.OutSine));
            tweens.Add(seq);
        }

        if (tweens.Count == 0) yield break;

        while (AnyTweenRunning(tweens))
        {
            yield return null;
        }
    }

    #endregion

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (dicePrefab != null && !dicePrefab.GetComponent<Dice>())
        {
            Debug.LogWarning("骰子预制体上没有Dice组件！", dicePrefab);
        }

        EnsureDiceSlots();
    }
    #endif
}