using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // ── Rewards granted once when this level is completed ──
    [Header("Reward")]
    [SerializeField] private int coinReward = 50;
    [Tooltip("Base XP per win. ProfileManager scales this up 15% per player level.")]
    [SerializeField] private int xpReward = 100;
    private bool levelRewardGiven = false;

    [Header("Ball Targets")]
    [SerializeField] private int redTarget = 40;
    [SerializeField] private int blueTarget = 30;
    [SerializeField] private int greenTarget = 20;

    private int redCount = 0;
    private int blueCount = 0;
    private int greenCount = 0;

    private TMP_Text redText;
    private TMP_Text blueText;
    private TMP_Text greenText;

    private bool targetsApplied = false;

    // ─────────────────────────────────────────────────────────────
    // WIN / GAME OVER PANELS
    // ─────────────────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject gameWinPanel;
    [SerializeField] private GameObject gameOverPanel;

    // ─────────────────────────────────────────────────────────────
    // BOX TIMER
    // ─────────────────────────────────────────────────────────────
    [Header("Box Timer UI")]
    [SerializeField] private TMP_Text timerText;

    [SerializeField] private string timerTextObjectName = "BoxTimerText";

    private Coroutine timerCoroutine;
    private float remainingTime = 0f;
    private bool timerRunning = false;

    public bool IsBoxTimerRunning => timerRunning;

    // ─────────────────────────────────────────────────────────────
    // AWAKE
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Each level prefab carries its own UIManager — newest one wins.
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    // START
    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        EnsureBallTexts();

        if (redText == null) Debug.LogError("❌ Red TMP Text not found!");
        if (blueText == null) Debug.LogError("❌ Blue TMP Text not found!");
        if (greenText == null) Debug.LogError("❌ Green TMP Text not found!");

        // Hide panels initially
        if (gameWinPanel != null)
            gameWinPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Auto-find timer text if not assigned
        if (timerText == null)
        {
            GameObject timerObj = GameObject.Find(timerTextObjectName);

            if (timerObj != null)
                timerText = timerObj.GetComponent<TMP_Text>();

            if (timerText == null)
                Debug.LogWarning($"⚠️ Timer TMP_Text not found.");
        }

        SafeSet(timerText, "");

        // If LevelTargets already pushed values in Awake, don't clobber them.
        if (!targetsApplied)
            Debug.LogWarning("[UIManager] No LevelTargets on this prefab — using Inspector defaults.");

        UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    // FIND TMP
    // ─────────────────────────────────────────────────────────────
    private TMP_Text FindTextInChild(string parentName)
    {
        GameObject obj = GameObject.Find(parentName);

        if (obj == null)
        {
            Debug.LogError($"❌ Could not find GameObject named: {parentName}");
            return null;
        }

        TMP_Text tmp = obj.GetComponentInChildren<TMP_Text>();

        if (tmp == null)
            Debug.LogError($"❌ No TMP_Text found inside: {parentName}");

        return tmp;
    }

    /// <summary>
    /// Finds the score texts if they haven't been located yet. Safe to call
    /// from Start() or from SetTargets() (which a LevelConfig may call before
    /// this UIManager's own Start() has run).
    /// </summary>
    private void EnsureBallTexts()
    {
        if (redText == null) redText = FindTextInChild("redBallBtn");
        if (blueText == null) blueText = FindTextInChild("blueBallBtn");
        if (greenText == null) greenText = FindTextInChild("GreenBallBtn");
    }

    // ─────────────────────────────────────────────────────────────
    // BALL TRACKING
    // ─────────────────────────────────────────────────────────────
    public void AddBall(BallColor color)
    {
        switch (color)
        {
            case BallColor.Red:
                if (redCount < redTarget)
                    redCount++;
                break;

            case BallColor.Blue:
                if (blueCount < blueTarget)
                    blueCount++;
                break;

            case BallColor.Green:
                if (greenCount < greenTarget)
                    greenCount++;
                break;
        }

        UpdateUI();
        CheckWinCondition();
    }

    private void UpdateUI()
    {
        SafeSet(redText, (redTarget - redCount).ToString());
        SafeSet(blueText, (blueTarget - blueCount).ToString());
        SafeSet(greenText, (greenTarget - greenCount).ToString());
    }

    private void SafeSet(TMP_Text t, string val)
    {
        if (t != null)
            t.text = val;
    }

    /// <summary>
    /// Sets this level's ball targets and resets counts. Called by LevelConfig
    /// on each level load, or any time you want to change targets at runtime.
    /// </summary>
    public void SetTargets(int r, int b, int g)
    {
        redTarget = r;
        blueTarget = b;
        greenTarget = g;

        redCount = 0;
        blueCount = 0;
        greenCount = 0;

        // Make sure the text refs exist before refreshing — LevelConfig may
        // call this before UIManager.Start() has run.
        EnsureBallTexts();
        UpdateUI();
    }

    /// <summary>
    /// Called by this prefab's own LevelTargets during Awake — before any
    /// Start() runs. Sets targets, zeroes counts, re-arms the reward guard,
    /// and locks out Inspector defaults.
    /// </summary>
    public void ApplyLevelTargets(int r, int b, int g)
    {
        redTarget = r;
        blueTarget = b;
        greenTarget = g;

        redCount = 0;
        blueCount = 0;
        greenCount = 0;

        levelRewardGiven = false;
        targetsApplied = true;

        EnsureBallTexts();
        UpdateUI();
    }

    /// <summary>
    /// Called by LevelManager when swapping level prefabs (no scene reload).
    /// Resets counters, hides panels, clears the box timer, and re-arms the
    /// reward guard so the next level can award its rewards.
    /// </summary>
    public void ResetForNewLevel()
    {
        redCount = 0;
        blueCount = 0;
        greenCount = 0;

        levelRewardGiven = false;

        StopBoxTimer();

        if (gameWinPanel != null)
            gameWinPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        EnsureBallTexts();
        UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    // WIN CONDITION
    // ─────────────────────────────────────────────────────────────
    private void CheckWinCondition()
    {
        if (redCount >= redTarget &&
            blueCount >= blueTarget &&
            greenCount >= greenTarget)
        {
            Debug.Log("🎉 LEVEL COMPLETE!");

            // ── Grant rewards once (coins + profile XP), persistently ──
            if (!levelRewardGiven)
            {
                levelRewardGiven = true;

                if (CoinManager.Instance != null)
                {
                    CoinManager.Instance.AddCoins(coinReward);
                    Debug.Log($"[UIManager] Awarded {coinReward} coins. Total: {CoinManager.Instance.Coins}");

                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayCoins();
                }
                else Debug.LogWarning("[UIManager] CoinManager.Instance is null — coins not awarded.");

                if (ProfileManager.Instance != null)
                {
                    int grantedXP = ProfileManager.Instance.AddScaledXP(xpReward);
                    Debug.Log($"[UIManager] Awarded {grantedXP} XP (base {xpReward}, scaled by level). " +
                              $"Level {ProfileManager.Instance.Level}, " +
                              $"XP {ProfileManager.Instance.CurrentXP}/{ProfileManager.Instance.XPForNextLevel}");
                }
                else Debug.LogWarning("[UIManager] ProfileManager.Instance is null — XP not awarded.");
            }

            StopBoxTimer();

            if (LevelTimer.Instance != null)
                LevelTimer.Instance.StopLevelTimer();

            ShowWinPanel();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PANELS
    // ─────────────────────────────────────────────────────────────
    public void ShowWinPanel()
    {
        if (gameWinPanel != null)
            gameWinPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    public void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // BOX TIMER API
    // ─────────────────────────────────────────────────────────────
    public void StartBoxTimer(float duration)
    {
        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        remainingTime = duration;
        timerRunning = true;

        timerCoroutine = StartCoroutine(RunTimer());
    }

    public void StopBoxTimer()
    {
        timerRunning = false;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        SafeSet(timerText, "");
    }

    public void PauseBoxTimer()
    {
        if (!timerRunning)
            return;

        timerRunning = false;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        Debug.Log("❄️ Box timer PAUSED");
    }

    public void ResumeBoxTimer()
    {
        if (timerRunning)
            return;

        if (remainingTime <= 0f)
            return;

        timerRunning = true;
        timerCoroutine = StartCoroutine(RunTimer());

        Debug.Log("❄️ Box timer RESUMED");
    }

    // ─────────────────────────────────────────────────────────────
    // TIMER
    // ─────────────────────────────────────────────────────────────
    private IEnumerator RunTimer()
    {
        while (remainingTime > 0f && timerRunning)
        {
            UpdateTimerDisplay(remainingTime);

            yield return null;

            remainingTime -= Time.deltaTime;
        }

        remainingTime = 0f;

        UpdateTimerDisplay(0f);

        timerRunning = false;

        OnTimerExpired();
    }

    private void UpdateTimerDisplay(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);

        SafeSet(timerText, $"{mins:0}:{secs:00}");
    }

    private void OnTimerExpired()
    {
        // The dust-box timer expiring is a pressure mechanic only —
        // it does NOT cause game over. Game over is handled exclusively
        // by LevelTimer → SceneLoader.LoadGameOver() → ShowGameOverPanel().
        Debug.Log("⏰ Dust-box timer expired — next dust box now unlocked.");

        SafeSet(timerText, "0:00");
    }
}