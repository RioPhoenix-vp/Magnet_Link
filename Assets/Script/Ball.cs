using UnityEngine;
using System.Collections;

public enum BallColor
{
    Red,
    Blue,
    Green,
    Rainbow  // ⭐ Wildcard — accepted by any box, converts to box color on drop
}

public class Ball : MonoBehaviour
{
    public BallColor color;

    private Coroutine rainbowCoroutine;
    private static readonly Color[] rainbowColors = new Color[]
    {
        Color.red,
        new Color(1f, 0.5f, 0f),   // orange
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        new Color(0.6f, 0f, 1f),   // violet
    };

    public void SetColor(BallColor newColor)
    {
        color = newColor;

        // Stop any ongoing rainbow shimmer first
        if (rainbowCoroutine != null)
        {
            StopCoroutine(rainbowCoroutine);
            rainbowCoroutine = null;
        }

        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        switch (color)
        {
            case BallColor.Red:
                r.material.color = Color.red;
                break;
            case BallColor.Blue:
                r.material.color = Color.blue;
                break;
            case BallColor.Green:
                r.material.color = Color.green;
                break;
            case BallColor.Rainbow:
                // Start shimmering through colors continuously
                rainbowCoroutine = StartCoroutine(RainbowShimmer(r));
                break;
        }
    }

    /// <summary>
    /// Called by BallBox when the rainbow ball is dropped into a colored box.
    /// Converts rainbow → the box's actual color so UIManager scores correctly.
    /// </summary>
    public void ConvertToColor(BallColor targetColor)
    {
        SetColor(targetColor);
    }

    // ── RAINBOW SHIMMER ───────────────────────────────────────────────────────
    private IEnumerator RainbowShimmer(Renderer r)
    {
        int index = 0;
        float duration = 0.18f; // seconds per color step

        while (true)
        {
            Color from = rainbowColors[index];
            Color to = rainbowColors[(index + 1) % rainbowColors.Length];

            float time = 0f;
            while (time < 1f)
            {
                if (r == null) yield break;
                time += Time.deltaTime / duration;
                r.material.color = Color.Lerp(from, to, time);
                yield return null;
            }

            index = (index + 1) % rainbowColors.Length;
        }
    }
}