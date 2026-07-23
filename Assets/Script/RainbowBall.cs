using UnityEngine;

/// <summary>
/// Attach this to any ball GameObject in the scene to make it a rainbow wildcard.
/// It calls SetColor(Rainbow) on Start — no spawner needed.
/// The magnet picks it up like any other ball.
/// </summary>
[RequireComponent(typeof(Ball))]
public class RainbowBall : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Ball>().SetColor(BallColor.Rainbow);
    }
}