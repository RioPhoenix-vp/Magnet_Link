using System.Collections.Generic;
using UnityEngine;

public class MagnetStick : MonoBehaviour
{
    [Header("Magnet Settings")]
    [SerializeField] private float magnetRadius = 5f;
    [SerializeField] private float magnetSpeed = 18f;
    [SerializeField] private float stickDistance = 0.35f;
    [SerializeField] private LayerMask redLayer;
    [SerializeField] private LayerMask blueLayer;
    [SerializeField] private LayerMask greenLayer;

    [Tooltip("Layer the rainbow wildcard ball is on. The magnet attracts rainbow " +
             "balls in addition to the currently selected color, so a rainbow ball " +
             "behaves like whatever color is active when it enters the radius.")]
    [SerializeField] private LayerMask rainbowLayer;

    private LayerMask currentLayer;

    [Header("Capacity")]
    [SerializeField] private int maxBalls = 5;

    [Header("Stack Settings")]
    [SerializeField] private float stackYOffset = 0.3f;
    [SerializeField] private Vector3 stackStartOffset = Vector3.zero;
    [SerializeField] private Vector3 fixedBallScale = new Vector3(0.5467485f, 0.5467485f, 0.5467485f);

    [Header("Debug")]
    [SerializeField] private bool showLogs = true;

    [Header("Pump Settings")]
    [SerializeField] private float collectPumpAmount = 0.12f;  // squash stretch on collect
    [SerializeField] private float collectPumpSpeed = 7f;     // how fast it bounces back
    [SerializeField] private float releaseReturnSpeed = 5f;    // lerp back to normal on release

    private readonly List<Rigidbody> attractedBalls = new List<Rigidbody>();
    [SerializeField] private List<Rigidbody> stuckBalls = new List<Rigidbody>();

    private int attachedBalls = 0;
    private int incomingBalls = 0;

    // Pump state
    private Transform magnetParent;         // the parent to scale (whole magnet hierarchy)
    private Vector3 magnetOriginalScale;
    private Coroutine pumpCoroutine;
    private Coroutine releaseCoroutine;
    

    public List<Rigidbody> GetAllBalls() => stuckBalls;

    public float MagnetRadius
    {
        get => magnetRadius;
        set => magnetRadius = value;
    }

    public float MagnetSpeed
    {
        get => magnetSpeed;
        set => magnetSpeed = value;
    }

    public int MaxBalls
    {
        get => maxBalls;
        set => maxBalls = value;
    }

    private void Start()
    {
        currentLayer = redLayer;

        // Use parent if exists, otherwise scale self
        magnetParent = transform.parent != null ? transform.parent : transform;
        magnetOriginalScale = magnetParent.localScale;
    }

    private void FixedUpdate()
    {
        CollectNearbyBalls();
        MoveIncomingBallsToMagnet();
    }

    private void LateUpdate()
    {
        foreach (var rb in stuckBalls)
            if (rb != null)
                rb.transform.localScale = fixedBallScale;
    }

    // ── COLLECT ───────────────────────────────────────────────────────────────
    private void CollectNearbyBalls()
    {
        if (attachedBalls + incomingBalls >= maxBalls) return;

        // Attract the currently selected color (red / blue / green) …
        Collider[] colorHits = Physics.OverlapSphere(transform.position, magnetRadius, currentLayer);
        foreach (var col in colorHits)
            TryAttract(col);

        // … and ALWAYS also attract rainbow balls, regardless of selected color.
        // A rainbow ball is a wildcard — once attracted it stacks with the
        // active color and converts to the box's color on drop (handled by
        // BallBox.AddBall / Ball.ConvertToColor), so it behaves exactly like
        // whatever color the magnet is currently fetching.
        //
        // We detect rainbow balls by COMPONENT (Ball.color == Rainbow) using a
        // layer-independent overlap, so it works no matter what layer the
        // rainbow prefab's collider is on. This avoids the common pitfall where
        // OverlapSphere filters by the collider's GameObject layer (not the
        // prefab root) and silently misses the ball.
        Collider[] allHits = Physics.OverlapSphere(transform.position, magnetRadius);

        int rainbowFound = 0;
        foreach (var col in allHits)
        {
            Ball b = col.GetComponent<Ball>() ?? col.GetComponentInParent<Ball>();
            if (b == null || b.color != BallColor.Rainbow) continue;

            rainbowFound++;
            TryAttract(col);
        }

        if (showLogs && rainbowFound > 0)
            Debug.Log($"[Magnet] Found {rainbowFound} rainbow ball(s) by component within radius.");
    }

