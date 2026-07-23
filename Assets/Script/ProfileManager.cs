using UnityEngine;
using System;

/// <summary>
/// Persistent player profile (level + XP), saved across sessions via PlayerPrefs.
/// Mirrors CoinManager's pattern: singleton, DontDestroyOnLoad, change event.
///
/// XP CURVE
/// ────────
/// The XP required to reach the next level DOUBLES each level:
///   level 1 → 2 : baseXP
///   level 2 → 3 : baseXP * 2
///   level 3 → 4 : baseXP * 4   …and so on.
///
/// Adding XP rolls over: excess past a threshold carries into the next level,
/// and a single large XP grant can cross multiple levels at once.
///
/// SETUP
/// ─────
/// 1. Create an empty GameObject named "ProfileManager".
/// 2. Add this component. Persists via DontDestroyOnLoad — put it only in your
///    first-loaded scene, or in every scene (duplicates self-destroy).
/// 3. Tune Base XP in the Inspector.
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    const string LEVEL_KEY = "ProfileLevel";
    const string XP_KEY = "ProfileXP";

    [Header("XP Curve")]
    [Tooltip("Base XP required to go from level 1 → 2. Each subsequent level doubles this.")]
    [SerializeField] private int baseXP = 100;

    [Header("XP Reward Scaling")]
    [Tooltip("The XP reward earned per win grows by this fraction for each level. " +
             "0.15 = +15% per level (level 1 = base, level 2 = base*1.15, level 3 = base*1.15^2 …).")]
    [SerializeField] private float rewardGrowthPerLevel = 0.15f;

    public int Level { get; private set; }
    public int CurrentXP { get; private set; }   // XP within the current level

    // Fired on any change. (level, currentXP, xpForNextLevel)
    public event Action<int, int, int> OnProfileChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Level = Mathf.Max(1, PlayerPrefs.GetInt(LEVEL_KEY, 1)); // start at level 1
        CurrentXP = PlayerPrefs.GetInt(XP_KEY, 0);
    }

    void Start()
    {
        // Push initial values to listeners that subscribed after Awake
        OnProfileChanged?.Invoke(Level, CurrentXP, XPForLevel(Level));
    }

    /// <summary>
    /// XP needed to advance FROM the given level to the next.
    /// Doubles each level: level 1 → baseXP, level 2 → baseXP*2, level 3 → baseXP*4 …
    /// </summary>
    public int XPForLevel(int level)
    {
        int steps = Mathf.Max(1, level) - 1;     // level 1 = 0 doublings
        return baseXP * (int)Mathf.Pow(2, steps);
    }

    public int XPForNextLevel => XPForLevel(Level);

    /// <summary>
    /// Scales a base XP reward by the player's current level:
    /// scaled = baseReward * (1 + rewardGrowthPerLevel)^(Level - 1).
    /// Level 1 returns baseReward unchanged; each higher level adds 15% (default).
    /// </summary>
    public int ScaledReward(int baseReward)
    {
        float multiplier = Mathf.Pow(1f + rewardGrowthPerLevel, Mathf.Max(1, Level) - 1);
        return Mathf.RoundToInt(baseReward * multiplier);
    }

    /// <summary>
    /// Grants a level-scaled XP reward in one call. Scales by the level at the
    /// moment of the call (before any level-ups this grant causes), then adds it.
    /// Returns the actual XP granted (useful for UI / logging).
    /// </summary>
    public int AddScaledXP(int baseReward)
    {
        int granted = ScaledReward(baseReward);
        AddXP(granted);
        return granted;
    }

    /// <summary>Adds XP, rolling over into level-ups (can cross multiple levels).</summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        CurrentXP += amount;

        int needed = XPForLevel(Level);
        while (CurrentXP >= needed)
        {
            CurrentXP -= needed;     // rollover excess
            Level++;
            needed = XPForLevel(Level);
            Debug.Log($"[ProfileManager] LEVEL UP! Now level {Level}");
        }

        Save();
    }

    // ── Testing / reset helpers ──
    public void SetProfile(int level, int xp)
    {
        Level = Mathf.Max(1, level);
        CurrentXP = Mathf.Max(0, xp);
        Save();
    }

    public void ResetProfile() => SetProfile(1, 0);

    void Save()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, Level);
        PlayerPrefs.SetInt(XP_KEY, CurrentXP);
        PlayerPrefs.Save();
        OnProfileChanged?.Invoke(Level, CurrentXP, XPForLevel(Level));
    }
}