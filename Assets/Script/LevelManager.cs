using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Drag all level prefabs here (Element 0, 1, 2, 3...)")]
    public GameObject[] levelPrefabs;

    [Header("Optional: parent transform for spawned level")]
    public Transform levelParent;

    [Header("Loop back to level 1 after the last level?")]
    public bool loopAfterLastLevel = false;

    private const string LevelKey = "CurrentLevel";
    private const string UnlockedKey = "UnlockedLevel";

    private GameObject currentLevelInstance;

    public int LevelCount => levelPrefabs != null ? levelPrefabs.Length : 0;
    public bool HasActiveLevel => currentLevelInstance != null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Intentionally empty. MenuNavigator decides when a level spawns.
        // (Was: LoadCurrentLevel() — that made the game start mid-level
        //  with no main menu.)
    }

    // ── LEVEL INDEX ───────────────────────────────────────────────────────────

    public int GetCurrentLevelIndex() => PlayerPrefs.GetInt(LevelKey, 0);

    public void SetCurrentLevelIndex(int index)
    {
        PlayerPrefs.SetInt(LevelKey, index);
        PlayerPrefs.Save();
    }

    // ── UNLOCK TRACKING ───────────────────────────────────────────────────────

    /// <summary>Highest level index the player is allowed to enter.</summary>
    public int GetUnlockedLevelIndex() => PlayerPrefs.GetInt(UnlockedKey, 0);

    public bool IsLevelUnlocked(int index) => index >= 0 && index <= GetUnlockedLevelIndex();

    public void UnlockLevel(int index)
    {
        if (index <= GetUnlockedLevelIndex()) return;

        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, LevelCount - 1));
        PlayerPrefs.SetInt(UnlockedKey, clamped);
        PlayerPrefs.Save();

        Debug.Log($"[LevelManager] Unlocked up to level {clamped + 1}");
    }

    // ── LOAD / UNLOAD ─────────────────────────────────────────────────────────

    public void LoadCurrentLevel()
    {
        if (levelPrefabs == null || levelPrefabs.Length == 0)
        {
            Debug.LogError("LevelManager: No level prefabs assigned in the Inspector!");
            return;
        }

        int index = Mathf.Clamp(GetCurrentLevelIndex(), 0, levelPrefabs.Length - 1);

        if (levelPrefabs[index] == null)
        {
            Debug.LogError($"LevelManager: Element {index} in Level Prefabs is EMPTY (None).");
            return;
        }

        // ── 1. Tear down the old level FIRST, completely ──
        UnloadCurrentLevel();

        // ── 2. Spawn the new level. Order inside this Instantiate call:
        //       LevelTargets.Awake()  → pushes targets into the new UIManager
        //       UIManager.Awake()     → claims Instance
        //       …then all Start()s run.
        Transform parent = levelParent != null ? levelParent : transform;
        currentLevelInstance = Instantiate(levelPrefabs[index], parent);
        currentLevelInstance.transform.localPosition = Vector3.zero;

        // ── 3. Start the level clock AFTER the level exists ──
        if (LevelTimer.Instance != null)
            LevelTimer.Instance.StartLevelTimer();

        Debug.Log($"LevelManager: Loaded '{levelPrefabs[index].name}' at index {index}");
    }

    /// <summary>
    /// Destroys the live level immediately and clears the stale UIManager
    /// pointer. DestroyImmediate (not Destroy) matters here: Destroy is
    /// deferred to end-of-frame, so the OLD UIManager.Awake-set Instance
    /// would still be alive when the NEW prefab spawns.
    /// </summary>
    public void UnloadCurrentLevel()
    {
        Time.timeScale = 1f;

        if (LevelTimer.Instance != null)
            LevelTimer.Instance.StopLevelTimer();

        if (currentLevelInstance != null)
        {
            DestroyImmediate(currentLevelInstance);
            currentLevelInstance = null;
        }

        UIManager.Instance = null;
    }

    // ── NAVIGATION ────────────────────────────────────────────────────────────

    public void NextLevel()
    {
        int index = GetCurrentLevelIndex() + 1;

        if (index >= levelPrefabs.Length)
        {
            if (loopAfterLastLevel) index = 0;
            else
            {
                Debug.Log("[LevelManager] Last level complete — returning to level select.");
                if (MenuNavigator.Instance != null)
                    MenuNavigator.Instance.OpenLevelSelect();
                return;
            }
        }

        UnlockLevel(index);
        SetCurrentLevelIndex(index);
        LoadCurrentLevel();
    }

    public void RetryLevel() => LoadCurrentLevel();

    public void GoToLevel(int index)
    {
        SetCurrentLevelIndex(Mathf.Clamp(index, 0, Mathf.Max(0, LevelCount - 1)));
        LoadCurrentLevel();
    }
}