    /// <summary>
    /// Validates a candidate collider and, if eligible, adds its rigidbody to
    /// the attracted list. Shared by both the color and rainbow overlap passes.
    /// </summary>
    private void TryAttract(Collider col)
    {
        if (attachedBalls + incomingBalls >= maxBalls) return;

        Rigidbody rb = col.attachedRigidbody;
        if (rb == null)
        {
            if (showLogs) Debug.Log($"[Magnet] {col.name} has no attachedRigidbody — skipped.");
            return;
        }

        // Already tracked by this magnet (flying in or stuck)? Skip.
        if (attractedBalls.Contains(rb) || stuckBalls.Contains(rb)) return;

        // Skip balls already inside a BallBox (parented after being dropped).
        // Balls on splines/conveyors may have other parents — those are fine.
        if (col.transform.parent != null && col.transform.parent.GetComponent<BallBox>() != null) return;

        attractedBalls.Add(rb);
        incomingBalls++;

        if (showLogs)
        {
            Ball b = rb.GetComponent<Ball>() ?? rb.GetComponentInChildren<Ball>();
            Debug.Log($"[Magnet] Attracting {rb.name} (color: {(b != null ? b.color.ToString() : "?")}).");
        }

        // Make the ball kinematic ONLY while the magnet owns it, so MovePosition
        // drives it cleanly and it doesn't fall mid-flight. This state is always
        // undone by ResetBallPhysics() the moment the magnet lets go (drop into
        // a box keeps it kinematic; any other release restores dynamics).
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// Restores a ball to normal dynamic physics. Called whenever the magnet
    /// releases a ball back into the world WITHOUT placing it in a box, so a
    /// ball can never be left frozen (kinematic) on its own.
    /// </summary>
    private void ResetBallPhysics(Rigidbody rb)
    {
        if (rb == null) return;
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    private void MoveIncomingBallsToMagnet()
    {
        for (int i = attractedBalls.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = attractedBalls[i];
            if (rb == null)
            {
                attractedBalls.RemoveAt(i);
                incomingBalls = Mathf.Max(0, incomingBalls - 1);
                continue;
            }

            // SAFETY: if the magnet is already full of stuck balls, this ball
            // can never complete its journey. Release it back to normal physics
            // and stop tracking it, so it is never left frozen (kinematic).
            if (stuckBalls.Count >= maxBalls)
            {
                ResetBallPhysics(rb);
                attractedBalls.RemoveAt(i);
                incomingBalls = Mathf.Max(0, incomingBalls - 1);
                if (showLogs) Debug.Log($"[Magnet] {rb.name} released — magnet full, won't leave it kinematic.");
                continue;
            }

            // Each incoming ball reserves its own slot ABOVE the already-stuck
            // balls based on its order in the queue, so multiple balls flying in
            // at once don't all fight for the same position.
            int slotIndex = stuckBalls.Count + i;
            Vector3 targetPos = GetAttachPositionForSlot(slotIndex);

            Vector3 toTarget = targetPos - rb.position;
            float dist = toTarget.magnitude;

            // Distance the ball would travel this physics step at magnetSpeed.
            float step = magnetSpeed * Time.fixedDeltaTime;

            // STICK if we're already within the snap window OR if a single step
            // would carry us past the target (prevents fast balls from
            // overshooting the stickDistance window and never registering).
            if (dist <= stickDistance || dist <= step)
            {
                StickToMagnet(rb, targetPos);
                continue;
            }

            // Drive the ball toward the slot with MovePosition — smooth,
            // frame-rate independent, and never overshoots because we clamp the
            // move to the remaining distance. The ball is kinematic during
            // flight (set in TryAttract), so we do NOT touch its velocities —
            // writing velocity on a kinematic body throws Unity warnings.
            Vector3 dir = toTarget / dist;             // normalized
            Vector3 nextPos = rb.position + dir * Mathf.Min(step, dist);

            rb.MovePosition(nextPos);
        }
    }

    /// <summary>
    /// World-space attach position for a given stack slot index (0 = bottom).
    /// </summary>
    private Vector3 GetAttachPositionForSlot(int slotIndex)
    {
        return transform.position + stackStartOffset + Vector3.up * (slotIndex * stackYOffset);
    }

    // Kept for compatibility — previews the next free slot at the top of the stack.
    private Vector3 GetNextAttachPositionPreview()
    {
        return GetAttachPositionForSlot(stuckBalls.Count);
    }

    private void StickToMagnet(Rigidbody ballRB, Vector3 targetPos)
    {
        if (ballRB == null || stuckBalls.Contains(ballRB) || attachedBalls >= maxBalls) return;

        // Ball is already kinematic from flight (set in TryAttract), so no
        // velocity writes here — just lock it in place on the magnet.
        ballRB.isKinematic = true;
        ballRB.useGravity = false;
        ballRB.transform.SetParent(transform, false);
        ballRB.transform.localScale = fixedBallScale;
        ballRB.transform.position = targetPos;

        stuckBalls.Add(ballRB);
        attachedBalls++;
        incomingBalls = Mathf.Max(0, incomingBalls - 1);
        attractedBalls.Remove(ballRB);

        // Catch sound — fires only on a successful stick, after every early
        // return above has been passed. Sits next to TriggerCollectPump so the
        // sound and the squash-stretch punch always happen together.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBallCatch();

        // Pump magnet on each ball collected
        TriggerCollectPump();
    }

    // ── RELEASE ───────────────────────────────────────────────────────────────
    // Called by BallDropBox with the specific box the magnet entered.
    //
    // Rules:
    //   • hoveredBox.CanAcceptBall(color) is true  → drop the ball there.
    //   • hoveredBox.CanAcceptBall(color) is false → do NOT drop, do nothing.
    //
    // This means:
    //   - Hovering over RedBox with red balls   → drops ✅
    //   - Hovering over GreenBox with red balls → ignored, player must move ❌
    //   - Hovering over dustBox with any balls  → drops (collectAllColors=true) ✅
    //
    // No automatic fallback. The player decides where to put unwanted balls.
    //
    public void ReleaseAllBalls(BallBox hoveredBox)
    {
        if (hoveredBox == null) return;
        if (hoveredBox.IsFull)
        {
            if (showLogs) Debug.Log($"Box {hoveredBox.name} is full — not dropping");
            return;
        }
        if (!hoveredBox.gameObject.activeInHierarchy) return;

        bool droppedAny = false;

        for (int i = stuckBalls.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = stuckBalls[i];
            if (rb == null) continue;

            Ball ball = rb.GetComponent<Ball>();
            if (ball == null) continue;

            // ✅ Only drop if THIS box accepts this ball's color
            if (!hoveredBox.CanAcceptBall(ball.color))
            {
                if (showLogs) Debug.Log($"Box {hoveredBox.name} does not accept {ball.color} — ball stays on magnet");
                continue; // leave this ball on the magnet
            }

            // The ball is already kinematic (it was stuck to the magnet), so
            // we don't write velocities here — doing so on a kinematic body
            // throws warnings. Just keep it kinematic and gravity-free as it
            // settles into the box.
            rb.isKinematic = true;
            rb.useGravity = false;

            // Then parent and position safely
            rb.transform.SetParent(hoveredBox.transform, true);
            rb.transform.localScale = fixedBallScale;
            rb.transform.localPosition = new Vector3(
                Random.Range(-0.1f, 0.1f),
                i * 0.1f,
                Random.Range(-0.2f, -0.3f)
            );

            hoveredBox.AddBall(ball.color, rb.gameObject);
            stuckBalls.RemoveAt(i);
            attachedBalls = Mathf.Max(0, attachedBalls - 1);
            droppedAny = true;

            if (showLogs) Debug.Log($"Dropped {ball.color} into {hoveredBox.name}");
        }


        // Only notify wave cleared if ALL balls were successfully dropped
        if (droppedAny && stuckBalls.Count == 0)
        {
            attractedBalls.Clear();
            incomingBalls = 0;

            // Lerp magnet back to original scale on full release
            TriggerReleaseShrink();

            
            
        }
        else if (droppedAny)
        {
            // Some balls dropped but others remain — do a light pump for partial drop
            TriggerCollectPump();
        }
    }

    // ── MAGNET PUMP ───────────────────────────────────────────────────────────

    /// <summary>
    /// Squash and stretch punch on the magnet parent each time a ball sticks.
    /// Y stretches up, XZ squashes in — snaps back to current loaded scale.
    /// </summary>
    private void TriggerCollectPump()
    {
        if (pumpCoroutine != null) StopCoroutine(pumpCoroutine);
        if (releaseCoroutine != null) StopCoroutine(releaseCoroutine);
        pumpCoroutine = StartCoroutine(CollectPump());
    }

    private System.Collections.IEnumerator CollectPump()
    {
        // How loaded is the magnet — more balls = bigger pump
        float loadRatio = maxBalls > 0 ? (float)attachedBalls / maxBalls : 0f;
        float extraBloat = 1f + (loadRatio * collectPumpAmount);

        // Squash XZ, stretch Y
        Vector3 punch = new Vector3(
            magnetOriginalScale.x * extraBloat * 0.80f,
            magnetOriginalScale.y * extraBloat * (1f + collectPumpAmount * 1.5f),
            magnetOriginalScale.z * extraBloat * 0.80f
        );

        // Settle back to a slightly bloated uniform scale (loaded feel)
        Vector3 settle = magnetOriginalScale * extraBloat;

        magnetParent.localScale = punch;

        float time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime * collectPumpSpeed;
            magnetParent.localScale = Vector3.Lerp(punch, settle, time);
            yield return null;
        }

        magnetParent.localScale = settle;
    }

    /// <summary>
    /// Smoothly lerps the magnet parent back to its original scale on release.
    /// </summary>
    private void TriggerReleaseShrink()
    {
        if (pumpCoroutine != null) StopCoroutine(pumpCoroutine);
        if (releaseCoroutine != null) StopCoroutine(releaseCoroutine);
        releaseCoroutine = StartCoroutine(ReleaseShrink());
    }

    private System.Collections.IEnumerator ReleaseShrink()
    {
        Vector3 start = magnetParent.localScale;
        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime * releaseReturnSpeed;
            magnetParent.localScale = Vector3.Lerp(start, magnetOriginalScale, time);
            yield return null;
        }

        magnetParent.localScale = magnetOriginalScale;
    }

    // 🎮 Color selection buttons
    public void SelectRed() => currentLayer = redLayer;
    public void SelectBlue() => currentLayer = blueLayer;
    public void SelectGreen() => currentLayer = greenLayer;
}