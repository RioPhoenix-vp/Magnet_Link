using UnityEngine;

/// <summary>
/// Spawns rainbow wildcard balls ONLY when SpawnRainbowBall() is called
/// (i.e. when the player taps the Rainbow Orb button).
/// Auto-spawn timer removed — button-only trigger.
/// maxRainbowBallsAtOnce still limits how many can exist in the arena at once.
/// </summary>
public class RainbowBallSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Maximum number of rainbow balls allowed alive in the arena at once.")]
    [SerializeField] private int maxRainbowBallsAtOnce = 1;

    [Tooltip("How many rainbow balls a single Rainbow Orb tap spawns. " +
             "Will not exceed Max Rainbow Balls At Once — e.g. if max is 2 and " +
             "one ball is already live, only 1 more spawns.")]
    [SerializeField] private int ballsPerUse = 1;

    [Header("Spawn Area")]
    [Tooltip("Preferred: one or more BoxColliders defining the areas to spawn within. " +
             "For each ball, one box is chosen at random, then a random point inside " +
             "its world-space bounds. Make them triggers so they don't block anything.")]
    [SerializeField] private BoxCollider[] spawnAreas;

    [Tooltip("Fallback used only if no Spawn Area boxes are assigned: " +
             "spawns randomly around this point within Spawn Area Radius.")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnAreaRadius = 1f;

    private int currentRainbowCount = 0;

    /// <summary>
    /// Attempts to spawn up to ballsPerUse rainbow balls in one call, never
    /// exceeding maxRainbowBallsAtOnce live balls.
    /// Returns TRUE if at least one ball was spawned, FALSE if none were
    /// (missing setup, or the arena is already at the cap).
    /// PowerUpManager uses the return value to decide whether to consume a
    /// charge and start the cooldown — so a fully-skipped use costs nothing.
    /// </summary>
    public bool SpawnRainbowBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("RainbowBallSpawner: missing ballPrefab");
            return false;
        }

        if (!HasValidSpawnArea() && spawnPoint == null)
        {
            Debug.LogWarning("RainbowBallSpawner: assign at least one Spawn Area box or a Spawn Point");
            return false;
        }

        // How many we can actually spawn right now without passing the cap.
        int freeSlots = maxRainbowBallsAtOnce - currentRainbowCount;
        if (freeSlots <= 0)
        {
            Debug.Log($"[RainbowBallSpawner] Cap reached ({maxRainbowBallsAtOnce}) — spawn skipped.");
            return false;
        }

        int toSpawn = Mathf.Clamp(ballsPerUse, 1, freeSlots);

        for (int i = 0; i < toSpawn; i++)
            SpawnOne();

        Debug.Log($"⭐ Spawned {toSpawn} rainbow ball(s). Live: {currentRainbowCount}/{maxRainbowBallsAtOnce}");
        return true;
    }

    /// <summary>Returns true if at least one non-null spawn-area box is assigned.</summary>
    private bool HasValidSpawnArea()
    {
        if (spawnAreas == null) return false;
        for (int i = 0; i < spawnAreas.Length; i++)
            if (spawnAreas[i] != null) return true;
        return false;
    }

    /// <summary>Spawns exactly one rainbow ball and tracks it.</summary>
    private void SpawnOne()
    {
        Vector3 spawnPos = GetRandomSpawnPosition();

        GameObject ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);

        Ball ballScript = ball.GetComponent<Ball>();
        if (ballScript != null)
            ballScript.SetColor(BallColor.Rainbow);

        // Increment count; decrement automatically when the ball is destroyed
        currentRainbowCount++;
        var tracker = ball.AddComponent<RainbowBallTracker>();
        tracker.OnDestroyed += () =>
        {
            currentRainbowCount = Mathf.Max(0, currentRainbowCount - 1);
        };
    }

    /// <summary>
    /// Returns a random world-space point to spawn at.
    /// If spawn-area boxes are assigned, picks one at random and returns a random
    /// point inside its world-space bounds (respecting rotation/scale).
    /// Otherwise falls back to a random offset around spawnPoint.
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        BoxCollider area = GetRandomSpawnArea();

        if (area != null)
        {
            // Random point inside the box's LOCAL bounds, then transformed to
            // world space — this respects the collider's position, rotation,
            // and the GameObject's scale.
            Vector3 size = area.size;
            Vector3 center = area.center;

            Vector3 localPoint = center + new Vector3(
                Random.Range(-size.x * 0.5f, size.x * 0.5f),
                Random.Range(-size.y * 0.5f, size.y * 0.5f),
                Random.Range(-size.z * 0.5f, size.z * 0.5f)
            );

            return area.transform.TransformPoint(localPoint);
        }

        // Fallback: circular-ish area around the fixed spawn point
        Vector3 offset = new Vector3(
            Random.Range(-spawnAreaRadius, spawnAreaRadius),
            0f,
            Random.Range(-spawnAreaRadius, spawnAreaRadius)
        );
        return spawnPoint.position + offset;
    }

    /// <summary>
    /// Picks one assigned spawn-area box at random, skipping null slots.
    /// Returns null if none are assigned (caller falls back to spawnPoint).
    /// </summary>
    private BoxCollider GetRandomSpawnArea()
    {
        if (spawnAreas == null || spawnAreas.Length == 0) return null;

        // Count valid (non-null) boxes
        int validCount = 0;
        for (int i = 0; i < spawnAreas.Length; i++)
            if (spawnAreas[i] != null) validCount++;

        if (validCount == 0) return null;

        // Pick the Nth valid box at random
        int pick = Random.Range(0, validCount);
        int seen = 0;
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            if (spawnAreas[i] == null) continue;
            if (seen == pick) return spawnAreas[i];
            seen++;
        }

        return null;
    }

    // Draw all spawn boxes in the editor so you can see/position the areas
    private void OnDrawGizmosSelected()
    {
        if (spawnAreas == null) return;

        Matrix4x4 old = Gizmos.matrix;

        for (int i = 0; i < spawnAreas.Length; i++)
        {
            if (spawnAreas[i] == null) continue;

            Gizmos.matrix = spawnAreas[i].transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.25f);
            Gizmos.DrawCube(spawnAreas[i].center, spawnAreas[i].size);
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.9f);
            Gizmos.DrawWireCube(spawnAreas[i].center, spawnAreas[i].size);
        }

        Gizmos.matrix = old;
    }
}

/// <summary>
/// Lightweight tracker attached to each rainbow ball to notify the spawner when destroyed.
/// </summary>
public class RainbowBallTracker : MonoBehaviour
{
    public System.Action OnDestroyed;

    private void OnDestroy()
    {
        OnDestroyed?.Invoke();
    }
}