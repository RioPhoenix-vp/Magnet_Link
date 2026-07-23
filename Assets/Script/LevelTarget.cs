using UnityEngine;

/// <summary>
/// Per-level ball targets. Put this on the ROOT of each level prefab.
/// Pushes targets in Awake() into the UIManager inside THIS SAME prefab —
/// never uses UIManager.Instance, so there is no stale-singleton race
/// and no Start() ordering ambiguity.
/// </summary>
public class LevelTargets : MonoBehaviour
{
    [Header("Ball targets for THIS level")]
    [SerializeField] private int redTarget = 40;
    [SerializeField] private int blueTarget = 30;
    [SerializeField] private int greenTarget = 20;

    [Header("Level time (seconds). 0 = use LevelTimer's own default.")]
    [SerializeField] private float levelDuration = 0f;

    public float LevelDuration => levelDuration;

    private void Awake()
    {
        // Search THIS prefab instance only — includes inactive children.
        UIManager ui = GetComponentInChildren<UIManager>(true);

        if (ui == null)
        {
            Debug.LogError($"[LevelTargets] No UIManager found inside prefab '{name}'. " +
                           "The UIManager must be a child of this level prefab root.");
            return;
        }

        ui.ApplyLevelTargets(redTarget, blueTarget, greenTarget);
        Debug.Log($"[LevelTargets] R:{redTarget} B:{blueTarget} G:{greenTarget}");
    }
}