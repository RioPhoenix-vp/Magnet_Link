using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Central scene-loading singleton.
///
/// Win  → LoadNextLevel()   — loads build index + 1, or lastLevelSceneName on final level.
/// Lose → LoadGameOver()    — shows the Game Over panel; does NOT load a scene.
///                            The player then taps Retry  → RestartLevel()
///                                              or Menu   → LoadScene(mainMenuSceneName)
/// Retry → RestartLevel()   — reloads the current scene.
///
/// SETUP
/// ─────
/// 1. Add to a persistent GameObject in every level scene (e.g. GameManager).
/// 2. File → Build Settings: add all scenes in order (0=Menu, 1=L1, 2=L2 …).
/// 3. Fill "Last Level Scene Name" and "Main Menu Scene Name" in the Inspector.
/// 4. Drag your Win and Game Over panel GameObjects into the Inspector slots,
///    OR leave them empty and handle panels only in UIManager — both work.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [Header("Scene Names")]
    [Tooltip("Scene to load after the player beats the final level.")]
    [SerializeField] private string lastLevelSceneName = "MainMenu";

    [Tooltip("Scene to load when the player taps the Main Menu button.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Transition")]
    [Tooltip("Seconds to wait before loading the next scene after a WIN. " +
             "Use this for a fade-out. Game Over does NOT auto-load — " +
             "the player must press Retry or Menu.")]
    [SerializeField] private float winTransitionDelay = 1.5f;

    // Prevents double-calls
    private bool isLoading = false;

    // ── LIFECYCLE ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── WIN ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by UIManager when all ball targets are met.
    /// Shows the win panel (via UIManager) then loads the next scene after a delay.
    /// </summary>
    public void LoadNextLevel()
    {
        if (isLoading) return;
        isLoading = true;

        // UIManager already shows the win panel and freezes time — we just schedule the load.
        int next = SceneManager.GetActiveScene().buildIndex + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"[SceneLoader] Win → loading scene index {next}");
            StartCoroutine(LoadAfterDelay(next, winTransitionDelay));
        }
        else
        {
            Debug.Log($"[SceneLoader] Final level complete → '{lastLevelSceneName}'");
            StartCoroutine(LoadAfterDelay(lastLevelSceneName, winTransitionDelay));
        }
    }

    // ── GAME OVER ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LevelTimer when the countdown reaches 0.
    /// Shows the Game Over panel — does NOT load any scene automatically.
    /// The player then presses Retry or Menu buttons.
    /// </summary>
    public void LoadGameOver()
    {
        if (isLoading) return;
        // Do NOT set isLoading = true here — we are not loading anything,
        // just surfacing the panel. Retry/Menu buttons handle the actual load.

        Debug.Log("[SceneLoader] Time's up → showing Game Over panel.");

        // Ask UIManager to show the panel (it also freezes Time.timeScale).
        if (UIManager.Instance != null)
            UIManager.Instance.ShowGameOverPanel();
        else
            Debug.LogWarning("[SceneLoader] UIManager not found — can't show Game Over panel.");
    }

    // ── RETRY / MENU BUTTONS ─────────────────────────────────────────────────

    /// <summary>
    /// Wire to the RETRY button on the Game Over panel.
    /// Reloads the current scene from scratch.
    /// </summary>
    public void RestartLevel()
    {
        if (isLoading) return;
        isLoading = true;

        Time.timeScale = 1f;   // unfreeze before loading
        int current = SceneManager.GetActiveScene().buildIndex;
        Debug.Log($"[SceneLoader] Retry → reloading scene {current}");
        StartCoroutine(LoadAfterDelay(current, 0f));
    }

    /// <summary>
    /// Wire to the MAIN MENU button on Win / Game Over panels.
    /// </summary>
    public void LoadMainMenu()
    {
        if (isLoading) return;
        isLoading = true;

        Time.timeScale = 1f;
        Debug.Log($"[SceneLoader] Going to main menu → '{mainMenuSceneName}'");
        StartCoroutine(LoadAfterDelay(mainMenuSceneName, 0f));
    }

    // ── COROUTINES ────────────────────────────────────────────────────────────

    private IEnumerator LoadAfterDelay(int buildIndex, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    private IEnumerator LoadAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}