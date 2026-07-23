using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Main level countdown timer — starts automatically when the scene loads,
/// counts down to zero, then triggers game-over.
///
/// Setup in the Inspector
/// ───────────────────────
/// 1. Create a TMP_Text GameObject anywhere in your Canvas hierarchy.
///    It can be a direct child of the Canvas, an Image, or any panel — 
///    parent/child nesting does not matter.
///    Recommended name: "LevelTimerText"
///
/// 2. Attach this script to any persistent GameObject (e.g. UIManager,
///    Canvas, or a dedicated "LevelTimer" empty GameObject).
///
/// 3. Drag the TMP_Text into the "Level Timer Text" slot in the Inspector.
///
/// 4. Set "Level Duration" to however many seconds the level allows
///    (e.g. 120 for 2 minutes).
///
/// The timer is completely independent of the dust-box timer in UIManager.
/// It cannot be paused by Time Freeze (unless you call PauseLevelTimer() from
/// PowerUpManager, which is optional).
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance;

    [Header("UI")]
    [Tooltip("Drag the TMP_Text that shows the main level countdown here.")]
    [SerializeField] private TMP_Text levelTimerText;

    [Tooltip("Fallback: if the slot above is empty, the script searches for a " +
             "GameObject with this exact name and grabs its TMP_Text component.")]
    [SerializeField] private string levelTimerTextObjectName = "LevelTimerText";

    [Header("Duration")]
    [Tooltip("Total seconds the player has to complete the level.")]
    [SerializeField] private float levelDuration = 120f;

    // ── Private state ─────────────────────────────────────────────────────────
    private float remainingTime;
    private bool timerRunning = false;
    private Coroutine timerCoroutine;

    /// <summary>Seconds left on the level timer (read-only).</summary>
    public float RemainingTime => remainingTime;

    /// <summary>True while the level clock is counting down.</summary>
    public bool IsRunning => timerRunning;

    // ── LIFECYCLE ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (levelTimerText == null)
        {
            GameObject obj = GameObject.Find(levelTimerTextObjectName);
            if (obj != null)
                levelTimerText = obj.GetComponent<TMP_Text>();

            if (levelTimerText == null)
                Debug.LogWarning($"[LevelTimer] TMP_Text not found. " +
                                 $"Assign it in the Inspector or name your GameObject '{levelTimerTextObjectName}'.");
        }

        // LevelManager.LoadCurrentLevel() starts the clock now.
        // (Was: StartLevelTimer(); — that ran the timer on the main menu.)
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts (or restarts) the level countdown from the full duration.
    /// Called automatically in Start(); call again to restart mid-level.
    /// </summary>
    public void StartLevelTimer()
    {
        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        remainingTime = levelDuration;
        timerRunning = true;
        timerCoroutine = StartCoroutine(RunLevelTimer());

        Debug.Log($"[LevelTimer] Started — {levelDuration}s");
    }

    /// <summary>
    /// Pauses the countdown.  Call from PowerUpManager if you ever want a
    /// power-up to freeze the main level clock too.
    /// </summary>
    public void PauseLevelTimer()
    {
        if (!timerRunning) return;
        timerRunning = false;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        Debug.Log("[LevelTimer] Paused");
    }

    /// <summary>Resumes a paused countdown from where it left off.</summary>
    public void ResumeLevelTimer()
    {
        if (timerRunning) return;
        if (remainingTime <= 0f) return;

        timerRunning = true;
        timerCoroutine = StartCoroutine(RunLevelTimer());

        Debug.Log("[LevelTimer] Resumed");
    }

    /// <summary>
    /// Adds extra seconds to the current countdown — used by the Timer Orb power-up.
    /// Safe to call whether the timer is running, paused, or even expired
    /// (calling it after expiry restarts the countdown from the bonus amount).
    /// </summary>
    public void AddTime(float seconds)
    {
        remainingTime += seconds;

        // If the timer had already expired, restart the coroutine so it counts down again.
        if (!timerRunning && remainingTime > 0f)
        {
            timerRunning = true;
            timerCoroutine = StartCoroutine(RunLevelTimer());
        }

        Debug.Log($"[LevelTimer] +{seconds}s added — remaining: {remainingTime:F1}s");
    }

    /// <summary>Stops the timer and clears the display (e.g. on level complete).</summary>
    public void StopLevelTimer()
    {
        timerRunning = false;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        SafeSet("");
        Debug.Log("[LevelTimer] Stopped");
    }

    // ── COROUTINE ─────────────────────────────────────────────────────────────

    private IEnumerator RunLevelTimer()
    {
        while (remainingTime > 0f && timerRunning)
        {
            UpdateDisplay(remainingTime);
            yield return null;               // refresh every frame
            remainingTime -= Time.deltaTime;
        }

        remainingTime = 0f;
        UpdateDisplay(0f);
        timerRunning = false;

        OnLevelTimerExpired();
    }

    // ── DISPLAY ───────────────────────────────────────────────────────────────

    private void UpdateDisplay(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        SafeSet($"{mins:0}:{secs:00}");
    }

    private void SafeSet(string value)
    {
        // The timer text may live inside a level prefab, which is destroyed on
        // every swap. Re-find it lazily instead of holding a dead reference.
        if (levelTimerText == null)
        {
            GameObject obj = GameObject.Find(levelTimerTextObjectName);
            if (obj != null)
                levelTimerText = obj.GetComponent<TMP_Text>();
        }

        if (levelTimerText != null)
            levelTimerText.text = value;
    }

    // ── GAME OVER ─────────────────────────────────────────────────────────────

    private void OnLevelTimerExpired()
    {
        SafeSet("0:00");
        Debug.Log("[LevelTimer] Time's up — GAME OVER");

        // Prefab-swap architecture — no scene load. Show the panel that lives
        // inside the current level prefab.
        if (UIManager.Instance != null)
            UIManager.Instance.ShowGameOverPanel();
        else
            Debug.LogWarning("[LevelTimer] UIManager.Instance is null — no game over panel shown.");
    }
}