using UnityEngine;
using System.Collections;

public class BoxSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] boxPrefabs;

    // ── Dust-box prefab ────────────────────────────────────────────────────────
    // Assign the prefab whose BallBox has collectAllColors = true.
    // BoxSpawner uses this reference to:
    //   • detect when a dust box (not a color box) is destroyed
    //   • read its base capacity so it can apply the +5 per-wave bonus
    [SerializeField] private GameObject dustBoxPrefab;

    [Header("Grid Settings")]
    [SerializeField] private int rows = 10;
    [SerializeField] private int columns = 3;

    [SerializeField] private float rowSpacing = 2f;
    [SerializeField] private float columnSpacing = 2f;

    [Header("Directions")]
    [SerializeField] private Vector3 rowDirection = Vector3.down;
    [SerializeField] private Vector3 columnDirection = Vector3.right;

    [Header("Offsets")]
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 perRowOffset;
    [SerializeField] private Vector3 perColumnOffset;

    [Header("Start Point")]
    [SerializeField] private Transform startPoint;

    [Header("Settings")]
    [SerializeField] private bool parentToSpawner = true;
    [SerializeField] private bool avoidImmediateRepeat = true;

    // ── Dust-box timer scaling ─────────────────────────────────────────────────
    [Header("Dust-Box Timer Settings")]
    [Tooltip("Duration (seconds) for the countdown after the 1st dust box is destroyed.")]
    [SerializeField] private float firstDustBoxTimerDuration = 15f;

    [Tooltip("Extra seconds added to the timer for each subsequent dust box destroyed.")]
    [SerializeField] private float timerIncreasePerBox = 15f;

    [Tooltip("Extra capacity added to each spawned dust box per wave (e.g. 5 means wave 2 = base+5, wave 3 = base+10).")]
    [SerializeField] private int capacityIncreasePerWave = 5;

    // ── Private state ──────────────────────────────────────────────────────────
    private Transform[,] grid;
    private int lastIndex = -1;

    // How many dust boxes have been completely destroyed so far this level.
    private int dustBoxesDestroyed = 0;

    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        SpawnGrid();
    }

    // ─── SPAWN GRID ────────────────────────────────────────────────────────────
    public void SpawnGrid()
    {
        if (boxPrefabs == null || boxPrefabs.Length == 0 || startPoint == null)
        {
            Debug.LogWarning("BoxSpawner: Missing setup — assign Box Prefabs and Start Point.");
            return;
        }

        grid = new Transform[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = GetGridPosition(r, c);

                GameObject prefab = GetRandomPrefab();

                // Parent at instantiate time (not after) — SetParent on an
                // already-placed object re-interprets the position against the
                // parent's transform and shifts the box.
                GameObject box = parentToSpawner
                    ? Instantiate(prefab, pos, Quaternion.identity, transform)
                    : Instantiate(prefab, pos, Quaternion.identity);

                BallBox boxScript = box.GetComponent<BallBox>();
                if (boxScript != null)
                    boxScript.SetSpawner(this);

                grid[r, c] = box.transform;
            }
        }

        // Set initial lid states — row 0 of each column is open, rest are closed
        for (int c = 0; c < columns; c++)
            RefreshAllLidsInColumn(c);
    }

    // ─── POSITION CALCULATION ──────────────────────────────────────────────────
    private Vector3 GetGridPosition(int r, int c)
    {
        return startPoint.position
            + (rowDirection.normalized * rowSpacing * r)
            + (columnDirection.normalized * columnSpacing * c)
            + positionOffset
            + (perRowOffset * r)
            + (perColumnOffset * c);
    }

    // ─── RANDOM PREFAB ─────────────────────────────────────────────────────────
    private GameObject GetRandomPrefab()
    {
        if (!avoidImmediateRepeat)
            return boxPrefabs[Random.Range(0, boxPrefabs.Length)];

        int newIndex;
        do
        {
            newIndex = Random.Range(0, boxPrefabs.Length);
        }
        while (newIndex == lastIndex && boxPrefabs.Length > 1);

        lastIndex = newIndex;
        return boxPrefabs[newIndex];
    }

    // ─── CALLED BY BALLBOX ─────────────────────────────────────────────────────
    public void OnBoxDestroyed(Transform destroyedBox)
    {
        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (grid[r, c] != destroyedBox) continue;

                // Only act on top-slot boxes
                if (r == 0)
                {
                    BallBox box = destroyedBox.GetComponent<BallBox>();

                    // ── Is this a dust box? ──────────────────────────────────
                    // We identify a dust box by its collectAllColors flag.
                    // This way it works even if the prefab reference is not set.
                    bool isDustBox = box != null && box.IsDustBox;

                    if (isDustBox)
                        HandleDustBoxDestroyed();
                    // Color boxes do NOT start the dust-box timer.
                }

                grid[r, c] = null;
                StartCoroutine(ShiftColumnUp(r, c));
                return;
            }
        }
    }

    // ─── DUST-BOX DESTROYED LOGIC ─────────────────────────────────────────────
    private void HandleDustBoxDestroyed()
    {
        dustBoxesDestroyed++;

        // Duration scales: 1st box = firstDustBoxTimerDuration,
        //                  2nd     = firstDustBoxTimerDuration + timerIncreasePerBox,
        //                  3rd     = firstDustBoxTimerDuration + 2 * timerIncreasePerBox, …
        float duration = firstDustBoxTimerDuration
                         + (dustBoxesDestroyed - 1) * timerIncreasePerBox;

        if (UIManager.Instance != null)
            UIManager.Instance.StartBoxTimer(duration);
        else
            Debug.LogWarning("[BoxSpawner] UIManager.Instance is null — box timer not started.");

        Debug.Log($"⏱ Dust-box #{dustBoxesDestroyed} destroyed — timer = {duration}s");
    }

    /// <summary>
    /// Returns the extra capacity that should be applied to a newly spawned
    /// dust box based on how many waves have already been completed.
    /// BallBox calls this via SetSpawner() so each new front-of-queue dust box
    /// is born with the correct wave-scaled capacity.
    /// </summary>
    public int GetDustBoxCapacityBonus() => dustBoxesDestroyed * capacityIncreasePerWave;

    // ─── IS TOP BOX (called by BallBox.CanAcceptBall) ─────────────────────────
    public bool IsTopBox(BallBox box)
    {
        if (grid == null) return false;

        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (grid[r, c] == null) continue;

                BallBox topBox = grid[r, c].GetComponent<BallBox>();
                if (topBox == box) return true;

                break;
            }
        }

        return false;
    }

    // ─── GET TOP ACTIVE DUSTBOX (called by MagnetStick) ───────────────────────
    public BallBox GetTopBox()
    {
        if (grid == null) return null;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (grid[r, c] != null)
                {
                    BallBox box = grid[r, c].GetComponent<BallBox>();
                    if (box != null && !box.IsFull && box.gameObject.activeInHierarchy)
                        return box;
                }
            }
        }

        return null;
    }

    // ─── SHIFT LOGIC ───────────────────────────────────────────────────────────
    private IEnumerator ShiftColumnUp(int startRow, int column)
    {
        for (int r = startRow; r < rows - 1; r++)
        {
            Transform nextBox = grid[r + 1, column];

            if (nextBox == null) continue;

            Vector3 targetPos = GetGridPosition(r, column);

            grid[r, column] = nextBox;
            grid[r + 1, column] = null;

            RefreshAllLidsInColumn(column);

            yield return StartCoroutine(MoveSmooth(nextBox, targetPos));
        }
    }

    private void RefreshAllLidsInColumn(int column)
    {
        for (int r = 0; r < rows; r++)
        {
            if (grid[r, column] == null) continue;
            BallBox box = grid[r, column].GetComponent<BallBox>();
            if (box != null) box.RefreshLid();
        }
    }

    // ─── SMOOTH MOVEMENT ───────────────────────────────────────────────────────
    private IEnumerator MoveSmooth(Transform obj, Vector3 target)
    {
        float duration = 0.3f;
        float time = 0f;
        Vector3 start = obj.position;

        while (time < duration)
        {
            if (obj == null) yield break;
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            obj.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        obj.position = target;
    }
}