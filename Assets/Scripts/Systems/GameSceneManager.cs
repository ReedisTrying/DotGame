using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 统一管理全局场景跳转，避免在各业务脚本中分散写死场景名。
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }
    public static NodeType CurrentBattleNodeType { get; private set; } = NodeType.Battle;

    [Header("Scene Names")]
    [SerializeField] private string startMenuSceneName = "StartMenu";
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private string eventSceneName = "Event";
    [SerializeField] private string storeSceneName = "Store";
    [SerializeField] private string forgeSceneName = "Forge";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("GameSceneManager");
        go.AddComponent<GameSceneManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void LoadStartMenu()
    {
        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.startMenuSceneName);
            return;
        }

        SceneManager.LoadScene("StartMenu");
    }

    public static void LoadMap()
    {
        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.mapSceneName);
            return;
        }

        SceneManager.LoadScene("Map");
    }

    public static void LoadBattle()
    {
        LoadBattle(NodeType.Battle);
    }

    public static void LoadBattle(NodeType battleNodeType)
    {
        CurrentBattleNodeType = NormalizeBattleNodeType(battleNodeType);

        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.battleSceneName);
            return;
        }

        SceneManager.LoadScene("Battle");
    }

    public static void LoadEvent()
    {
        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.eventSceneName);
            return;
        }

        SceneManager.LoadScene("Event");
    }

    public static void LoadStore()
    {
        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.storeSceneName);
            return;
        }

        SceneManager.LoadScene("Store");
    }

    public static void LoadForge()
    {
        if (Instance != null)
        {
            Instance.LoadSceneInternal(Instance.forgeSceneName);
            return;
        }

        SceneManager.LoadScene("Forge");
    }

    public static void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void LoadByNodeType(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Battle:
            case NodeType.Elite:
            case NodeType.Boss:
                LoadBattle(nodeType);
                break;
            case NodeType.Event:
                LoadEvent();
                break;
            case NodeType.Shop:
                LoadStore();
                break;
            case NodeType.Forge:
                LoadForge();
                break;
            case NodeType.Start:
            default:
                LoadMap();
                break;
        }
    }

    private void LoadSceneInternal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[GameSceneManager] Scene name is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static NodeType NormalizeBattleNodeType(NodeType nodeType)
    {
        return nodeType switch
        {
            NodeType.Elite => NodeType.Elite,
            NodeType.Boss => NodeType.Boss,
            _ => NodeType.Battle
        };
    }
}