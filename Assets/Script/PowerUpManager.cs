using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Central controller for all four power-ups.
///
/// Wire each orb Button's onClick() in the Inspector:
///   RainbowOrbBtn  → UseRainbowBall()
///   TimerOrbBtn    → UseTimeFreeze()
///   BoomOrbBtn     → UseColorBomb()
///   MagnetOrbBtn   → UseOverloadMode()
///
/// Wire each Buy Button's onClick():
///   RainbowORB / Buy  → BuyRainbow()
///   MagnetOrb  / Buy  → BuyOverload()
///   TimerOrb   / Buy  → BuyTimer()
///   BoomOrb    / Buy  → BuyBomb()
///
/// STOCK SYSTEM
/// ────────────
/// Each power-up starts with a configurable number of charges (startingStock).
/// The No.ofPowerups TMP_Text shows the current stock (e.g. "x3").
/// Using the power-up decrements stock. At zero stock the main button is
/// disabled and only the Buy button can restore it.
///
/// BUY-BACK
/// ────────
/// Tapping Buy deducts coinCost coins via SetCoins() and restores 1 charge,
/// re-enabling the main button if stock was zero.
/// The Buy button itself is hidden when stock is above zero (optional: always show it).
///
/// UNLOCK SYSTEM
/// ─────────────
/// Each power-up has an unlockLevel (LevelManager array index, 0-based).
/// If the current level index < unlockLevel the main button and Buy button are
/// both non-interactable and the lockOverlay is shown. Because levels are now
/// swapped as PREFABS within one scene, LevelManager calls RefreshUnlocks() on
/// each level load so orbs unlock the instant the player reaches their index.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance;

    // ── SCENE REFERENCES ──────────────────────────────────────────────────────
    [Header("Scene References")]
    [SerializeField] private MagnetStick magnetStick;
    [SerializeField] private RainbowBallSpawner rainbowBallSpawner;

    // ── TIME FREEZE / TIMER ORB ───────────────────────────────────────────────
    [Header("Timer Orb Settings")]
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private float timeBonusSeconds = 15f;
    [SerializeField] private float freezeOverlayDuration = 2f;
    [SerializeField] private Image freezeOverlayImage;
    [SerializeField] private Button timeFreezeButton;

    // ── COLOR BOMB ────────────────────────────────────────────────────────────
    [Header("Color Bomb Settings")]
    [SerializeField] private GameObject colorBombPrefab;
    [SerializeField] private Camera gameCamera;
    [SerializeField] private LayerMask arenaTapLayer;
    [SerializeField] private float bombSpawnHeight = 8f;
    [SerializeField] private Button colorBombButton;
    private bool bombArmed = false;

    // ── OVERLOAD MODE ─────────────────────────────────────────────────────────
    [Header("Overload Mode Settings")]
    [SerializeField] private float overloadDuration = 8f;
    [SerializeField] private float overloadRadiusMultiplier = 2f;
    [SerializeField] private float overloadSpeedMultiplier = 2f;
    [SerializeField] private int overloadMaxBalls = 20;
    [SerializeField] private Button overloadButton;

    // ── RAINBOW BALL ──────────────────────────────────────────────────────────
    [Header("Rainbow Ball Settings")]
    [SerializeField] private Button rainbowButton;

    // ── COOLDOWN UI ───────────────────────────────────────────────────────────
    [Header("Cooldown Fill Images (Filled / Radial360 per button)")]
    [SerializeField] private Image timeFreezeCD;
    [SerializeField] private Image colorBombCD;
    [SerializeField] private Image overloadCD;
    [SerializeField] private Image rainbowCD;

    [Header("Cooldown Durations (seconds)")]
    [SerializeField] private float timeFreezeCooldown = 30f;
    [SerializeField] private float colorBombCooldown = 25f;
    [SerializeField] private float overloadCooldown = 40f;
    [SerializeField] private float rainbowBallCooldown = 20f;

    // ═════════════════════════════════════════════════════════════════════════
    //  STOCK SYSTEM
    // ═════════════════════════════════════════════════════════════════════════

    [Header("─── Starting Stock (charges per power-up) ─────────────────")]
    [Tooltip("How many uses each power-up starts the level with.")]
    [SerializeField] private int rainbowStartStock = 3;
    [SerializeField] private int timerStartStock = 3;
    [SerializeField] private int bombStartStock = 3;
    [SerializeField] private int overloadStartStock = 3;

    [Header("No. of Powerups Labels (No.ofPowerups TMP_Text per orb)")]
    [Tooltip("Drag the No.ofPowerups TMP_Text child of RainbowORB here.")]
    [SerializeField] private TMP_Text rainbowStockLabel;
    [Tooltip("Drag the No.ofPowerups TMP_Text child of TimerOrb here.")]
    [SerializeField] private TMP_Text timerStockLabel;
    [Tooltip("Drag the No.ofPowerups TMP_Text child of BoomOrb here.")]
    [SerializeField] private TMP_Text bombStockLabel;
    [Tooltip("Drag the No.ofPowerups TMP_Text child of MagnetOrb here.")]
    [SerializeField] private TMP_Text overloadStockLabel;

    // Runtime stock
    private int rainbowStock;
    private int timerStock;
    private int bombStock;
    private int overloadStock;

    // ═════════════════════════════════════════════════════════════════════════
    //  BUY-BACK SYSTEM
    // ═════════════════════════════════════════════════════════════════════════

    [Header("─── Buy-Back ────────────────────────────────────────────────")]
    [Tooltip("Coin cost to buy 1 extra charge of any power-up.")]
    [SerializeField] private int coinCostPerCharge = 50;

    [Tooltip("Fallback coin count used ONLY if no CoinManager exists in the scene. " +
             "When a CoinManager is present it is the single source of truth and this value is ignored.")]
    [SerializeField] private int playerCoins = 200;

    // True once we've confirmed a CoinManager exists and subscribed to it.
    private bool usingCoinManager = false;

    [Header("Buy Buttons (Buy child of each orb)")]
    [Tooltip("Drag the Buy Button child of RainbowORB here.")]
    [SerializeField] private Button rainbowBuyButton;
    [Tooltip("Drag the Buy Button child of TimerOrb here.")]
    [SerializeField] private Button timerBuyButton;
    [Tooltip("Drag the Buy Button child of BoomOrb here.")]
    [SerializeField] private Button bombBuyButton;
    [Tooltip("Drag the Buy Button child of MagnetOrb here.")]
    [SerializeField] private Button overloadBuyButton;

    // ═════════════════════════════════════════════════════════════════════════
    //  UNLOCK SYSTEM
    // ═════════════════════════════════════════════════════════════════════════

    [Header("─── Power-Up Unlock Levels (LevelManager array index) ──────")]
    [Tooltip("0-based level index from LevelManager (Element 0 = Level 1, " +
             "Element 1 = Level 2, …). The orb unlocks when the current level " +
             "index is >= this value. Set 0 to unlock from the very first level.")]
    [SerializeField] private int rainbowUnlockLevel = 0;
    [SerializeField] private int timerUnlockLevel = 1;
    [SerializeField] private int bombUnlockLevel = 2;
    [SerializeField] private int overloadUnlockLevel = 3;

    [Header("Lock Overlays (shown when locked)")]
    [SerializeField] private GameObject rainbowLockOverlay;
    [SerializeField] private GameObject timerLockOverlay;
    [SerializeField] private GameObject bombLockOverlay;
    [SerializeField] private GameObject overloadLockOverlay;

    [Header("Lock Icons (optional RawImage inside overlay — lock icon)")]
    [SerializeField] private RawImage rainbowLockLabel;
    [SerializeField] private RawImage timerLockLabel;
    [SerializeField] private RawImage bombLockLabel;
    [SerializeField] private RawImage overloadLockLabel;

    [Header("Orb Art (the RawImage child of each orb — faded when locked)")]
    [Tooltip("Drag the orb's main RawImage child here. It is dimmed to Locked Fade Alpha while locked, full opacity when unlocked.")]
    [SerializeField] private RawImage rainbowOrbArt;
    [SerializeField] private RawImage timerOrbArt;
    [SerializeField] private RawImage bombOrbArt;
    [SerializeField] private RawImage overloadOrbArt;

    [Tooltip("Alpha (0–1) applied to the orb art while the power-up is locked. 0 = invisible, 1 = full.")]
    [Range(0f, 1f)]
    [SerializeField] private float lockedFadeAlpha = 0.35f;

    // ── PRIVATE STATE ─────────────────────────────────────────────────────────
    private bool freezeActive = false;
    private bool overloadActive = false;

    private bool freezeOnCD = false;
    private bool bombOnCD = false;
    private bool overloadOnCD = false;
    private bool rainbowOnCD = false;

    private float origRadius;
    private float origSpeed;
    private int origMaxBalls;

    // ── LIFECYCLE ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (levelTimer == null)
        {
            levelTimer = FindObjectOfType<LevelTimer>();
            if (levelTimer == null)
                Debug.LogWarning("[PowerUpManager] LevelTimer not found.");
        }

        if (magnetStick != null)
        {
            origRadius = magnetStick.MagnetRadius;
            origSpeed = magnetStick.MagnetSpeed;
            origMaxBalls = magnetStick.MaxBalls;
        }

        SetFill(timeFreezeCD, 0f);
        SetFill(colorBombCD, 0f);
        SetFill(overloadCD, 0f);
        SetFill(rainbowCD, 0f);

        // Initialise stock
        rainbowStock = rainbowStartStock;
        timerStock = timerStartStock;
        bombStock = bombStartStock;
        overloadStock = overloadStartStock;

        // Apply lock states first, then refresh all stock labels + buy buttons
        ApplyUnlockStates();
        RefreshAllStockUI();

        // ── Hook into the persistent CoinManager (single source of truth) ──
        // If present, subscribe so Buy buttons re-evaluate whenever coins change
        // (level rewards, purchases elsewhere, etc.). If absent, we silently
        // fall back to the local playerCoins field so the game still runs.
        if (CoinManager.Instance != null)
        {
            usingCoinManager = true;
            CoinManager.Instance.OnCoinsChanged += OnCoinsChanged;
            RefreshAllStockUI(); // reflect the real stored balance immediately
        }
        else
        {
            Debug.LogWarning("[PowerUpManager] No CoinManager found — using local playerCoins fallback.");
        }
    }

    private void OnDestroy()
    {
        if (usingCoinManager && CoinManager.Instance != null)
            CoinManager.Instance.OnCoinsChanged -= OnCoinsChanged;
    }

    // Called by CoinManager whenever the balance changes.
    private void OnCoinsChanged(int newTotal)
    {
        RefreshAllStockUI();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UNLOCK SYSTEM
    // ═════════════════════════════════════════════════════════════════════════

    private void ApplyUnlockStates()
    {
        int scene = CurrentLevelIndex();

        ApplySingleUnlock(scene, rainbowUnlockLevel, rainbowButton, rainbowBuyButton, rainbowLockOverlay, rainbowLockLabel, rainbowOrbArt, rainbowStockLabel, "Rainbow Orb");
        ApplySingleUnlock(scene, timerUnlockLevel, timeFreezeButton, timerBuyButton, timerLockOverlay, timerLockLabel, timerOrbArt, timerStockLabel, "Timer Orb");
        ApplySingleUnlock(scene, bombUnlockLevel, colorBombButton, bombBuyButton, bombLockOverlay, bombLockLabel, bombOrbArt, bombStockLabel, "Color Bomb");
        ApplySingleUnlock(scene, overloadUnlockLevel, overloadButton, overloadBuyButton, overloadLockOverlay, overloadLockLabel, overloadOrbArt, overloadStockLabel, "Overload");
    }

    private void ApplySingleUnlock(int currentScene, int unlockAt,
        Button mainBtn, Button buyBtn, GameObject overlay, RawImage lockIcon,
        RawImage orbArt, TMP_Text stockLabel, string powerName)
    {
        bool locked = currentScene < unlockAt;

        if (mainBtn != null) mainBtn.interactable = !locked;

        // ── Buy button: deactivate the whole object when locked ──
        if (buyBtn != null) buyBtn.gameObject.SetActive(!locked);

        // ── No.ofPowerups: if the stock TMP_Text sits inside a RawImage badge,
        // deactivate that parent RawImage so the badge AND its text hide together.
        // Only the IMMEDIATE parent is checked, so a RawImage higher up the
        // hierarchy (e.g. the orb art) is never accidentally turned off.
        // If the immediate parent has no RawImage, the label's own object is used. ──
        if (stockLabel != null)
        {
            GameObject toToggle = stockLabel.gameObject;

            Transform parent = stockLabel.transform.parent;
            if (parent != null && parent.GetComponent<RawImage>() != null)
                toToggle = parent.gameObject;

            toToggle.SetActive(!locked);
        }

        // ── Orb art: fade down when locked, full opacity when unlocked ──
        if (orbArt != null)
        {
            Color c = orbArt.color;
            c.a = locked ? lockedFadeAlpha : 1f;
            orbArt.color = c;
        }

        // ── Lock visuals ──
        if (locked)
        {
            // Show the lock overlay + icon while locked.
            if (overlay != null) overlay.SetActive(true);
            if (lockIcon != null) lockIcon.enabled = true;
        }
        else
        {
            // Unlocked → remove the lock icon entirely so it never lingers.
            if (lockIcon != null) Destroy(lockIcon.gameObject);
            if (overlay != null) overlay.SetActive(false);
        }

        Debug.Log($"[PowerUpManager] {powerName} — {(locked ? $"LOCKED (unlocks at level index {unlockAt})" : "UNLOCKED")}");
    }

    private bool IsUnlocked(Button btn)
    {
        int level = CurrentLevelIndex();
        int unlockAt = GetUnlockLevel(btn);
        return level >= unlockAt;
    }

    /// <summary>
    /// Returns the current level's array index (0-based) from LevelManager.
    /// This replaces the old scene build-index check, since the game now swaps
    /// level PREFABS within a single scene instead of loading new scenes.
    /// Falls back to 0 if LevelManager isn't ready, so nothing unlocks early.
    /// </summary>
    private int CurrentLevelIndex()
    {
        return LevelManager.Instance != null
            ? LevelManager.Instance.GetCurrentLevelIndex()
            : 0;
    }

    /// <summary>
    /// Re-applies all unlock states and refreshes stock UI. Call this whenever
    /// the current level changes (LevelManager invokes it on each level load),
    /// so orbs unlock the moment the player reaches their unlock index — no
    /// scene reload required.
    /// </summary>
    public void RefreshUnlocks()
    {
        ApplyUnlockStates();
        RefreshAllStockUI();
    }

    private int GetUnlockLevel(Button btn)
    {
        if (btn == rainbowButton) return rainbowUnlockLevel;
        if (btn == timeFreezeButton) return timerUnlockLevel;
        if (btn == colorBombButton) return bombUnlockLevel;
        if (btn == overloadButton) return overloadUnlockLevel;
        return 0;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STOCK UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Refreshes a single power-up's stock label and the interactability of
    /// its main button and Buy button based on current stock + unlock state.
    /// </summary>
    private void RefreshStockUI(int stock, TMP_Text label,
        Button mainBtn, Button buyBtn, int unlockLevel)
    {
        int scene = CurrentLevelIndex();
        bool unlocked = scene >= unlockLevel;

        // Stock label — show "x0", "x1", "x2" …
        if (label != null)
            label.text = $"x{stock}";

        // Main button: enabled only if unlocked AND stock > 0 AND not on cooldown
        if (mainBtn != null)
        {
            bool onCD = IsOnCooldown(mainBtn);
            mainBtn.interactable = unlocked && stock > 0 && !onCD;
        }

        // Buy button: always visible when unlocked, but interactable only when
        // player can afford it.  Disable when locked.
        if (buyBtn != null)
            buyBtn.interactable = unlocked && CurrentCoins() >= coinCostPerCharge;
    }

    /// <summary>
    /// Returns the authoritative coin balance: the persistent CoinManager if one
    /// exists, otherwise the local playerCoins fallback.
    /// </summary>
    private int CurrentCoins()
    {
        return CoinManager.Instance != null ? CoinManager.Instance.Coins : playerCoins;
    }

    private void RefreshAllStockUI()
    {
        RefreshStockUI(rainbowStock, rainbowStockLabel, rainbowButton, rainbowBuyButton, rainbowUnlockLevel);
        RefreshStockUI(timerStock, timerStockLabel, timeFreezeButton, timerBuyButton, timerUnlockLevel);
        RefreshStockUI(bombStock, bombStockLabel, colorBombButton, bombBuyButton, bombUnlockLevel);
        RefreshStockUI(overloadStock, overloadStockLabel, overloadButton, overloadBuyButton, overloadUnlockLevel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  COIN HELPER (wire to your CoinManager later)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the internal coin count.  Call this from your CoinManager
    /// whenever coins change so buy buttons stay in sync.
    /// </summary>
    /// <summary>
    /// Sets the coin count. Routes to the persistent CoinManager when present
    /// (so the value is saved and other listeners update), otherwise updates the
    /// local fallback. Kept public for backward compatibility / manual testing.
    /// </summary>
    public void SetCoins(int coins)
    {
        if (CoinManager.Instance != null)
            CoinManager.Instance.SetCoins(coins); // triggers OnCoinsChanged → RefreshAllStockUI
        else
        {
            playerCoins = coins;
            RefreshAllStockUI();
        }
    }

    public int GetCoins() => CurrentCoins();

    // ═════════════════════════════════════════════════════════════════════════
    //  BUY-BACK — one public method per power-up, wired to each Buy button
    // ═════════════════════════════════════════════════════════════════════════

    public void BuyRainbow() => TryBuy(ref rainbowStock, rainbowStockLabel, rainbowButton, rainbowBuyButton, rainbowUnlockLevel, "Rainbow");
    public void BuyTimer() => TryBuy(ref timerStock, timerStockLabel, timeFreezeButton, timerBuyButton, timerUnlockLevel, "Timer Orb");
    public void BuyBomb() => TryBuy(ref bombStock, bombStockLabel, colorBombButton, bombBuyButton, bombUnlockLevel, "Color Bomb");
    public void BuyOverload() => TryBuy(ref overloadStock, overloadStockLabel, overloadButton, overloadBuyButton, overloadUnlockLevel, "Overload");

    private void TryBuy(ref int stock, TMP_Text label,
        Button mainBtn, Button buyBtn, int unlockLevel, string powerName)
    {
        if (!IsUnlocked(mainBtn))
        {
            Debug.Log($"[PowerUpManager] {powerName} is still locked — cannot buy.");
            return;
        }

        // Spend through the persistent CoinManager when present so the purchase
        // is saved and every coin display updates. Fall back to the local field
        // only if no CoinManager exists in the scene.
        bool paid;
        if (CoinManager.Instance != null)
        {
            paid = CoinManager.Instance.TrySpend(coinCostPerCharge);
        }
        else
        {
            paid = playerCoins >= coinCostPerCharge;
            if (paid) playerCoins -= coinCostPerCharge;
        }

        if (!paid)
        {
            Debug.Log($"[PowerUpManager] Not enough coins to buy {powerName} " +
                      $"(have {CurrentCoins()}, need {coinCostPerCharge}).");
            return;
        }

        stock++;

        Debug.Log($"[PowerUpManager] Bought 1 {powerName} charge. " +
                  $"Stock: {stock}  Coins left: {CurrentCoins()}");

        // If a CoinManager handled payment it already fired OnCoinsChanged →
        // RefreshAllStockUI. We still refresh this power-up explicitly so the
        // new stock count shows immediately even in the local-fallback path.
        RefreshStockUI(stock, label, mainBtn, buyBtn, unlockLevel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  1. RAINBOW BALL
    // ═════════════════════════════════════════════════════════════════════════

    public void UseRainbowBall()
    {
        if (rainbowOnCD) return;
        if (!IsUnlocked(rainbowButton)) { Debug.Log("[PowerUpManager] Rainbow Orb is locked."); return; }
        if (rainbowStock <= 0) { Debug.Log("[PowerUpManager] Rainbow Orb out of stock."); return; }
        if (rainbowBallSpawner == null) { Debug.LogWarning("[PowerUpManager] RainbowBallSpawner not assigned."); return; }

        // Only spend a charge + start cooldown if a ball was ACTUALLY spawned.
        // If the arena cap is hit (a rainbow ball is still live), the spawn is
        // skipped and the player keeps the charge — button stays usable.
        bool spawned = rainbowBallSpawner.SpawnRainbowBall();
        if (!spawned)
        {
            Debug.Log("[PowerUpManager] Rainbow spawn skipped (arena cap) — charge not used.");
            return;
        }

        rainbowStock--;
        Debug.Log($"[PowerUp] Rainbow Ball spawned! Stock left: {rainbowStock}");

        RefreshStockUI(rainbowStock, rainbowStockLabel, rainbowButton, rainbowBuyButton, rainbowUnlockLevel);
        StartCoroutine(CooldownRoutine(rainbowBallCooldown, rainbowCD, rainbowButton,
            v => rainbowOnCD = v));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  2. TIMER ORB
    // ═════════════════════════════════════════════════════════════════════════

    public void UseTimeFreeze()
    {
        if (freezeOnCD || freezeActive) return;
        if (!IsUnlocked(timeFreezeButton)) { Debug.Log("[PowerUpManager] Timer Orb is locked."); return; }
        if (timerStock <= 0) { Debug.Log("[PowerUpManager] Timer Orb out of stock."); return; }
        if (levelTimer == null) { Debug.LogWarning("[PowerUpManager] LevelTimer not assigned."); return; }

        timerStock--;
        Debug.Log($"[PowerUp] Timer Orb used! +{timeBonusSeconds}s. Stock left: {timerStock}");

        RefreshStockUI(timerStock, timerStockLabel, timeFreezeButton, timerBuyButton, timerUnlockLevel);
        StartCoroutine(TimerOrbRoutine());
        StartCoroutine(CooldownRoutine(timeFreezeCooldown, timeFreezeCD, timeFreezeButton,
            v => freezeOnCD = v));
    }

    private IEnumerator TimerOrbRoutine()
    {
        freezeActive = true;
        levelTimer.AddTime(timeBonusSeconds);

        bool dustWasRunning = UIManager.Instance != null && UIManager.Instance.IsBoxTimerRunning;
        if (dustWasRunning) UIManager.Instance.PauseBoxTimer();

        if (freezeOverlayImage != null)
        {
            freezeOverlayImage.gameObject.SetActive(true);
            SetAlpha(freezeOverlayImage, 0.35f);
        }

        yield return new WaitForSeconds(freezeOverlayDuration);

        if (freezeOverlayImage != null)
            freezeOverlayImage.gameObject.SetActive(false);

        if (dustWasRunning && UIManager.Instance != null)
            UIManager.Instance.ResumeBoxTimer();

        freezeActive = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  3. COLOR BOMB
    // ═════════════════════════════════════════════════════════════════════════

    public void UseColorBomb()
    {
        if (bombOnCD || bombArmed) return;
        if (!IsUnlocked(colorBombButton)) { Debug.Log("[PowerUpManager] Color Bomb is locked."); return; }
        if (bombStock <= 0) { Debug.Log("[PowerUpManager] Color Bomb out of stock."); return; }
        if (colorBombPrefab == null) { Debug.LogWarning("[PowerUpManager] Color Bomb prefab not assigned."); return; }

        bombArmed = true;
        Debug.Log("[PowerUp] Color Bomb armed — tap the arena to throw!");
    }

    private void Update()
    {
        if (!bombArmed) return;

        bool tapped = false;
        Vector3 screenPos = Vector3.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) { tapped = true; screenPos = Input.mousePosition; }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            { tapped = true; screenPos = Input.GetTouch(0).position; }
#endif
        if (tapped) TryPlaceBomb(screenPos);
    }

    private void TryPlaceBomb(Vector3 screenPos)
    {
        Camera cam = gameCamera != null ? gameCamera : Camera.main;
        if (cam == null) { bombArmed = false; return; }

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, arenaTapLayer))
        {
            Debug.Log("[PowerUp] Bomb tap missed arena — tap again.");
            return;
        }

        bombArmed = false;
        bombStock--;
        Debug.Log($"[PowerUp] Color Bomb launched. Stock left: {bombStock}");

        RefreshStockUI(bombStock, bombStockLabel, colorBombButton, bombBuyButton, bombUnlockLevel);

        Vector3 targetPos = hit.point;
        Vector3 spawnPos = targetPos + Vector3.up * bombSpawnHeight;

        GameObject bombObj = Instantiate(colorBombPrefab, spawnPos, Quaternion.identity);
        ColorBomb bomb = bombObj.GetComponent<ColorBomb>();

        if (bomb != null)
            bomb.Launch(targetPos);
        else
        {
            Debug.LogWarning("[PowerUpManager] colorBombPrefab missing ColorBomb component.");
            Destroy(bombObj);
        }

        StartCoroutine(CooldownRoutine(colorBombCooldown, colorBombCD, colorBombButton,
            v => bombOnCD = v));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  4. OVERLOAD MODE
    // ═════════════════════════════════════════════════════════════════════════

    public void UseOverloadMode()
    {
        if (overloadOnCD || overloadActive) return;
        if (!IsUnlocked(overloadButton)) { Debug.Log("[PowerUpManager] Overload is locked."); return; }
        if (overloadStock <= 0) { Debug.Log("[PowerUpManager] Overload out of stock."); return; }
        if (magnetStick == null) { Debug.LogWarning("[PowerUpManager] MagnetStick not assigned."); return; }

        overloadStock--;
        Debug.Log($"[PowerUp] Overload activated! Stock left: {overloadStock}");

        RefreshStockUI(overloadStock, overloadStockLabel, overloadButton, overloadBuyButton, overloadUnlockLevel);
        StartCoroutine(OverloadRoutine());
        StartCoroutine(CooldownRoutine(overloadCooldown, overloadCD, overloadButton,
            v => overloadOnCD = v));
    }

    private IEnumerator OverloadRoutine()
    {
        overloadActive = true;
        magnetStick.MagnetRadius = origRadius * overloadRadiusMultiplier;
        magnetStick.MagnetSpeed = origSpeed * overloadSpeedMultiplier;
        magnetStick.MaxBalls = overloadMaxBalls;

        yield return new WaitForSeconds(overloadDuration);

        magnetStick.MagnetRadius = origRadius;
        magnetStick.MagnetSpeed = origSpeed;
        magnetStick.MaxBalls = origMaxBalls;
        overloadActive = false;
        Debug.Log("[PowerUp] Overload ended — magnet restored.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  COOLDOWN HELPER
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs the radial fill countdown.
    /// Re-enables the button only if still unlocked AND stock > 0 after cooldown.
    /// </summary>
    private IEnumerator CooldownRoutine(float duration, Image fillImage, Button btn,
        System.Action<bool> setOnCD)
    {
        setOnCD(true);
        if (btn != null) btn.interactable = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFill(fillImage, 1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetFill(fillImage, 0f);

        // Clear the cooldown flag FIRST so RefreshStockUI sees onCD = false,
        // then refresh through the single source of truth. This guarantees the
        // button re-enables whenever it is unlocked AND still has stock.
        setOnCD(false);
        RefreshButtonFor(btn);
    }

    /// <summary>
    /// Re-evaluates and applies the interactable state for a single power-up's
    /// main button + Buy button + stock label. One consistent path used by
    /// both usage and cooldown-end so button states never get stuck.
    /// </summary>
    private void RefreshButtonFor(Button btn)
    {
        if (btn == rainbowButton)
            RefreshStockUI(rainbowStock, rainbowStockLabel, rainbowButton, rainbowBuyButton, rainbowUnlockLevel);
        else if (btn == timeFreezeButton)
            RefreshStockUI(timerStock, timerStockLabel, timeFreezeButton, timerBuyButton, timerUnlockLevel);
        else if (btn == colorBombButton)
            RefreshStockUI(bombStock, bombStockLabel, colorBombButton, bombBuyButton, bombUnlockLevel);
        else if (btn == overloadButton)
            RefreshStockUI(overloadStock, overloadStockLabel, overloadButton, overloadBuyButton, overloadUnlockLevel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UTILITY
    // ═════════════════════════════════════════════════════════════════════════

    private bool IsOnCooldown(Button btn)
    {
        if (btn == rainbowButton) return rainbowOnCD;
        if (btn == timeFreezeButton) return freezeOnCD;
        if (btn == colorBombButton) return bombOnCD;
        if (btn == overloadButton) return overloadOnCD;
        return false;
    }

    private void SetFill(Image img, float amount)
    {
        if (img != null) img.fillAmount = amount;
    }

    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}