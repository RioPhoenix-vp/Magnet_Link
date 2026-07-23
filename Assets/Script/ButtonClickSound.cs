using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add to any Button for a click sound. No Inspector wiring — finds its own
/// Button and AudioManager at runtime, so it survives prefab swaps.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();
        });
    }
}