using UnityEngine;
using System;
using System.Collections;

public class BallBox : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int capacity = 10;
    [SerializeField] private bool collectAllColors = false;
    [SerializeField] private BallColor boxColor;
    [SerializeField] private float disappearDelay = 5f;
    [SerializeField] private float shrinkSpeed = 2f;

    [Header("Lid Settings")]
    [SerializeField] private float lidShrinkSpeed = 4f;

    [Header("Fill Pump Effect")]
    [SerializeField] private float pumpAmount = 0.08f;
    [SerializeField] private float pumpSpeed = 8f;
    [SerializeField] private float destroyShrinkSpeed = 3f;

    [Header("Fill Tint")]
    [SerializeField] private Color fullTintColor = Color.white; // color to lerp toward when full
    [SerializeField] private float tintIntensity = 0.6f;        // 0 = no tint, 1 = full tint color

    private BoxSpawner spawner;
    private Transform lid;
    private Vector3 lidOriginalScale;
    private Coroutine lidCoroutine;

    // Pump
    private Vector3 boxOriginalScale;
    private Coroutine pumpCoroutine;

    // Destroy shrink
    private Coroutine destroyCoroutine;

    // Tint — each renderer gets its own material instance
    private Renderer[] boxRenderers;
    private Color[] originalColors;
    private static readonly int ColorPropStandard = Shader.PropertyToID("_Color");
    private static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");

    private int currentCount = 0;
    public bool IsFull => currentCount >= capacity;
    public bool IsDustBox => collectAllColors;

    public Action<BallColor> OnBallAdded;
    public Action<BallBox> OnBoxDestroyed;

    // ── LIFECYCLE ─────────────────────────────────────────────────────────────

    private void Start()
    {
        boxOriginalScale = transform.localScale;

        int childCount = transform.childCount;
        if (childCount > 0)
        {
            lid = transform.GetChild(childCount - 1);
            lidOriginalScale = lid.localScale;
        }

        // Cache own material instances so tint is isolated to this box only
        // renderer.material auto-creates a unique instance — won't affect other boxes
        boxRenderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[boxRenderers.Length];
        for (int i = 0; i < boxRenderers.Length; i++)
        {
            Material mat = boxRenderers[i].material; // creates unique instance
            if (mat.HasProperty(ColorPropURP))
                originalColors[i] = mat.GetColor(ColorPropURP);
            else if (mat.HasProperty(ColorPropStandard))
                originalColors[i] = mat.GetColor(ColorPropStandard);
            else
                originalColors[i] = Color.white;
        }

        RefreshLid();
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    public void SetSpawner(BoxSpawner s)
    {
        spawner = s;
        RefreshLid();
    }

    public void RefreshLid()
    {
        if (lid == null) return;

        bool isTop = spawner == null || spawner.IsTopBox(this);

        if (isTop)
        {
            if (lidCoroutine != null) StopCoroutine(lidCoroutine);
            lid.gameObject.SetActive(true);
            lidCoroutine = StartCoroutine(ShrinkLid());
        }
        else
        {
            if (lidCoroutine != null) StopCoroutine(lidCoroutine);
            lid.localScale = lidOriginalScale;
            lid.gameObject.SetActive(true);
        }
    }

    public bool CanAcceptBall(BallColor color)
    {
        if (spawner != null && !spawner.IsTopBox(this))
            return false;

        if (collectAllColors && UIManager.Instance != null && UIManager.Instance.IsBoxTimerRunning)
            return false;

        // Rainbow is a wildcard — accepted by every box
        if (color == BallColor.Rainbow) return true;

        if (collectAllColors) return true;
        return color == boxColor;
    }

    public void AddBall(BallColor color, GameObject ballObj)
    {
        if (IsFull) return;
        if (!gameObject.activeInHierarchy) return;

        // Convert rainbow ball to this box's required color before scoring
        // so UIManager tracks the correct count, and the ball visually matches
        BallColor scoreColor = color;
        if (color == BallColor.Rainbow)
        {
            scoreColor = collectAllColors ? BallColor.Red : boxColor; // dustBox counts as Red fallback
            if (ballObj != null)
            {
                Ball ballScript = ballObj.GetComponent<Ball>();
                if (ballScript != null) ballScript.ConvertToColor(scoreColor);
            }
        }

        currentCount++;

        // Dust boxes (collectAllColors) are disposal boxes — balls dropped
        // into them do NOT count toward the player's color targets.
        // Only dedicated color boxes (Red / Blue / Green) score points.
        // UIManager lives inside the level prefab — it is null during the
        // destroy/respawn window of a level swap. Guard every access.
        if (!collectAllColors && UIManager.Instance != null)
            UIManager.Instance.AddBall(scoreColor);

        // Drop sound — fires for EVERY box including dust boxes.
        // Combo pitch climbs on rapid consecutive drops.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBallDrop();

        // Pump the box scale to show it filling up
        TriggerPump();

        // Update color tint based on fill ratio
        ApplyFillTint();

        if (ballObj != null)
            StartCoroutine(ShrinkAndDestroyBall(ballObj));

        if (currentCount >= capacity)
            DestroyBox();
    }

    // ── FILL TINT ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lerps each renderer on this box from its original color toward
    /// fullTintColor based on fill ratio. Uses renderer.material so only
    /// THIS box instance is affected — other same-colored boxes are untouched.
    /// </summary>
    private void ApplyFillTint()
    {
        if (boxRenderers == null) return;

        float fillRatio = (float)currentCount / capacity; // 0 → 1

        for (int i = 0; i < boxRenderers.Length; i++)
        {
            if (boxRenderers[i] == null) continue;

            Color target = Color.Lerp(originalColors[i], fullTintColor, fillRatio * tintIntensity);
            Material mat = boxRenderers[i].material;

            if (mat.HasProperty(ColorPropURP))
                mat.SetColor(ColorPropURP, target);
            else if (mat.HasProperty(ColorPropStandard))
                mat.SetColor(ColorPropStandard, target);
        }
    }

    // ── PUMP EFFECT ───────────────────────────────────────────────────────────

    private void TriggerPump()
    {
        if (pumpCoroutine != null) StopCoroutine(pumpCoroutine);
        pumpCoroutine = StartCoroutine(PumpBox());
    }

    /// <summary>
    /// Scales the box up slightly then lerps back to original scale.
    /// Gives a "filling / pumped" feel each time a ball is added.
    /// The target scale also grows slightly with fill percentage
    /// so the box looks progressively more bloated as it fills.
    /// </summary>
    private IEnumerator PumpBox()
    {
        // Fill ratio 0→1 drives a subtle permanent bloat on top of the pump
        float fillRatio = (float)currentCount / capacity;
        float bloat = 1f + (fillRatio * pumpAmount * 2f); // gentle permanent grow

        // Squash and stretch punch:
        // Y stretches up, XZ squash inward — like a rubber ball squeeze
        Vector3 punchScale = new Vector3(
            boxOriginalScale.x * bloat * 0.82f,   // XZ squash
            boxOriginalScale.y * (bloat + pumpAmount * 2f), // Y stretch
            boxOriginalScale.z * bloat * 0.82f
        );

        // Settle target — slight permanent bloat, back to uniform
        Vector3 bloatScale = new Vector3(
            boxOriginalScale.x * bloat,
            boxOriginalScale.y * bloat,
            boxOriginalScale.z * bloat
        );

        // Apply punch instantly
        transform.localScale = punchScale;

        // Lerp back to bloated uniform scale
        float time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime * pumpSpeed;
            transform.localScale = Vector3.Lerp(punchScale, bloatScale, time);
            yield return null;
        }

        transform.localScale = bloatScale;
    }

    // ── DESTROY SHRINK ────────────────────────────────────────────────────────

    private void DestroyBox()
    {
        Debug.Log(gameObject.name + " FULL → Destroyed");

        // Notify spawner immediately so grid shifts and timer starts
        if (spawner != null)
            spawner.OnBoxDestroyed(transform);

        // Stop any ongoing pump
        if (pumpCoroutine != null) StopCoroutine(pumpCoroutine);

        // Shrink box to zero then destroy
        destroyCoroutine = StartCoroutine(ShrinkBoxThenDestroy());
    }

    private IEnumerator ShrinkBoxThenDestroy()
    {
        float time = 0f;
        Vector3 startScale = transform.localScale;

        while (time < 1f)
        {
            time += Time.deltaTime * destroyShrinkSpeed;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, time);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── LID ───────────────────────────────────────────────────────────────────

    private IEnumerator ShrinkLid()
    {
        float time = 0f;
        Vector3 startScale = lid.localScale;

        while (time < 1f)
        {
            if (lid == null) yield break;
            time += Time.deltaTime * lidShrinkSpeed;
            lid.localScale = Vector3.Lerp(startScale, Vector3.zero, time);
            yield return null;
        }

        lid.localScale = Vector3.zero;
        lid.gameObject.SetActive(false);
    }

    // ── BALL SHRINK ───────────────────────────────────────────────────────────

    private IEnumerator ShrinkAndDestroyBall(GameObject ball)
    {
        if (ball == null || !gameObject.activeInHierarchy) yield break;

        yield return new WaitForSeconds(disappearDelay);

        float time = 0f;
        Vector3 startScale = ball.transform.localScale;

        while (time < 1f)
        {
            if (ball == null) yield break;
            time += Time.deltaTime * shrinkSpeed;
            ball.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, time);
            yield return null;
        }

        Destroy(ball);
    }
}