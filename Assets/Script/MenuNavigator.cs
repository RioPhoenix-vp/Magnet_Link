using UnityEngine;

/// <summary>
/// Switches between Main Menu, Level Select, and Gameplay.
/// Lives on a persistent object OUTSIDE all level prefabs.
/// </summary>
public class MenuNavigator : MonoBehaviour
{
    public static MenuNavigator Instance;

    [Header("Canvases / Panels")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject levelSelectCanvas;
    [SerializeField] private GameObject gameplayRoot;

    [Header("Level Select")]
    [SerializeField] private LevelSelectMenu levelSelectMenu;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        OpenMainMenu();
    }

    public void OpenMainMenu()
    {
        ClearActiveLevel();

        Show(mainMenuCanvas, true);
        Show(levelSelectCanvas, false);
        Show(gameplayRoot, false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    public void OpenLevelSelect()
    {
        ClearActiveLevel();

        Show(mainMenuCanvas, false);
        Show(levelSelectCanvas, true);
        Show(gameplayRoot, false);

        // ★ Rebuild every open — unlock states and layout always current.
        if (levelSelectMenu != null)
            levelSelectMenu.RefreshButtons();

        // Same track continues — PlayMenuMusic no-ops if it's already running,
        // so there's no restart when moving menu → level select.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    /// <summary>Called by a level button. Shows gameplay, then loads the level.</summary>
    public void OpenGameplay(int levelIndex)
    {
        Show(mainMenuCanvas, false);
        Show(levelSelectCanvas, false);
        Show(gameplayRoot, true);   // must be active BEFORE the level spawns into it

        if (LevelManager.Instance != null)
            LevelManager.Instance.GoToLevel(levelIndex);
        else
            Debug.LogError("[MenuNavigator] LevelManager.Instance is null.");

        // Menu music stops when gameplay begins.
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();
    }

    /// <summary>Destroys the live level so menus never sit over a running game.</summary>
    private void ClearActiveLevel()
    {
        Time.timeScale = 1f;

        if (LevelManager.Instance != null)
            LevelManager.Instance.UnloadCurrentLevel();
    }

    private void Show(GameObject go, bool state)
    {
        if (go != null) go.SetActive(state);
    }
}