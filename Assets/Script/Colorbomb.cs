using UnityEngine;
using System.Collections;

/// <summary>
/// Attach this to your bomb prefab (currently a color ball, swap later).
///
/// Flow:
///   1. PowerUpManager.UseColorBomb() instantiates the prefab above the arena
///      and calls Launch(spawnPosition, targetPosition).
///   2. The bomb arcs through the air using a simple lerp trajectory.
///   3. On landing it calls Explode():
///        - Finds every loose ball inside explosionRadius on ballLayers.
///        - Destroys each with a shrink animation.
///        - Credits each destroyed ball toward UIManager targets.
///        - Plays the optional explosion VFX prefab.
///
/// Inspector setup on the prefab:
///   • Explosion Radius   — how wide the blast reaches (world units, e.g. 4)
///   • Ball Layers        — same LayerMask used by PowerUpManager (tick your ball layers)
///   • Flight Duration    — seconds to arc from spawn to target (e.g. 0.6)
///   • Arc Height         — how high the arc rises before falling (e.g. 3)
///   • Explosion VFX      — optional particle prefab spawned at impact (can be null)
/// </summary>
public class ColorBomb : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private LayerMask ballLayers;

    [Header("Flight")]
    [SerializeField] private float flightDuration = 0.6f;
    [SerializeField] private float arcHeight = 3f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionVFXPrefab;

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PowerUpManager after instantiating the bomb.
    /// Starts the arc flight toward targetPos.
    /// </summary>
    public void Launch(Vector3 targetPos)
    {
        StartCoroutine(FlyToTarget(transform.position, targetPos));
    }

    // ── FLIGHT ────────────────────────────────────────────────────────────────

    private IEnumerator FlyToTarget(Vector3 start, Vector3 end)
    {
        float elapsed = 0f;

        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            // Lerp X/Z linearly, add parabolic arc on Y
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);   // sin curve: 0 → peak → 0

            transform.position = pos;

            // Spin the bomb while flying for visual polish
            transform.Rotate(Vector3.forward, 360f * Time.deltaTime / flightDuration);

            yield return null;
        }

        transform.position = end;
        Explode();
    }

    // ── EXPLOSION ─────────────────────────────────────────────────────────────

    private void Explode()
    {
        // Spawn optional VFX at impact point
        if (explosionVFXPrefab != null)
            Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);

        // Find all loose balls in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, ballLayers);
        int destroyed = 0;

        foreach (var col in hits)
        {
            Ball ball = col.GetComponent<Ball>() ?? col.GetComponentInParent<Ball>();
            if (ball == null) continue;

            // Skip balls already scored inside a BallBox
            if (col.transform.parent != null &&
                col.transform.parent.GetComponent<BallBox>() != null) continue;

            // Skip balls currently on the magnet (belong to the player)
            if (col.transform.parent != null &&
                col.transform.parent.GetComponent<MagnetStick>() != null) continue;

            // Credit the score toward UIManager targets.
            // Rainbow balls count as Red (no specific box triggered this bomb).
            // If you want to credit only a specific color, pass that color from
            // PowerUpManager and compare ball.color == bombColor here.
            BallColor scoreColor = ball.color == BallColor.Rainbow
                ? BallColor.Red
                : ball.color;

            if (UIManager.Instance != null)
                UIManager.Instance.AddBall(scoreColor);

            // Shrink and destroy with staggered delay for burst feel
            StartCoroutine(ShrinkAndDestroy(ball.gameObject,
                shrinkDuration: 0.35f,
                delay: Random.Range(0f, 0.18f)));

            destroyed++;
        }

        Debug.Log($"[ColorBomb] Exploded at {transform.position} — destroyed {destroyed} balls");

        // Destroy the bomb object itself after the longest possible shrink delay
        Destroy(gameObject, 0.18f + 0.35f + 0.05f);
    }

    // ── SHRINK HELPER ─────────────────────────────────────────────────────────

    private IEnumerator ShrinkAndDestroy(GameObject ball, float shrinkDuration, float delay)
    {
        if (ball == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (ball == null) yield break;

        // Freeze physics so ball stops while shrinking
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Vector3 startScale = ball.transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration && ball != null)
        {
            elapsed += Time.deltaTime;
            ball.transform.localScale = Vector3.Lerp(startScale, Vector3.zero,
                                                     Mathf.Clamp01(elapsed / shrinkDuration));
            yield return null;
        }

        if (ball != null)
            Destroy(ball);
    }

    // ── GIZMO (editor only) ───────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}