using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  BallSpawner — Continuous Wave-Based Ball Spawner
//  Attach to an empty GameObject above your play area.
//  Set spawnPoints in Inspector (or it falls back to transform.position).
// ============================================================

[System.Serializable]
public class LevelConfig
{
    [Header("Level Info")]
    public string levelName = "Level 1";

    [Header("Targets to Fill")]
    public int redTarget = 20;
    public int blueTarget = 15;
    public int greenTarget = 10;

    [Header("Spawn Settings")]
    [Tooltip("Total balls spawned per wave")]
    public int ballsPerWave = 15;

    [Tooltip("Seconds between each ball spawn inside a wave")]
    public float spawnInterval = 0.4f;

    [Tooltip("Seconds to wait before the next wave auto-starts")]
    public float waveCooldown = 3f;

    [Tooltip("How many waves before level is considered endless")]
    public int maxWaves = 5;

    [Header("Color Weights (higher = more frequent)")]
    [Range(1, 10)] public int redWeight = 3;
    [Range(1, 10)] public int blueWeight = 3;
    [Range(1, 10)] public int greenWeight = 3;

    [Header("Physics")]
    public float spawnForceMin = 2f;
    public float spawnForceMax = 5f;
    public Vector3 forceDirection = new Vector3(0, -1f, 0.5f); // down + slight forward
}

public class BallSpawner : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn Points (leave empty → use this transform)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Level Configs")]
    [SerializeField] private LevelConfig[] levels;

    [Header("Spawn Area Scatter")]
    [SerializeField] private float scatterRadius = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool showLogs = true;

    // ─── State ────────────────────────────────────────────────
    private int currentLevelIndex = 0;
    private int currentWave = 0;
    private int ballsSpawnedInWave = 0;
    private bool isSpawning = false;
    private bool levelComplete = false;

    // Running totals used to check win condition
    private int redCollected = 0;
    private int blueCollected = 0;
    private int greenCollected = 0;

    // Pool of all live balls (optional — for reference)
    private readonly List<GameObject> aliveBalls = new List<GameObject>();

    // ─── Properties ───────────────────────────────────────────
    public LevelConfig CurrentLevel =>
        (levels != null && levels.Length > 0)
        ? levels[Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1)]
        : null;

    public int CurrentWaveNumber => currentWave + 1;
    public bool IsSpawning => isSpawning;

    // ─── Unity ────────────────────────────────────────────────
    private void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("BallSpawner: No levels configured!");
            return;
        }

        // Sync UIManager targets with first level
        SyncUITargets();

        // Auto-start first wave
        StartNextWave();
    }

    // ─── Public API ───────────────────────────────────────────

    /// Called by MagnetStick.ReleaseAllBalls() after balls are dropped
    public void OnWaveCleared()
    {
        if (showLogs) Debug.Log($"Wave cleared. Checking if next wave is needed…");
        // Optionally wait a beat, then start next wave
        if (!levelComplete)
            Invoke(nameof(StartNextWave), CurrentLevel.waveCooldown);
    }

    /// Called by BallBox / UIManager when a ball is successfully collected
    public void RegisterBallCollected(BallColor color)
    {
        switch (color)
        {
            case BallColor.Red: redCollected++; break;
            case BallColor.Blue: blueCollected++; break;
            case BallColor.Green: greenCollected++; break;
        }

        CheckLevelComplete();
    }

    /// Jump directly to a specific level (e.g. from menu)
    public void LoadLevel(int index)
    {
        if (index < 0 || index >= levels.Length) return;

        currentLevelIndex = index;
        currentWave = 0;
        levelComplete = false;
        redCollected = blueCollected = greenCollected = 0;

        SyncUITargets();
        StopAllCoroutines();
        StartNextWave();
    }

    // ─── Wave Logic ───────────────────────────────────────────

    private void StartNextWave()
    {
        if (levelComplete) return;

        LevelConfig cfg = CurrentLevel;
        if (cfg == null) return;

        if (currentWave >= cfg.maxWaves)
        {
            if (showLogs) Debug.Log("All waves done for this level. Waiting for win condition.");
            return;
        }

        if (showLogs) Debug.Log($"▶ Starting Wave {currentWave + 1} of {cfg.maxWaves}  [{cfg.levelName}]");
        StartCoroutine(SpawnWave(cfg));
    }

    private IEnumerator SpawnWave(LevelConfig cfg)
    {
        isSpawning = true;
        ballsSpawnedInWave = 0;

        // Build weighted color pool
        List<BallColor> pool = BuildColorPool(cfg);

        for (int i = 0; i < cfg.ballsPerWave; i++)
        {
            SpawnOneBall(pool, cfg);
            ballsSpawnedInWave++;
            yield return new WaitForSeconds(cfg.spawnInterval);
        }

        currentWave++;
        isSpawning = false;

        if (showLogs) Debug.Log($"Wave {currentWave} spawning complete.");
    }

    // ─── Ball Spawning ────────────────────────────────────────

    private void SpawnOneBall(List<BallColor> pool, LevelConfig cfg)
    {
        if (ballPrefab == null) return;

        // Pick random spawn point
        Vector3 spawnPos = GetSpawnPosition();

        // Add scatter
        spawnPos += new Vector3(
            Random.Range(-scatterRadius, scatterRadius),
            0f,
            Random.Range(-scatterRadius, scatterRadius)
        );

        // Instantiate
        GameObject ballObj = Instantiate(ballPrefab, spawnPos, Quaternion.identity);

        // Pick and apply color
        BallColor color = pool[Random.Range(0, pool.Count)];
        Ball ball = ballObj.GetComponent<Ball>();
        if (ball != null) ball.SetColor(color);

        // Apply launch force
        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float force = Random.Range(cfg.spawnForceMin, cfg.spawnForceMax);
            rb.AddForce(cfg.forceDirection.normalized * force, ForceMode.Impulse);
        }

        aliveBalls.Add(ballObj);
    }

    // ─── Helpers ──────────────────────────────────────────────

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            return pt != null ? pt.position : transform.position;
        }
        return transform.position;
    }

    private List<BallColor> BuildColorPool(LevelConfig cfg)
    {
        var pool = new List<BallColor>();
        for (int i = 0; i < cfg.redWeight; i++) pool.Add(BallColor.Red);
        for (int i = 0; i < cfg.blueWeight; i++) pool.Add(BallColor.Blue);
        for (int i = 0; i < cfg.greenWeight; i++) pool.Add(BallColor.Green);
        return pool;
    }

    private void SyncUITargets()
    {
        LevelConfig cfg = CurrentLevel;
        if (cfg == null || UIManager.Instance == null) return;

        // Directly set targets on UIManager via reflection-free public method
        // Requires you to add SetTargets(int r, int b, int g) to UIManager — see note below
        if (UIManager.Instance != null)
            UIManager.Instance.SetTargets(cfg.redTarget, cfg.blueTarget, cfg.greenTarget);
    }

    private void CheckLevelComplete()
    {
        LevelConfig cfg = CurrentLevel;
        if (cfg == null) return;

        if (redCollected >= cfg.redTarget &&
            blueCollected >= cfg.blueTarget &&
            greenCollected >= cfg.greenTarget)
        {
            levelComplete = true;

            if (showLogs) Debug.Log($"🎉 Level {cfg.levelName} Complete!");

            // Advance to next level after short delay
            int next = currentLevelIndex + 1;
            if (next < levels.Length)
                Invoke(nameof(AdvanceToNextLevel), 2f);
            else
                Debug.Log("🏆 All levels complete!");
        }
    }

    private void AdvanceToNextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }
}

