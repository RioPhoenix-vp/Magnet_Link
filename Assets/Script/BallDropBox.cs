using UnityEngine;

public class BallDropBox : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        MagnetStick magnet = other.GetComponentInParent<MagnetStick>();
        if (magnet == null) return;

        // Get THIS box's BallBox component
        BallBox thisBox = GetComponent<BallBox>();
        if (thisBox == null) return;

        hasTriggered = true;

        // Tell the magnet exactly which box it is hovering over
        magnet.ReleaseAllBalls(thisBox);

        Invoke(nameof(ResetTrigger), 0.2f);
    }

    void ResetTrigger() => hasTriggered = false;
}